using AlertBuddy.Core.Localization;
using System.Collections.ObjectModel;
using System.Globalization;
using AlertBuddy.Core.Abstractions;
using AlertBuddy.Core.Alerts;
using AlertBuddy.Core.Store;
using AlertBuddy.ViewModels.Copy;
using AlertBuddy.ViewModels.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AlertBuddy.ViewModels.Screens
{
    /// <summary>A day's alerts under a heading such as "Today".</summary>
    /// <param name="Heading">"Today", "Yesterday" or the date.</param>
    /// <param name="Items">The tickets for that day, newest first.</param>
    public sealed record AlertDayGroup (string Heading, IReadOnlyList<AlertItemViewModel> Items);

    /// <summary>
    /// The Alert Book: past alerts, newest first, grouped by day (PLAN.md section 9). A history, not a collection: nothing here is earned
    /// (section 4.1). Clearing it needs the grown-up gate.
    /// </summary>
    public sealed partial class AlertBookViewModel : ScreenViewModel
    {
        private readonly AlertEngine engine;
        private readonly AlertHub hub;
        private readonly INavigator navigator;
        private readonly IScreenFactory screens;
        private readonly IClock clock;
        private readonly TimeZoneInfo zone;

        [ObservableProperty]
        private bool isEmpty = true;

        /// <summary>The alerts grouped by day, newest day first.</summary>
        public ObservableCollection<AlertDayGroup> Groups { get; } = [];

        /// <summary>The invitation shown when there is nothing yet.</summary>
        public string EmptyText => Words.AlertBookEmpty;

        /// <summary>Creates the Alert Book.</summary>
        public AlertBookViewModel (AlertEngine engine, AlertHub hub, INavigator navigator, IScreenFactory screens, IClock clock, IUiDispatcher dispatcher, TimeZoneInfo? zone = null)
        {
            this.engine = engine ?? throw new ArgumentNullException (nameof (engine));
            this.hub = hub ?? throw new ArgumentNullException (nameof (hub));
            this.navigator = navigator ?? throw new ArgumentNullException (nameof (navigator));
            this.screens = screens ?? throw new ArgumentNullException (nameof (screens));
            this.clock = clock ?? throw new ArgumentNullException (nameof (clock));
            this.zone = zone ?? TimeZoneInfo.Local;

            Own (new HubSubscription (hub, dispatcher, _ => Refresh ()));
            Refresh ();
        }

        [RelayCommand]
        private void Back () => navigator.GoBack ();

        /// <summary>Asks for the grown-up gate, and clears the history only once it is unlocked.</summary>
        [RelayCommand]
        private void ClearHistory () => navigator.Show (screens.Gate (engine.ClearHistory));

        private void Refresh ()
        {
            if (IsDisposed)
                return;

            var history = hub.Snapshot.History;
            var today = TimeZoneInfo.ConvertTime (clock.Now, zone).Date;

            var groups = history
                .GroupBy (a => TimeZoneInfo.ConvertTime (a.Time, zone).Date)
                .OrderByDescending (g => g.Key)
                .Select (g => new AlertDayGroup (Heading (g.Key, today),
                    g.OrderByDescending (a => a.Time).Select (a => new AlertItemViewModel (a, clock, x => navigator.Show (screens.Detail (x)))).ToList ()))
                .ToList ();

            Groups.Clear ();
            foreach (var group in groups)
                Groups.Add (group);

            IsEmpty = groups.Count == 0;
        }

        // Sentence case, no all-caps labels (PLAN.md section 8.2). The date is written in the language in use.
        private static string Heading (DateTime day, DateTime today)
            => day == today ? Loc.T ("Today")
             : day == today.AddDays (-1) ? Loc.T ("Yesterday")
             : day.ToString ("dddd d MMMM", Loc.Culture);
    }
}
