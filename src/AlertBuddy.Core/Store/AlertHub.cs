using AlertBuddy.Core.Alerts;
using AlertBuddy.Core.Ntfy;

namespace AlertBuddy.Core.Store
{
    /// <summary>What is true right now: the open alerts, the Alert Book and the connection.</summary>
    /// <param name="Active">Open alerts, newest first.</param>
    /// <param name="History">Every alert, newest first.</param>
    /// <param name="Connection">The listener's state.</param>
    public sealed record HubSnapshot (IReadOnlyList<Alert> Active, IReadOnlyList<Alert> History, ConnectionInfo Connection)
    {
        /// <summary>The state before anything has happened.</summary>
        public static HubSnapshot Empty { get; } = new ([], [], ConnectionInfo.Initial);
    }

    /// <summary>
    /// In-process publish and subscribe between whatever listens (the Android service, a desktop runner) and whatever shows (the UI).
    /// A plain class with no framework in it (PLAN.md section 7.2). Events are raised on the publisher's thread, which is a background
    /// thread; a subscriber that touches a control marshals first, through the view models' dispatcher.
    /// </summary>
    public sealed class AlertHub
    {
        private readonly object gate = new ();
        private HubSnapshot snapshot = HubSnapshot.Empty;

        /// <summary>Something changed in the store: a new alert, an upgrade, an all clear, an acknowledgement, a test.</summary>
        public event Action<AlertChange>? AlertChanged;

        /// <summary>The connection state changed.</summary>
        public event Action<ConnectionInfo>? ConnectionChanged;

        /// <summary>A subscriber threw. It is reported here and the other subscribers still run: a faulty screen must not stop the listener.</summary>
        public event Action<Exception>? HandlerFailed;

        /// <summary>
        /// The current state. A late subscriber (the UI attaching to a service that has been running for hours) subscribes and then reads
        /// this; a view recomputes from it rather than trusting that it saw every event.
        /// </summary>
        public HubSnapshot Snapshot {
            get {
                lock (gate)
                    return snapshot;
            }
        }

        /// <summary>Records the new state and tells the subscribers.</summary>
        public void Publish (AlertChange change, IReadOnlyList<Alert> active, IReadOnlyList<Alert> history)
        {
            ArgumentNullException.ThrowIfNull (change);

            lock (gate)
                snapshot = snapshot with { Active = active, History = history };

            Raise (AlertChanged, change);
        }

        /// <summary>Replaces the alerts without announcing a change: the state loaded from disk at startup.</summary>
        public void SetAlerts (IReadOnlyList<Alert> active, IReadOnlyList<Alert> history)
        {
            lock (gate)
                snapshot = snapshot with { Active = active, History = history };
        }

        /// <summary>Records the connection state and tells the subscribers, when it actually changed.</summary>
        public void SetConnection (ConnectionInfo info)
        {
            ArgumentNullException.ThrowIfNull (info);

            lock (gate) {
                if (snapshot.Connection == info)
                    return;

                snapshot = snapshot with { Connection = info };
            }

            Raise (ConnectionChanged, info);
        }

        private void Raise<T> (Action<T>? handlers, T argument)
        {
            if (handlers is null)
                return;

            foreach (var handler in handlers.GetInvocationList ().Cast<Action<T>> ()) {
                try {
                    handler (argument);
                } catch (Exception ex) {
                    HandlerFailed?.Invoke (ex);
                }
            }
        }
    }
}
