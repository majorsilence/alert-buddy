using AlertBuddy.Core.Alerts;
using AlertBuddy.Core.Store;
using AlertBuddy.ViewModels.Screens;

namespace AlertBuddy.ViewModels.Services
{
    /// <summary>
    /// Puts the alarm takeover on screen while an alarm is active and nobody has responded, and takes it off again afterwards (PLAN.md
    /// section 9: it replaces Home while any alarm is Active and not Acknowledged). It covers whatever is showing, settings included: an
    /// alarm is more important than the screen the child was on, and after the tap the child is back where they were.
    /// </summary>
    public sealed class TakeoverCoordinator : IDisposable
    {
        private readonly AlertHub hub;
        private readonly INavigator navigator;
        private readonly IScreenFactory screens;
        private readonly HubSubscription subscription;

        /// <summary>Starts watching, and shows the takeover at once if an alarm is already pending (restored from before a restart).</summary>
        public TakeoverCoordinator (AlertHub hub, INavigator navigator, IScreenFactory screens, IUiDispatcher dispatcher)
        {
            this.hub = hub ?? throw new ArgumentNullException (nameof (hub));
            this.navigator = navigator ?? throw new ArgumentNullException (nameof (navigator));
            this.screens = screens ?? throw new ArgumentNullException (nameof (screens));

            subscription = new HubSubscription (hub, dispatcher, _ => Evaluate ());
            Evaluate ();
        }

        private void Evaluate ()
        {
            // The newest alarm nobody has responded to.
            var pending = hub.Snapshot.Active
                .Where (a => a.Level == AlertLevel.Alarm && a.Status == AlertStatus.Active)
                .OrderByDescending (a => a.Time)
                .FirstOrDefault ();

            var showing = navigator.Current as AlarmViewModel;

            if (pending is null) {
                if (showing is not null)
                    navigator.GoBack ();
                return;
            }

            if (showing is not null && showing.AlertId == pending.Id)
                return;

            // A different alarm is now the one to show: replace, never stack.
            if (showing is not null)
                navigator.GoBack ();

            navigator.Show (screens.Alarm (pending));
        }

        /// <summary>Stops watching.</summary>
        public void Dispose () => subscription.Dispose ();
    }
}
