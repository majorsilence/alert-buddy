using System.Text.Json;
using System.Text.Json.Serialization;
using AlertBuddy.Core.Store;

namespace AlertBuddy.Core.Settings
{
    /// <summary>Where <see cref="AppSettings"/> are kept.</summary>
    public interface ISettingsStore
    {
        /// <summary>The saved settings, or the defaults when there are none or they cannot be read.</summary>
        AppSettings Load ();

        /// <summary>Saves the settings, replacing what was there.</summary>
        void Save (AppSettings settings);
    }

    /// <summary>
    /// Where the password and the token live: Android's Keystore-backed storage, the iOS Keychain, the desktop credential store
    /// (PLAN.md section 7.4). Each head supplies one. Nothing in this repo may put a secret in the settings file.
    /// </summary>
    public interface ISecretStore
    {
        /// <summary>The secret stored under a key, or null.</summary>
        string? Get (string key);

        /// <summary>Stores a secret.</summary>
        void Set (string key, string value);

        /// <summary>Removes a secret. Removing one that is not there is not an error.</summary>
        void Remove (string key);
    }

    /// <summary>The keys the app stores secrets under.</summary>
    public static class SecretKeys
    {
        /// <summary>The password for <see cref="AuthMode.Basic"/>.</summary>
        public const string Password = "ntfy.password";

        /// <summary>The token for <see cref="AuthMode.Token"/>.</summary>
        public const string Token = "ntfy.token";
    }

    [JsonSourceGenerationOptions (UseStringEnumConverter = true, WriteIndented = true)]
    [JsonSerializable (typeof (AppSettings))]
    [JsonSerializable (typeof (SettingsBackup.Envelope))]
    internal sealed partial class SettingsJsonContext : JsonSerializerContext
    {
    }

    /// <summary>Settings in a JSON file, written atomically.</summary>
    public sealed class JsonFileSettingsStore (string path) : ISettingsStore
    {
        /// <inheritdoc />
        public AppSettings Load ()
        {
            if (!File.Exists (path))
                return new AppSettings ();

            try {
                return JsonSerializer.Deserialize (File.ReadAllBytes (path), SettingsJsonContext.Default.AppSettings) ?? new AppSettings ();
            } catch (JsonException) {
                // Unreadable settings must not brick the app: set the file aside for a grown-up to look at and start from the defaults.
                AtomicFile.QuarantineCorrupt (path);
                return new AppSettings ();
            } catch (IOException) {
                return new AppSettings ();
            }
        }

        /// <inheritdoc />
        public void Save (AppSettings settings)
        {
            ArgumentNullException.ThrowIfNull (settings);
            AtomicFile.WriteAllBytes (path, JsonSerializer.SerializeToUtf8Bytes (settings, SettingsJsonContext.Default.AppSettings));
        }
    }
}
