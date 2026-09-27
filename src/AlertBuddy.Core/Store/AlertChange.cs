using AlertBuddy.Core.Alerts;
using AlertBuddy.Core.Ntfy;

namespace AlertBuddy.Core.Store
{
    /// <summary>Which sound a change calls for, before the night policy and the volume settings have their say.</summary>
    public enum AlertSound
    {
        /// <summary>None. Replayed, stale, silenced or simply uneventful.</summary>
        None,

        /// <summary>The friendly two-note cue for a new warning.</summary>
        Warning,

        /// <summary>The siren.</summary>
        Alarm,

        /// <summary>The three ascending notes of an all clear.</summary>
        AllClear,

        /// <summary>The "it works" cheer for a test message.</summary>
        TestCheer,
    }

    /// <summary>What a message did to the store.</summary>
    public enum AlertChangeKind
    {
        /// <summary>A new episode for a source that had none open.</summary>
        Raised,

        /// <summary>A warning became an alarm: one card, not two.</summary>
        Upgraded,

        /// <summary>The same level again, so the card's text and temperature were refreshed.</summary>
        Updated,

        /// <summary>An alarm eased to a warning.</summary>
        Downgraded,

        /// <summary>The source sent an all clear.</summary>
        Resolved,

        /// <summary>A test message: no alert, just a cheer.</summary>
        Test,

        /// <summary>Nothing to do: a calm message for a source with nothing open.</summary>
        Ignored,

        /// <summary>This message id was already processed.</summary>
        Duplicate,

        /// <summary>The child said "I told a grown-up".</summary>
        Acknowledged,

        /// <summary>A grown-up said "Got it".</summary>
        Handled,

        /// <summary>The Alert Book was cleared. Announced so a screen showing it can refresh: the silent state load at startup is not this.</summary>
        HistoryCleared,
    }

    /// <summary>The outcome of one thing happening to the store, published on the hub.</summary>
    /// <param name="Kind">What happened.</param>
    /// <param name="Alert">The alert affected, after the change. Null for a test, an ignored or a duplicate message.</param>
    /// <param name="Sound">Which sound the change calls for.</param>
    /// <param name="Origin">Live news or a replay. A replay's <paramref name="Sound"/> is always none.</param>
    public sealed record AlertChange (AlertChangeKind Kind, Alert? Alert, AlertSound Sound, MessageOrigin Origin);

    /// <summary>Tunable rules of the store. The silence window is a grown-up setting.</summary>
    public sealed record AlertStoreOptions
    {
        /// <summary>
        /// A repeated alarm does not sound again for this long after the child acknowledged (PLAN.md section 4.2). Servers repeat an alarm
        /// for as long as the room stays hot.
        /// </summary>
        public TimeSpan SilenceWindow { get; init; } = TimeSpan.FromMinutes (10);

        /// <summary>
        /// A live message older than this is processed silently. It was missed during a short outage and the state is what matters,
        /// not a siren for something that began a quarter of an hour ago.
        /// </summary>
        public TimeSpan SoundFreshness { get; init; } = TimeSpan.FromMinutes (15);

        /// <summary>The Alert Book keeps at most this many alerts (PLAN.md section 5.3).</summary>
        public int MaxAlerts { get; init; } = 200;

        /// <summary>How many message ids are remembered to drop duplicates.</summary>
        public int MaxSeenIds { get; init; } = 500;
    }

    /// <summary>Everything the store persists: enough to rebuild "what is active right now" after a restart.</summary>
    /// <param name="Alerts">Every alert, oldest first.</param>
    /// <param name="SeenIds">Recently processed message ids, oldest first, so a replay does not process them twice.</param>
    /// <param name="LastMessageId">The newest message id processed, to resume the stream from.</param>
    public sealed record AlertStoreState (IReadOnlyList<Alert> Alerts, IReadOnlyList<string> SeenIds, string? LastMessageId)
    {
        /// <summary>The state of a store that has never seen anything.</summary>
        public static AlertStoreState Empty { get; } = new ([], [], null);
    }
}
