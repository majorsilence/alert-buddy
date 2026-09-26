using System.Text.Json;
using System.Text.Json.Serialization;
using AlertBuddy.Core.Alerts;

namespace AlertBuddy.Core.Store
{
    /// <summary>Where the store's state is kept between runs.</summary>
    public interface IAlertStateStore
    {
        /// <summary>The saved state, or null when there is none or it cannot be read.</summary>
        AlertStoreState? Load ();

        /// <summary>Saves the state, replacing what was there.</summary>
        void Save (AlertStoreState state);
    }

    [JsonSourceGenerationOptions (UseStringEnumConverter = true, WriteIndented = true)]
    [JsonSerializable (typeof (AlertStoreState))]
    internal sealed partial class StoreJsonContext : JsonSerializerContext
    {
    }

    /// <summary>A bounded JSON file with atomic writes. No database in v1 (PLAN.md section 5.3): 200 alerts is a few tens of kilobytes.</summary>
    public sealed class JsonFileAlertStateStore (string path) : IAlertStateStore
    {
        /// <inheritdoc />
        public AlertStoreState? Load ()
        {
            if (!File.Exists (path))
                return null;

            try {
                return JsonSerializer.Deserialize (File.ReadAllBytes (path), StoreJsonContext.Default.AlertStoreState);
            } catch (JsonException) {
                // A file that cannot be read must not stop the app starting. It is set aside and the history starts empty; the server's
                // own cache rebuilds the recent past on the next connect.
                AtomicFile.QuarantineCorrupt (path);
                return null;
            } catch (IOException) {
                return null;
            }
        }

        /// <inheritdoc />
        public void Save (AlertStoreState state)
        {
            ArgumentNullException.ThrowIfNull (state);
            AtomicFile.WriteAllBytes (path, JsonSerializer.SerializeToUtf8Bytes (state, StoreJsonContext.Default.AlertStoreState));
        }
    }
}
