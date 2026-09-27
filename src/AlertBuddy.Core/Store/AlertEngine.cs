using AlertBuddy.Core.Abstractions;
using AlertBuddy.Core.Alerts;
using AlertBuddy.Core.Interpretation;
using AlertBuddy.Core.Ntfy;

namespace AlertBuddy.Core.Store
{
    /// <summary>
    /// The pipeline from a stream event to the screen: interpret it, apply it to the store, save the state, publish it on the hub.
    /// One place holds the lock, so the order things are applied is the order they are announced.
    /// </summary>
    public sealed class AlertEngine
    {
        // Subscribers run under this lock (so the announce order matches the apply order). It is reentrant, which lets a handler act on
        // the engine; a handler must not block waiting on another thread that wants the engine.
        private readonly object gate = new ();
        private readonly AlertStore store;
        private readonly AlertHub hub;
        private readonly IClock clock;
        private readonly IAlertStateStore? persistence;
        private readonly Action<Exception>? persistFailed;
        private AlertInterpreter interpreter;

        /// <summary>Creates the engine and loads any saved state, so "what is active right now" is right before the first message.</summary>
        /// <param name="store">The alert rules and history.</param>
        /// <param name="hub">Where changes are announced.</param>
        /// <param name="clock">The current time.</param>
        /// <param name="persistence">Where state is saved, or null to keep it in memory only (Practice mode).</param>
        /// <param name="interpreter">How messages are read; the defaults when null.</param>
        /// <param name="persistFailed">Told when saving fails. Saving failing must not stop alerts being shown.</param>
        public AlertEngine (
            AlertStore store,
            AlertHub hub,
            IClock clock,
            IAlertStateStore? persistence = null,
            AlertInterpreter? interpreter = null,
            Action<Exception>? persistFailed = null)
        {
            this.store = store ?? throw new ArgumentNullException (nameof (store));
            this.hub = hub ?? throw new ArgumentNullException (nameof (hub));
            this.clock = clock ?? throw new ArgumentNullException (nameof (clock));
            this.persistence = persistence;
            this.persistFailed = persistFailed;
            this.interpreter = interpreter ?? new AlertInterpreter ();

            if (persistence?.Load () is { } saved) {
                store.Restore (saved);
                hub.SetAlerts (store.Active, store.History);
            }
        }

        /// <summary>The id to resume the stream from, saved with the alerts.</summary>
        public string? LastMessageId => store.LastMessageId;

        /// <summary>Uses new interpretation settings for the messages that follow. Alerts already open are left as they are.</summary>
        public void SetInterpreter (AlertInterpreter newInterpreter)
        {
            lock (gate)
                interpreter = newInterpreter ?? throw new ArgumentNullException (nameof (newInterpreter));
        }

        /// <summary>Applies the settings that shape how alerts are read and kept: the interpretation rules and the silence window.</summary>
        public void Configure (Settings.AppSettings settings)
        {
            ArgumentNullException.ThrowIfNull (settings);

            lock (gate) {
                interpreter = new AlertInterpreter (settings.Interpretation);
                store.Configure (store.Options with { SilenceWindow = settings.SilenceWindow });
            }
        }

        /// <summary>Tells the hub the connection state.</summary>
        public void SetConnection (ConnectionInfo info) => hub.SetConnection (info);

        /// <summary>Handles one event from the stream. Only messages matter; an open or a keepalive returns null.</summary>
        public AlertChange? Handle (NtfyEvent evt)
        {
            ArgumentNullException.ThrowIfNull (evt);

            if (evt.Kind != NtfyEventKind.Message || evt.Message is null)
                return null;

            lock (gate) {
                var interpreted = interpreter.Interpret (evt.Message);
                var change = store.Apply (interpreted, evt.Origin, clock.Now);

                // A duplicate changed nothing, and there is no point announcing or saving it.
                if (change.Kind != AlertChangeKind.Duplicate) {
                    Save ();
                    hub.Publish (change, store.Active, store.History);
                }

                return change;
            }
        }

        /// <summary>The child said "I told a grown-up".</summary>
        public Alert? Acknowledge (string alertId)
        {
            lock (gate) {
                var alert = store.Acknowledge (alertId, clock.Now);
                if (alert is not null)
                    PublishLocal (AlertChangeKind.Acknowledged, alert);
                return alert;
            }
        }

        /// <summary>A grown-up said "Got it".</summary>
        public Alert? MarkHandled (string alertId)
        {
            lock (gate) {
                var alert = store.Handle (alertId);
                if (alert is not null)
                    PublishLocal (AlertChangeKind.Handled, alert);
                return alert;
            }
        }

        /// <summary>Clears the resolved alerts from the Alert Book.</summary>
        public void ClearHistory ()
        {
            lock (gate) {
                store.ClearHistory ();
                Save ();

                // Announced, unlike the silent load at startup, because a screen may be showing the Alert Book right now.
                hub.Publish (new AlertChange (AlertChangeKind.HistoryCleared, null, AlertSound.None, MessageOrigin.Live), store.Active, store.History);
            }
        }

        // A local action changes state and is announced, but it is not news and makes no sound.
        private void PublishLocal (AlertChangeKind kind, Alert alert)
        {
            Save ();
            hub.Publish (new AlertChange (kind, alert, AlertSound.None, MessageOrigin.Live), store.Active, store.History);
        }

        private void Save ()
        {
            if (persistence is null)
                return;

            try {
                persistence.Save (store.Export ());
            } catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) {
                persistFailed?.Invoke (ex);
            }
        }
    }
}
