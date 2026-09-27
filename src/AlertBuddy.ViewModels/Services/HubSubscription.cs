using AlertBuddy.Core.Store;

namespace AlertBuddy.ViewModels.Services
{
    /// <summary>
    /// Subscribes a view model to the <see cref="AlertHub"/> and delivers every event on the UI thread through the dispatcher (PLAN.md
    /// section 7.5, threading). The hub raises events on whatever thread the listener runs on; a view model never sees that.
    /// </summary>
    public sealed class HubSubscription : IDisposable
    {
        private readonly AlertHub hub;
        private readonly IUiDispatcher dispatcher;
        private readonly Action<AlertChange?> onChange;
        private volatile bool disposed;

        /// <summary>Subscribes. <paramref name="onChange"/> receives the change for an alert event, or null when only the connection changed.</summary>
        public HubSubscription (AlertHub hub, IUiDispatcher dispatcher, Action<AlertChange?> onChange)
        {
            this.hub = hub ?? throw new ArgumentNullException (nameof (hub));
            this.dispatcher = dispatcher ?? throw new ArgumentNullException (nameof (dispatcher));
            this.onChange = onChange ?? throw new ArgumentNullException (nameof (onChange));

            hub.AlertChanged += OnAlert;
            hub.ConnectionChanged += OnConnection;
        }

        private void OnAlert (AlertChange change) => Post (change);

        private void OnConnection (Core.Ntfy.ConnectionInfo _) => Post (null);

        private void Post (AlertChange? change)
            => dispatcher.Post (() => {
                // A change queued before Dispose must not reach a screen that has already been left.
                if (!disposed)
                    onChange (change);
            });

        /// <summary>Stops delivering.</summary>
        public void Dispose ()
        {
            disposed = true;
            hub.AlertChanged -= OnAlert;
            hub.ConnectionChanged -= OnConnection;
        }
    }
}
