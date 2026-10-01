using Android.Content;
using AlertBuddy.ViewModels.Services;
using Majorsilence.Forms.Media;

namespace AlertBuddy.Android.Platform
{
    /// <summary>
    /// The cues of <c>tools/SoundSynth</c>, played through the framework's <see cref="AudioPlayer"/>. The siren is tagged
    /// <see cref="AudioUsage.Alarm"/> so it plays on the alarm stream and is heard even when media volume is low (PLAN.md section 6.2).
    /// </summary>
    internal sealed class AndroidSoundPlayer (Context context) : ISoundPlayer, IDisposable
    {
        private readonly object gate = new ();
        private readonly List<AudioPlayer> playing = [];
        private AudioPlayer? loop;

        public bool IsSupported => AudioPlayer.IsSupported;

        public void Play (Cue cue)
        {
            var player = Create (cue, loop: false);
            lock (gate)
                playing.Add (player);

            player.Completed += (_, _) => {
                lock (gate)
                    playing.Remove (player);
                player.Dispose ();
            };
            player.Play ();
        }

        public void StartLoop (Cue cue)
        {
            lock (gate) {
                loop?.Stop ();
                loop?.Dispose ();
                loop = Create (cue, loop: true);
                loop.Play ();
            }
        }

        public void StopLoop ()
        {
            lock (gate) {
                loop?.Stop ();
                loop?.Dispose ();
                loop = null;
            }
        }

        public void Dispose () => StopLoop ();

        private AudioPlayer Create (Cue cue, bool loop)
        {
            var stream = context.Assets!.Open ($"sounds/{FileFor (cue)}.wav");
            return new AudioPlayer (stream) {
                Loop = loop,
                Usage = cue == Cue.Alarm ? AudioUsage.Alarm : cue is Cue.Warning or Cue.AllClear ? AudioUsage.Notification : AudioUsage.Effect,
            };
        }

        private static string FileFor (Cue cue) => cue switch {
            Cue.Boop => "boop",
            Cue.Warning => "warning",
            Cue.Alarm => "alarm",
            Cue.AllClear => "allclear",
            Cue.Cheer => "cheer",
            _ => "practice",
        };
    }
}
