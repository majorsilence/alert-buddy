using AlertBuddy.Core.Alerts;
using AlertBuddy.Core.Store;
using AlertBuddy.ViewModels.Copy;
using AlertBuddy.ViewModels.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AlertBuddy.ViewModels.Screens
{
    /// <summary>
    /// The alarm takeover: "Tell a grown-up now." and one big button (PLAN.md sections 4.3 and 9). The child is a helper, never the
    /// responder, so the button only records that a grown-up was told. Both buttons change local state; the alert stays until the server
    /// sends the all clear.
    /// </summary>
    public sealed partial class AlarmViewModel : ScreenViewModel, IHandlesBack
    {
        private readonly AlertEngine engine;
        private readonly AlertHub hub;

        [ObservableProperty]
        private string detail = "";

        [ObservableProperty]
        private bool isAcknowledged;

        [ObservableProperty]
        private double? temperature;

        /// <summary>Creates the takeover for one alarm.</summary>
        public AlarmViewModel (Alert alert, AlertEngine engine, AlertHub hub, IUiDispatcher dispatcher)
        {
            Alert = alert ?? throw new ArgumentNullException (nameof (alert));
            this.engine = engine ?? throw new ArgumentNullException (nameof (engine));
            this.hub = hub ?? throw new ArgumentNullException (nameof (hub));

            Own (new HubSubscription (hub, dispatcher, _ => Refresh ()));
            Apply (alert);
        }

        /// <summary>The alarm shown, as of the latest change.</summary>
        public Alert Alert { get; private set; }

        /// <summary>The id of the alarm, so a coordinator can tell whether this takeover is for the alarm that is pending.</summary>
        public string AlertId => Alert.Id;

        /// <summary>"Tell a grown-up now."</summary>
        public string Heading => Words.TellAGrownUpNow;

        /// <summary>The child's button: "I told a grown-up".</summary>
        public string ToldButtonText => Words.ToldAGrownUp;

        /// <summary>The grown-up's small hold button: "Got it".</summary>
        public string GotItButtonText => Words.GotIt;

        /// <summary>What the screen says after the child has tapped: "Thank you. A grown-up is on it."</summary>
        public string ThankYouText => Words.ThankYou;

        /// <summary>
        /// Back does nothing while the alarm is unanswered. Only the child's button, a grown-up's "Got it", or the all clear ends the takeover;
        /// once the alarm has been answered back is an ordinary back.
        /// </summary>
        public bool HandleBack () => !IsAcknowledged;

        [RelayCommand]
        private void ToldAGrownUp () => engine.Acknowledge (Alert.Id);

        [RelayCommand]
        private void GotIt () => engine.MarkHandled (Alert.Id);

        private void Refresh ()
        {
            if (IsDisposed)
                return;

            // The latest version of this alarm, whether it is still open or has resolved while the screen was up.
            var latest = hub.Snapshot.History.FirstOrDefault (a => a.Id == Alert.Id);
            if (latest is not null)
                Apply (latest);
        }

        private void Apply (Alert alert)
        {
            Alert = alert;
            Temperature = alert.Temperature;
            IsAcknowledged = alert.Status != AlertStatus.Active;

            // "Too hot" is only true of an alert that carried a temperature; any other alarm simply needs a look.
            Detail = alert.Temperature is not null ? Words.TooHot (alert.Source) : Words.NeedsALook (alert.Source);
        }
    }
}
