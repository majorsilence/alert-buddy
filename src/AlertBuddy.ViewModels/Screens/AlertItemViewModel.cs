using AlertBuddy.Core.Localization;
using AlertBuddy.Core.Abstractions;
using AlertBuddy.Core.Alerts;
using AlertBuddy.ViewModels.Copy;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AlertBuddy.ViewModels.Screens
{
    /// <summary>One ticket: a source, how long ago, one sentence, and a temperature when there is one (PLAN.md section 7.5).</summary>
    public sealed partial class AlertItemViewModel : ObservableObject
    {
        private readonly IClock clock;
        private readonly Action<Alert> open;

        [ObservableProperty]
        private string source = "";

        [ObservableProperty]
        private AlertLevel level;

        [ObservableProperty]
        private AlertStatus status;

        [ObservableProperty]
        private string timeAgo = "";

        [ObservableProperty]
        private string sentence = "";

        [ObservableProperty]
        private double? temperature;

        /// <summary>Creates the ticket for an alert.</summary>
        /// <param name="alert">The alert shown.</param>
        /// <param name="clock">For "22 min".</param>
        /// <param name="open">What tapping the ticket does: open its detail.</param>
        public AlertItemViewModel (Alert alert, IClock clock, Action<Alert> open)
        {
            this.clock = clock ?? throw new ArgumentNullException (nameof (clock));
            this.open = open ?? throw new ArgumentNullException (nameof (open));
            Alert = alert ?? throw new ArgumentNullException (nameof (alert));
            Apply (alert);
        }

        /// <summary>The alert this ticket shows, as of the last <see cref="Update"/>.</summary>
        public Alert Alert { get; private set; }

        /// <summary>The alert's id, which is what keeps this ticket the same ticket as the alert upgrades.</summary>
        public string Id => Alert.Id;

        /// <summary>Shows a newer version of the same alert.</summary>
        public void Update (Alert alert)
        {
            ArgumentNullException.ThrowIfNull (alert);
            Alert = alert;
            Apply (alert);
        }

        /// <summary>Recomputes "22 min" against the clock. Called on a timer, so the ticket ages while it is on screen.</summary>
        public void RefreshTime () => TimeAgo = Words.TimeAgo (clock.Now - Alert.Time);

        [RelayCommand]
        private void Open () => open (Alert);

        private void Apply (Alert alert)
        {
            Source = alert.Source;
            Level = alert.Level;
            Status = alert.Status;
            Temperature = alert.Temperature;
            Sentence = SentenceFor (alert);
            RefreshTime ();
        }

        // "41 degrees. Keep an eye on it." What to do is always said, because a child reading a card needs to know.
        private static string SentenceFor (Alert alert)
        {
            var advice = alert.Status == AlertStatus.Resolved ? Loc.T ("All clear.")
                : alert.Level == AlertLevel.Alarm ? Loc.T ("Tell a grown-up.")
                : Loc.T ("Keep an eye on it.");

            return alert.Temperature is { } t ? Loc.F ("{0}. {1}", Words.Degrees (t), advice) : advice;
        }
    }
}
