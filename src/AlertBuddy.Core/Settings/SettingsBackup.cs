using System.Text.Json;
using System.Text.Json.Serialization;

namespace AlertBuddy.Core.Settings
{
    /// <summary>
    /// The settings as a file a person keeps: so a new install, after the app has been removed, can be set up again without typing everything.
    /// The password and token are not in it (they live in the device's secret store and never leave it), so they are entered again; the
    /// PIN is only its salted hash, as in the settings file itself. A file says which app wrote it and in which version of the format, so a
    /// stray JSON file is refused rather than wiping the settings.
    /// </summary>
    public static class SettingsBackup
    {
        private const string AppName = "AlertBuddy";
        private const int Version = 1;

        /// <summary>The name offered when the file is saved.</summary>
        public const string FileName = "alertbuddy-settings.json";

        /// <summary>What goes in the file around the settings.</summary>
        public sealed record Envelope
        {
            /// <summary>The app that wrote it.</summary>
            [JsonPropertyName ("app")]
            public string App { get; init; } = "";

            /// <summary>The version of this format.</summary>
            [JsonPropertyName ("version")]
            public int Version { get; init; }

            /// <summary>The settings.</summary>
            [JsonPropertyName ("settings")]
            public AppSettings? Settings { get; init; }
        }

        /// <summary>The text of the file for <paramref name="settings"/>.</summary>
        public static string Write (AppSettings settings)
        {
            ArgumentNullException.ThrowIfNull (settings);
            return JsonSerializer.Serialize (new Envelope { App = AppName, Version = Version, Settings = settings }, SettingsJsonContext.Default.Envelope);
        }

        /// <summary>
        /// The settings in a file's <paramref name="text"/>, marked as set up (a restored install is not a new one), or null when the text is not
        /// one of this app's files or is from a newer version of it.
        /// </summary>
        public static AppSettings? TryRead (string text)
        {
            if (string.IsNullOrWhiteSpace (text))
                return null;

            try {
                var envelope = JsonSerializer.Deserialize (text, SettingsJsonContext.Default.Envelope);
                if (envelope is not { App: AppName, Settings: { } settings } || envelope.Version is < 1 or > Version)
                    return null;

                return settings with { FirstRunComplete = true };
            } catch (JsonException) {
                return null;
            }
        }
    }
}
