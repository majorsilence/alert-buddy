using AlertBuddy.Core.Alerts;
using AlertBuddy.Core.Interpretation;
using AlertBuddy.Core.Ntfy;

namespace AlertBuddy.Core.Store
{
    /// <summary>
    /// The active set and the bounded history, and every rule of PLAN.md section 4.2 about how a message changes them. Pure and
    /// in-memory: no clock of its own (the time is passed in), no I/O, no threads. Safe to call from any thread.
    /// </summary>
    public sealed class AlertStore
    {
        private readonly object gate = new ();
        private AlertStoreOptions options;
        private readonly List<Alert> alerts = [];          // oldest first
        private readonly Queue<string> seenOrder = new ();
        private readonly HashSet<string> seen = new (StringComparer.Ordinal);

        /// <summary>Creates a store with the given rules.</summary>
        public AlertStore (AlertStoreOptions? options = null) => this.options = options ?? new AlertStoreOptions ();

        /// <summary>The rules in force.</summary>
        public AlertStoreOptions Options {
            get {
                lock (gate)
                    return options;
            }
        }

        /// <summary>Changes the rules, for the messages that follow. A grown-up changing the silence window in settings takes effect at once.</summary>
        public void Configure (AlertStoreOptions newOptions)
        {
            ArgumentNullException.ThrowIfNull (newOptions);

            lock (gate) {
                options = newOptions;
                Trim ();
            }
        }

        /// <summary>The id of the newest message processed, to resume the stream from.</summary>
        public string? LastMessageId { get; private set; }

        /// <summary>The alerts that are still open, newest first.</summary>
        public IReadOnlyList<Alert> Active {
            get {
                lock (gate)
                    return alerts.Where (a => a.IsOpen).OrderByDescending (a => a.Time).ToList ();
            }
        }

        /// <summary>Every alert, open and resolved, newest first: the Alert Book.</summary>
        public IReadOnlyList<Alert> History {
            get {
                lock (gate)
                    return alerts.OrderByDescending (a => a.Time).ToList ();
            }
        }

        /// <summary>
        /// Applies one message and says what happened and whether it calls for a sound.
        /// </summary>
        /// <param name="message">The message, read as an alert event.</param>
        /// <param name="origin">Whether it is news or a replay to rebuild state. A replay never sounds.</param>
        /// <param name="now">The current time, from the caller's clock.</param>
        public AlertChange Apply (InterpretedMessage message, MessageOrigin origin, DateTimeOffset now)
        {
            ArgumentNullException.ThrowIfNull (message);

            lock (gate) {
                if (!Remember (message.MessageId))
                    return new AlertChange (AlertChangeKind.Duplicate, null, AlertSound.None, origin);

                LastMessageId = message.MessageId;

                // A sound is for news: a live message, and one that is not already stale.
                var fresh = origin == MessageOrigin.Live && now - message.Time <= options.SoundFreshness;

                return message.Kind switch {
                    InterpretedKind.Test => new AlertChange (AlertChangeKind.Test, null, fresh ? AlertSound.TestCheer : AlertSound.None, origin),
                    InterpretedKind.Clear => Clear (message, fresh, origin),
                    _ => Raise (message, fresh, origin, now),
                };
            }
        }

        /// <summary>The child said "I told a grown-up". Silences a repeated alarm for the silence window; the card stays.</summary>
        /// <returns>The alert after the change, or null when there is no such open alert.</returns>
        public Alert? Acknowledge (string alertId, DateTimeOffset now)
        {
            lock (gate) {
                var index = alerts.FindIndex (a => a.Id == alertId && a.IsOpen);
                if (index < 0)
                    return null;

                // A grown-up's "Got it" outranks the child's tap and is not undone by it.
                if (alerts[index].Status == AlertStatus.Handled)
                    return alerts[index];

                return alerts[index] = alerts[index] with { Status = AlertStatus.Acknowledged, AcknowledgedAt = now };
            }
        }

        /// <summary>A grown-up said "Got it". Silences the alert until its source resolves.</summary>
        /// <returns>The alert after the change, or null when there is no such open alert.</returns>
        public Alert? Handle (string alertId)
        {
            lock (gate) {
                var index = alerts.FindIndex (a => a.Id == alertId && a.IsOpen);
                return index < 0 ? null : alerts[index] = alerts[index] with { Status = AlertStatus.Handled };
            }
        }

        /// <summary>Removes resolved alerts from the Alert Book. Open ones stay: clearing history must not hide something that is still happening.</summary>
        public void ClearHistory ()
        {
            lock (gate)
                alerts.RemoveAll (a => !a.IsOpen);
        }

        /// <summary>Captures everything worth persisting.</summary>
        public AlertStoreState Export ()
        {
            lock (gate)
                return new AlertStoreState (alerts.ToList (), seenOrder.ToList (), LastMessageId);
        }

        /// <summary>Replaces the store's contents with a persisted state, trimming to the configured bounds.</summary>
        public void Restore (AlertStoreState state)
        {
            ArgumentNullException.ThrowIfNull (state);

            lock (gate) {
                alerts.Clear ();
                seenOrder.Clear ();
                seen.Clear ();

                // A file from an older build or edited by hand may hold two open alerts for one source; keep the newest so the
                // "one card per source" rule survives the round trip.
                foreach (var alert in state.Alerts.OrderBy (a => a.Time)) {
                    var duplicate = alerts.FindIndex (a => a.IsOpen && alert.IsOpen && string.Equals (a.Source, alert.Source, StringComparison.OrdinalIgnoreCase));
                    if (duplicate >= 0)
                        alerts[duplicate] = alerts[duplicate] with { Status = AlertStatus.Resolved };
                    alerts.Add (alert);
                }

                foreach (var id in state.SeenIds.TakeLast (options.MaxSeenIds))
                    if (seen.Add (id))
                        seenOrder.Enqueue (id);

                LastMessageId = state.LastMessageId;
                Trim ();
            }
        }

        // ---- the rules ----

        private AlertChange Clear (InterpretedMessage message, bool fresh, MessageOrigin origin)
        {
            var index = OpenIndex (message.Source);
            if (index < 0)
                return new AlertChange (AlertChangeKind.Ignored, null, AlertSound.None, origin);

            // The alert keeps the last warning or alarm reading, which is what the Alert Book should show; only its status and its
            // last-heard time move.
            var resolved = alerts[index] with { Status = AlertStatus.Resolved, UpdatedAt = message.Time };
            alerts[index] = resolved;
            return new AlertChange (AlertChangeKind.Resolved, resolved, fresh ? AlertSound.AllClear : AlertSound.None, origin);
        }

        private AlertChange Raise (InterpretedMessage message, bool fresh, MessageOrigin origin, DateTimeOffset now)
        {
            var index = OpenIndex (message.Source);

            if (index < 0) {
                var created = new Alert (message.MessageId, message.Source, message.Level, message.Title, message.Body,
                    message.Time, message.Time, message.Temperature, AlertStatus.Active);
                alerts.Add (created);
                Trim ();
                return new AlertChange (AlertChangeKind.Raised, created, fresh ? SoundFor (message.Level) : AlertSound.None, origin);
            }

            var existing = alerts[index];
            var refreshed = existing with {
                Title = message.Title,
                Body = message.Body,
                UpdatedAt = message.Time,
                Temperature = message.Temperature,
            };

            if (message.Level > existing.Level) {
                // A warning that becomes an alarm is one card, not two. It is also a NEW emergency: whatever the child or a grown-up
                // said about the warning does not silence the alarm.
                var upgraded = refreshed with { Level = message.Level, Status = AlertStatus.Active, AcknowledgedAt = null };
                alerts[index] = upgraded;
                return new AlertChange (AlertChangeKind.Upgraded, upgraded, fresh ? SoundFor (message.Level) : AlertSound.None, origin);
            }

            if (message.Level < existing.Level) {
                // An alarm easing to a warning lowers the card and makes no noise; the all clear is what ends it.
                var lowered = refreshed with { Level = message.Level };
                alerts[index] = lowered;
                return new AlertChange (AlertChangeKind.Downgraded, lowered, AlertSound.None, origin);
            }

            // The same level again. Only a repeated ALARM can sound, and only if nobody has responded recently: a repeated warning
            // would just be nagging, and a grown-up who said "Got it" has taken over until the all clear.
            var sound = AlertSound.None;
            if (message.Level == AlertLevel.Alarm && fresh && existing.Status != AlertStatus.Handled) {
                var silenced = existing.Status == AlertStatus.Acknowledged
                    && existing.AcknowledgedAt is { } at
                    && now - at < options.SilenceWindow;

                if (!silenced) {
                    sound = AlertSound.Alarm;

                    // The silence window is over and the alarm is still going: the child is asked again, so the takeover returns.
                    if (existing.Status == AlertStatus.Acknowledged)
                        refreshed = refreshed with { Status = AlertStatus.Active, AcknowledgedAt = null };
                }
            }

            alerts[index] = refreshed;
            return new AlertChange (AlertChangeKind.Updated, refreshed, sound, origin);
        }

        private static AlertSound SoundFor (AlertLevel level) => level == AlertLevel.Alarm ? AlertSound.Alarm : AlertSound.Warning;

        private int OpenIndex (string source)
            => alerts.FindIndex (a => a.IsOpen && string.Equals (a.Source, source, StringComparison.OrdinalIgnoreCase));

        private bool Remember (string id)
        {
            if (!seen.Add (id))
                return false;

            seenOrder.Enqueue (id);
            while (seenOrder.Count > options.MaxSeenIds)
                seen.Remove (seenOrder.Dequeue ());

            return true;
        }

        // Keeps the Alert Book bounded. Resolved alerts go first, oldest first; an open alert is never dropped to make room, because
        // dropping it would hide something that is still happening.
        private void Trim ()
        {
            while (alerts.Count > options.MaxAlerts) {
                var index = alerts.FindIndex (a => !a.IsOpen);
                if (index < 0)
                    return;
                alerts.RemoveAt (index);
            }
        }
    }
}
