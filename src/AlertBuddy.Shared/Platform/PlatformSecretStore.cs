using AlertBuddy.Core.Settings;
using Majorsilence.Forms.Essentials;

namespace AlertBuddy.Shared.Platform
{
    /// <summary>
    /// The password and token in the platform's own secure store: the Android Keystore, the iOS Keychain, the desktop credential store
    /// (<c>Majorsilence.Forms.Essentials.SecureStorage</c>). Where the platform has none, as on a Linux desktop with no keyring, the
    /// secrets live only for the process's lifetime rather than in a file, so a grown-up re-enters them next launch and nothing is
    /// ever written in the clear.
    /// </summary>
    public sealed class PlatformSecretStore : ISecretStore
    {
        private readonly Dictionary<string, string> cache = [];
        private readonly Func<string, string?> read;
        private readonly Action<string, string> write;
        private readonly Action<string> remove;

        /// <summary>A store over the framework's <see cref="SecureStorage"/>.</summary>
        public PlatformSecretStore ()
            : this (SecureStorage.IsSupported,
                key => SecureStorage.GetAsync (key).GetAwaiter ().GetResult (),
                (key, value) => SecureStorage.SetAsync (key, value).GetAwaiter ().GetResult (),
                SecureStorage.Remove)
        {
        }

        /// <summary>A store over any backing; <paramref name="supported"/> false keeps secrets in memory only.</summary>
        public PlatformSecretStore (bool supported, Func<string, string?> read, Action<string, string> write, Action<string> remove)
        {
            Supported = supported;
            this.read = read;
            this.write = write;
            this.remove = remove;
        }

        /// <summary>A store that never persists, for the browser demo and for tests.</summary>
        public static PlatformSecretStore MemoryOnly () => new (false, _ => null, (_, _) => { }, _ => { });

        /// <summary>Whether secrets persist across launches.</summary>
        public bool Supported { get; }

        /// <inheritdoc/>
        public string? Get (string key)
        {
            lock (cache) {
                if (cache.TryGetValue (key, out var cached))
                    return cached;

                if (!Supported)
                    return null;

                // The store is small and local; a blocking read here keeps ISecretStore synchronous. Cached so the
                // listener's per-connect reads never touch the keystore twice.
                var stored = read (key);
                if (stored != null)
                    cache[key] = stored;
                return stored;
            }
        }

        /// <inheritdoc/>
        public void Set (string key, string value)
        {
            lock (cache) {
                cache[key] = value;
                if (Supported)
                    write (key, value);
            }
        }

        /// <inheritdoc/>
        public void Remove (string key)
        {
            lock (cache) {
                cache.Remove (key);
                if (Supported)
                    remove (key);
            }
        }
    }
}
