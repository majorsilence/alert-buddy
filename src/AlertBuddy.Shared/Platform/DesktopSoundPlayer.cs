using AlertBuddy.ViewModels.Services;
using Majorsilence.Forms.Media;

namespace AlertBuddy.Shared.Platform
{
    /// <summary>
    /// The cues of <c>tools/SoundSynth</c> on Windows, macOS and Linux, through the framework's <see cref="SoundPlayer"/>, which hands the
    /// file to the operating system's own player (<c>paplay</c> or <c>aplay</c> on Linux, <c>afplay</c> on macOS, <c>System.Media</c> on
    /// Windows). The files sit beside the program, in <c>Assets/sounds</c>.
    /// </summary>
    /// <remarks>
    /// Two limits come from that player. The volume is the system's: the framework's <see cref="SoundPlayer"/> has no volume of its own, so
    /// Practice's "quietly" is not honoured here. And a sound starts after the launch of a small process, a fraction of a second. Neither
    /// matters for an alarm; both are noted in the framework's own description of its audio.
    /// </remarks>
    public sealed class DesktopSoundPlayer : ISoundPlayer, IDisposable
    {
        private readonly object gate = new ();
        private readonly string directory;
        private readonly List<SoundPlayer> playing = [];
        private SoundPlayer? loop;

        /// <summary>Plays from <paramref name="directory"/>, or from <c>Assets/sounds</c> beside the program.</summary>
        public DesktopSoundPlayer (string? directory = null)
            => this.directory = directory ?? Path.Combine (AppContext.BaseDirectory, "Assets", "sounds");

        /// <inheritdoc/>
        public bool IsSupported => Directory.Exists (directory);

        /// <inheritdoc/>
        public void Play (Cue cue, double volume = 1)
        {
            if (PathFor (cue) is not { } path)
                return;

            try {
                var player = new SoundPlayer (path);
                lock (gate) {
                    // A finished sound is let go of; the list only ever holds the few cues still sounding.
                    if (playing.Count > 8) {
                        foreach (var old in playing)
                            old.Dispose ();
                        playing.Clear ();
                    }

                    playing.Add (player);
                }

                player.Play ();
            } catch (Exception) {
                // Never worth a crash: a missing player or file is silence, as it was before.
            }
        }

        /// <inheritdoc/>
        public void StartLoop (Cue cue, double volume = 1)
        {
            StopLoop ();
            if (PathFor (cue) is not { } path)
                return;

            try {
                var player = new SoundPlayer (path);
                lock (gate)
                    loop = player;

                player.PlayLooping ();
            } catch (Exception) {
            }
        }

        /// <inheritdoc/>
        public void StopLoop ()
        {
            SoundPlayer? stopping;
            lock (gate) {
                stopping = loop;
                loop = null;
            }

            try {
                stopping?.Stop ();
                stopping?.Dispose ();
            } catch (Exception) {
            }
        }

        /// <inheritdoc/>
        public void Dispose ()
        {
            StopLoop ();
            lock (gate) {
                foreach (var player in playing)
                    player.Dispose ();
                playing.Clear ();
            }
        }

        private string? PathFor (Cue cue)
        {
            var path = Path.Combine (directory, SoundFiles.FileName (cue));
            return File.Exists (path) ? path : null;
        }
    }
}
