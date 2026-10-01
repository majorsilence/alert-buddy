using Android.App;
using Android.Content;

namespace AlertBuddy.Android
{
    /// <summary>Starts the listener again after the phone restarts, so a reboot does not leave the house unheard (PLAN.md section 6.2).</summary>
    [BroadcastReceiver (Enabled = true, Exported = true)]
    [IntentFilter ([Intent.ActionBootCompleted])]
    public sealed class BootReceiver : BroadcastReceiver
    {
        public override void OnReceive (Context? context, Intent? intent)
        {
            if (context is not null && intent?.Action == Intent.ActionBootCompleted)
                ListenerService.Start (context);
        }
    }
}
