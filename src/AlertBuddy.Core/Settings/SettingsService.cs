using System.Globalization;
using AlertBuddy.Core.Localization;

namespace AlertBuddy.Core.Settings
{
    /// <summary>
    /// The one place the rest of the app reads settings from and saves them to. It holds the current values in memory, so a screen reading
    /// them never touches the disk, and it tells whoever is interested when they change. Thread-safe: the listener reads on a background
    /// thread while a settings screen saves on the UI thread.
    /// </summary>
    public sealed class SettingsService
    {
        private readonly object gate = new ();
        private readonly ISettingsStore store;
        private AppSettings current;

        /// <summary>Loads the saved settings.</summary>
        public SettingsService (ISettingsStore store)
        {
            this.store = store ?? throw new ArgumentNullException (nameof (store));
            current = store.Load ();
            ApplyLanguage (current);
        }

        // The language is global: every layer asks Loc, so saving a new one is all it takes.
        private static void ApplyLanguage (AppSettings settings) => Loc.Use (settings.Language, CultureInfo.CurrentUICulture.Name);

        /// <summary>The settings as they are now.</summary>
        public AppSettings Current {
            get {
                lock (gate)
                    return current;
            }
        }

        /// <summary>Raised, on the thread that saved, after the settings change.</summary>
        public event Action? Changed;

        /// <summary>Saves the settings and tells the subscribers.</summary>
        public void Save (AppSettings settings)
        {
            ArgumentNullException.ThrowIfNull (settings);

            // Written first: if the disk refuses, nothing in memory has changed and nobody is told that something did.
            store.Save (settings);
            lock (gate)
                current = settings;

            ApplyLanguage (settings);
            Changed?.Invoke ();
        }
    }
}
