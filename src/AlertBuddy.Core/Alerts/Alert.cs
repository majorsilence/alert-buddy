using System.Text.Json.Serialization;

namespace AlertBuddy.Core.Alerts
{
    /// <summary>
    /// One episode of something needing a look: a source (a room, a machine) going from a first warning or alarm to its all clear.
    /// Immutable; the store replaces it as it changes.
    /// </summary>
    /// <param name="Id">The id of the message that started the episode. Stable for the episode's life, so a card keeps its identity as it upgrades.</param>
    /// <param name="Source">The thing alerting, taken from the message title. One episode per source is open at a time.</param>
    /// <param name="Level">The current level; it rises on an upgrade and can fall back to a warning.</param>
    /// <param name="Title">The title with any leading emoji removed.</param>
    /// <param name="Body">The latest message text.</param>
    /// <param name="Time">When the episode began.</param>
    /// <param name="UpdatedAt">When the source last sent something for it.</param>
    /// <param name="Temperature">The parsed temperature in degrees Celsius, when the message carried one.</param>
    /// <param name="Status">Where the episode has got to.</param>
    /// <param name="AcknowledgedAt">When the child acknowledged, which starts the silence window for a repeated alarm.</param>
    public sealed record Alert (
        string Id,
        string Source,
        AlertLevel Level,
        string Title,
        string Body,
        DateTimeOffset Time,
        DateTimeOffset UpdatedAt,
        double? Temperature,
        AlertStatus Status,
        DateTimeOffset? AcknowledgedAt = null)
    {
        /// <summary>Whether the episode is still open, that is not resolved.</summary>
        [JsonIgnore]
        public bool IsOpen => Status != AlertStatus.Resolved;
    }
}
