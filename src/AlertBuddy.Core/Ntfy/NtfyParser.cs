using System.Text.Json;

namespace AlertBuddy.Core.Ntfy
{
    /// <summary>Turns one line of the ntfy JSON stream into an <see cref="NtfyEvent"/>.</summary>
    public static class NtfyParser
    {
        /// <summary>
        /// Parses a line. Returns false for a line that is not valid JSON or is not an event at all; the caller skips it. A stray line
        /// must never end a connection that has been healthy for hours, so this never throws.
        /// </summary>
        public static bool TryParse (string line, out NtfyEvent? result)
        {
            result = null;
            if (string.IsNullOrWhiteSpace (line))
                return false;

            NtfyWireEvent? wire;
            try {
                wire = JsonSerializer.Deserialize (line, NtfyJsonContext.Default.NtfyWireEvent);
            } catch (JsonException) {
                return false;
            }

            if (wire?.Event is null)
                return false;

            switch (wire.Event) {
                case "open":
                    result = new NtfyEvent (NtfyEventKind.Open);
                    return true;
                case "keepalive":
                    result = new NtfyEvent (NtfyEventKind.Keepalive);
                    return true;
                case "message":
                    // A message with no id could not be resumed from or de-duplicated, so it is not one we can use.
                    if (string.IsNullOrEmpty (wire.Id))
                        return false;

                    result = new NtfyEvent (NtfyEventKind.Message, new NtfyMessage (
                        wire.Id,
                        DateTimeOffset.FromUnixTimeSeconds (wire.Time),
                        wire.Topic ?? "",
                        wire.Title,
                        wire.Message ?? "",
                        // ntfy omits the priority when it is the default of 3; out-of-range values are clamped rather than trusted.
                        Math.Clamp (wire.Priority ?? 3, 1, 5),
                        wire.Tags ?? []));
                    return true;
                default:
                    result = new NtfyEvent (NtfyEventKind.Other);
                    return true;
            }
        }
    }
}
