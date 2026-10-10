using AlertBuddy.Core.Alerts;
using AlertBuddy.Core.Settings;
using AlertBuddy.Core.Store;
using AlertBuddy.ViewModels.Services;

namespace AlertBuddy.Shared.Platform
{
    /// <summary>
    /// What a head uses for a platform service it does not have yet, so the app runs and says so honestly instead of failing: no sound,
    /// no buzz, no notification. The Android head replaces each with the real thing in milestone 4 (PLAN.md section 12).
    /// </summary>
    public sealed class QuietSoundPlayer : ISoundPlayer
    {
        /// <inheritdoc/>
        public bool IsSupported => false;

        /// <inheritdoc/>
        public void Play (Cue cue, double volume = 1) { }

        /// <inheritdoc/>
        public void StartLoop (Cue cue, double volume = 1) { }

        /// <inheritdoc/>
        public void StopLoop () { }
    }

    /// <summary>No haptics.</summary>
    public sealed class QuietHaptics : IHaptics
    {
        /// <inheritdoc/>
        public bool IsSupported => false;

        /// <inheritdoc/>
        public void Tap () { }

        /// <inheritdoc/>
        public void Alarm () { }

        /// <inheritdoc/>
        public void Stop () { }
    }

    /// <summary>No notifications.</summary>
    public sealed class QuietNotifier : IAlertNotifier
    {
        /// <inheritdoc/>
        public void Show (Alert alert) { }

        /// <inheritdoc/>
        public void Clear (string alertId) { }
    }

    /// <summary>Says plainly that nothing keeps the app listening once it is closed, which Home shows as its honest banner.</summary>
    public sealed class ForegroundOnlyListener (string reason) : IBackgroundListener
    {
        /// <inheritdoc/>
        public bool CanListenInBackground => false;

        /// <inheritdoc/>
        public string? WhyNot => reason;

        /// <inheritdoc/>
        public void Start () { }

        /// <inheritdoc/>
        public void Stop () { }
    }

    /// <summary>Settings that live as long as the process, for the browser demo, which has nothing to persist to.</summary>
    public sealed class MemorySettingsStore (AppSettings initial) : ISettingsStore
    {
        private AppSettings current = initial;

        /// <inheritdoc/>
        public AppSettings Load () => current;

        /// <inheritdoc/>
        public void Save (AppSettings settings) => current = settings;
    }

    /// <summary>Alert state that lives as long as the process.</summary>
    public sealed class MemoryAlertStateStore : IAlertStateStore
    {
        private AlertStoreState? state;

        /// <inheritdoc/>
        public AlertStoreState? Load () => state;

        /// <inheritdoc/>
        public void Save (AlertStoreState value) => state = value;
    }
}
