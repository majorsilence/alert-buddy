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

        public void Play (Cue cue, double volume = 1)
        {
            var player = Create (cue, loop: false);
            player.Volume = (float)Math.Clamp (volume, 0, 1);
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
                Usage = cue is Cue.Alarm or Cue.Code3 or Cue.MarchTime or Cue.Continuous or Cue.VoiceEvacuation ? AudioUsage.Alarm : cue is Cue.Warning or Cue.AllClear ? AudioUsage.Notification : AudioUsage.Effect,
            };
        }

        private static string FileFor (Cue cue) => cue switch {
            Cue.Boop => "boop",
            Cue.Warning => "warning",
            Cue.Alarm => "alarm",
            Cue.AllClear => "allclear",
            Cue.Cheer => "cheer",
            Cue.Code3 => "code3",
            Cue.MarchTime => "marchtime",
            Cue.Continuous => "continuous",
            Cue.VoiceEvacuation => "voice",
            _ => "practice",
        };
    }
}
