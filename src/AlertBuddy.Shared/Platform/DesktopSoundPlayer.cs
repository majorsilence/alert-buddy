using AlertBuddy.ViewModels.Services;

namespace AlertBuddy.Shared.Platform
{
    /// <summary>
    /// Desktop has no sound cues yet: <c>tools/SoundSynth</c> (the generator for the original cues of PLAN.md section 8.8) arrives with
    /// milestone 6, and the framework's own <c>Media.AudioPlayer</c> is real only on Android and iOS (register item F9's own
    /// acceptance criterion). <see cref="IsSupported"/> stays honestly false rather than playing nothing silently.
    /// </summary>
    public sealed class DesktopSoundPlayer : ISoundPlayer
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
}
