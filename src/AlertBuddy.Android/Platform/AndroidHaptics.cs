using AlertBuddy.ViewModels.Services;
using Majorsilence.Forms;

namespace AlertBuddy.Android.Platform
{
    /// <summary>The framework's <see cref="Haptics"/> behind the app's one-method-per-feeling interface.</summary>
    internal sealed class AndroidHaptics : IHaptics
    {
        private static readonly TimeSpan Pulse = TimeSpan.FromMilliseconds (600);
        private CancellationTokenSource? alarm;

        public bool IsSupported => Haptics.IsSupported;

        public void Tap () => Haptics.Tap ();

        // A steady buzz-and-rest for as long as the alarm is open: the framework has one-shot vibration, so the repeat is ours.
        public void Alarm ()
        {
            Stop ();
            var cts = alarm = new CancellationTokenSource ();
            _ = Task.Run (async () => {
                while (!cts.IsCancellationRequested) {
                    Haptics.Vibrate (Pulse);
                    try {
                        await Task.Delay (Pulse * 2, cts.Token).ConfigureAwait (false);
                    } catch (OperationCanceledException) {
                    }
                }
            });
        }

        public void Stop ()
        {
            alarm?.Cancel ();
            alarm = null;
        }
    }
}
