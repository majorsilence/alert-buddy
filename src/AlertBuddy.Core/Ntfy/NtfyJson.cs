using System.Text.Json.Serialization;

namespace AlertBuddy.Core.Ntfy
{
    /// <summary>The JSON ntfy sends, one object per line. Only the fields the app reads.</summary>
    internal sealed class NtfyWireEvent
    {
        [JsonPropertyName ("id")] public string? Id { get; set; }
        [JsonPropertyName ("time")] public long Time { get; set; }
        [JsonPropertyName ("event")] public string? Event { get; set; }
        [JsonPropertyName ("topic")] public string? Topic { get; set; }
        [JsonPropertyName ("title")] public string? Title { get; set; }
        [JsonPropertyName ("message")] public string? Message { get; set; }
        [JsonPropertyName ("priority")] public int? Priority { get; set; }
        [JsonPropertyName ("tags")] public string[]? Tags { get; set; }
    }

    // Source generation, not reflection: iOS is full AOT and trimmed, and reflection-based System.Text.Json would fail there
    // with no warning at build time (PLAN.md section 2).
    [JsonSourceGenerationOptions (PropertyNameCaseInsensitive = false)]
    [JsonSerializable (typeof (NtfyWireEvent))]
    internal sealed partial class NtfyJsonContext : JsonSerializerContext
    {
    }
}
