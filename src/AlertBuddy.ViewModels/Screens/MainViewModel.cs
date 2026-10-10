using System.Collections.ObjectModel;
using AlertBuddy.Core.Abstractions;
using AlertBuddy.Core.Alerts;
using AlertBuddy.Core.Settings;
using AlertBuddy.Core.Store;
using AlertBuddy.ViewModels.Copy;
using AlertBuddy.ViewModels.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AlertBuddy.ViewModels.Screens
{
    /// <summary>Home: the buddy, one sentence, the connection, the open alerts, two big buttons (PLAN.md sections 7.5 and 9).</summary>
    public sealed partial class MainViewModel : ScreenViewModel
    {
        private readonly AlertHub hub;
        private readonly INavigator navigator;
        private readonly IScreenFactory screens;
        private readonly IClock clock;
        private readonly SettingsService settings;
        private readonly IBackgroundListener background;
        private readonly IScheduler scheduler;
        private readonly TimeZoneInfo zone;
        private readonly IKeepAwake? keepAwake;
        private readonly Dictionary<string, AlertItemViewModel> items = new ();
        private AllClearInfo? allClear;
        private DateTimeOffset allClearUntil;
        private IDisposable? allClearTimer;

        [ObservableProperty]
        private BeaconMood mood;

        [ObservableProperty]
        private string statusText = "";

        [ObservableProperty]
        private string connectionText = "";

        [ObservableProperty]
        private string? banner;

        [ObservableProperty]
        private string buddyName = "";

        [ObservableProperty]
        private bool unencrypted;

        /// <summary>The open alerts, newest first.</summary>
        public ObservableCollection<AlertItemViewModel> ActiveAlerts { get; } = [];

        /// <summary>Creates Home.</summary>
        public MainViewModel (
            AlertHub hub,
            INavigator navigator,
            IScreenFactory screens,
            IClock clock,
            IUiDispatcher dispatcher,
            IScheduler scheduler,
            SettingsService settings,
            IBackgroundListener background,
            TimeZoneInfo? zone = null,
            IKeepAwake? keepAwake = null)
        {
            this.hub = hub ?? throw new ArgumentNullException (nameof (hub));
            this.navigator = navigator ?? throw new ArgumentNullException (nameof (navigator));
            this.screens = screens ?? throw new ArgumentNullException (nameof (screens));
            this.clock = clock ?? throw new ArgumentNullException (nameof (clock));
            this.settings = settings ?? throw new ArgumentNullException (nameof (settings));
            this.background = background ?? throw new ArgumentNullException (nameof (background));
            this.scheduler = scheduler ?? throw new ArgumentNullException (nameof (scheduler));
            this.zone = zone ?? TimeZoneInfo.Local;
            this.keepAwake = keepAwake;

            Own (new HubSubscription (hub, dispatcher, OnChange));

            // "Last heard 3 min ago" and every ticket's age move on their own, without a message having to arrive.
            Own (scheduler.Every (TimeSpan.FromSeconds (15), Refresh));

            void OnSettingsChanged () => dispatcher.Post (Refresh);
            settings.Changed += OnSettingsChanged;
            Own (new Unsubscribe (() => settings.Changed -= OnSettingsChanged));

            Refresh ();
        }

        /// <summary>
        /// Bedside mode (PLAN.md sections 6.3 and 8.7): the device is a status display on a stand, so the screen stays on and the look goes
        /// to Night. The view applies the look; this keeps the screen awake, and lets go of it when bedside mode ends or Home is left for good.
        /// </summary>
        [ObservableProperty]
        private bool isBedside;

        /// <summary>Whether this device can keep its screen on, so the view only offers bedside mode where it works.</summary>
        public bool CanBedside => keepAwake is not null;

        partial void OnIsBedsideChanged (bool value)
        {
            if (keepAwake is not null)
                keepAwake.Enabled = value;
        }

        [RelayCommand (CanExecute = nameof (CanBedside))]
        private void ToggleBedside () => IsBedside = !IsBedside;

        [RelayCommand]
        private void OpenBook () => navigator.GoTo<AlertBookViewModel> ();

        [RelayCommand]
        private void StartPractice () => navigator.GoTo<PracticeViewModel> ();

        /// <summary>The gear. A tap asks for the PIN, then opens settings.</summary>
        [RelayCommand]
        // A tap on the gear opens the gate, which is the PIN: one step, not a hold and then a PIN.
        private void OpenSettings () => navigator.Show (screens.Gate (() => navigator.GoTo<SettingsViewModel> ()));

        private void OnChange (AlertChange? change)
        {
            // An all clear is celebrated for a few seconds, and only when it is news: a replayed one is history.
            if (change is { Kind: AlertChangeKind.Resolved, Origin: Core.Ntfy.MessageOrigin.Live, Alert: { } cleared }) {
                allClear = new AllClearInfo (cleared.Source, cleared.Temperature is not null);
                allClearUntil = clock.Now + HomeStatusCalculator.AllClearFor;
                allClearTimer?.Dispose ();
                allClearTimer = scheduler.Schedule (HomeStatusCalculator.AllClearFor, Refresh);
            }

            Refresh ();
        }

        private void Refresh ()
        {
            if (IsDisposed)
                return;

            var snapshot = hub.Snapshot;
            var current = settings.Current;
            var now = clock.Now;

            var localTime = TimeOnly.FromDateTime (TimeZoneInfo.ConvertTime (now, zone).DateTime);
            var isNight = current.Night.Enabled && current.Night.Covers (localTime);
            var celebrating = allClear is not null && now < allClearUntil ? allClear : null;

            var status = HomeStatusCalculator.Compute (snapshot, current.BuddyName, celebrating, isNight);
            Mood = status.Mood;
            StatusText = status.Text;
            BuddyName = current.BuddyName;
            Unencrypted = snapshot.Connection.Unencrypted;

            var configured = !string.IsNullOrWhiteSpace (current.ServerUrl) && !string.IsNullOrWhiteSpace (current.Topic);
            ConnectionText = HomeStatusCalculator.ConnectionSentence (snapshot.Connection, now, configured);

            // The honest banner: if the app cannot listen with the screen off, it says so plainly (PLAN.md principle 5).
            Banner = background.WhyNot is { } reason ? Words.CannotListenInBackground (current.BuddyName, reason) : null;

            SyncAlerts (snapshot.Active);
        }

        // Tickets keep their identity while the alert upgrades, so a view can animate the change instead of rebuilding the list.
        private void SyncAlerts (IReadOnlyList<Alert> active)
        {
            var wanted = active.OrderByDescending (a => a.Time).ToList ();

            foreach (var alert in wanted) {
                if (items.TryGetValue (alert.Id, out var existing))
                    existing.Update (alert);
                else
                    items[alert.Id] = new AlertItemViewModel (alert, clock, a => navigator.Show (screens.Detail (a)));
            }

            foreach (var gone in items.Keys.Except (wanted.Select (a => a.Id)).ToList ())
                items.Remove (gone);

            foreach (var item in items.Values)
                item.RefreshTime ();

            var order = wanted.Select (a => items[a.Id]).ToList ();
            if (!ActiveAlerts.SequenceEqual (order)) {
                ActiveAlerts.Clear ();
                foreach (var item in order)
                    ActiveAlerts.Add (item);
            }
        }

        protected override void OnDisposed ()
        {
            allClearTimer?.Dispose ();
            if (IsBedside && keepAwake is not null)
                keepAwake.Enabled = false;
        }
    }
}
