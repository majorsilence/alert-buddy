namespace AlertBuddy.Core.Ntfy
{
    /// <summary>What a stream line is (PLAN.md section 5.2). Anything the app does not use is <see cref="Other"/> and ignored.</summary>
    public enum NtfyEventKind
    {
        /// <summary>The stream is open.</summary>
        Open,

        /// <summary>Sent about every 45 seconds so a silent line means a dead connection.</summary>
        Keepalive,

        /// <summary>An alert.</summary>
        Message,

        /// <summary>Any other event, such as a deletion or a poll request.</summary>
        Other,
    }

    /// <summary>Whether a message is news or is being replayed to rebuild state. Replayed messages never make a sound (PLAN.md section 5.3).</summary>
    public enum MessageOrigin
    {
        /// <summary>Arrived on a live connection, or was missed during a short gap.</summary>
        Live,

        /// <summary>Came from a history replay: it builds the Alert Book and the "active right now" set, silently.</summary>
        Backlog,
    }

    /// <summary>A generic ntfy message, the contract of PLAN.md section 5.1.</summary>
    /// <param name="Id">Unique id, used to resume with <c>since=</c> and to drop duplicates.</param>
    /// <param name="Time">When the server accepted it.</param>
    /// <param name="Topic">The topic it was published to.</param>
    /// <param name="Title">The title, if the publisher gave one.</param>
    /// <param name="Message">The body.</param>
    /// <param name="Priority">1 to 5. ntfy omits it when it is 3.</param>
    /// <param name="Tags">Optional hints such as <c>warning</c>.</param>
    public sealed record NtfyMessage (
        string Id,
        DateTimeOffset Time,
        string Topic,
        string? Title,
        string Message,
        int Priority,
        IReadOnlyList<string> Tags);

    /// <summary>One event read from the stream.</summary>
    /// <param name="Kind">What it is.</param>
    /// <param name="Message">Set for <see cref="NtfyEventKind.Message"/>.</param>
    /// <param name="Origin">Live or replayed; set by the subscription, which knows which kind of request it made.</param>
    public sealed record NtfyEvent (NtfyEventKind Kind, NtfyMessage? Message = null, MessageOrigin Origin = MessageOrigin.Live);
}
