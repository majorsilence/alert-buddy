using AlertBuddy.Core.Localization;
using Android.Content;
using Android.Content.PM;
using Android.Net;
using Android.OS;
using AlertBuddy.ViewModels.Services;

namespace AlertBuddy.Android.Platform
{
    /// <summary>
    /// What keeps the listener alive with the app closed: the foreground service. <see cref="WhyNot"/> is the honest banner of PLAN.md
    /// section 6.2, in a phrase for Home: it names the first thing that would stop an alarm from being heard.
    /// </summary>
    internal sealed class AndroidBackgroundListener (Context context) : IBackgroundListener
    {
        public bool CanListenInBackground => WhyNot is null;

        public string? WhyNot {
            get {
                if (OperatingSystem.IsAndroidVersionAtLeast (33)
                    && context.CheckSelfPermission (global::Android.Manifest.Permission.PostNotifications) != Permission.Granted)
                    return Loc.T ("Notifications are turned off.");

                var power = (PowerManager?)context.GetSystemService (Context.PowerService);
                if (power is not null && !power.IsIgnoringBatteryOptimizations (context.PackageName))
                    return Loc.T ("Android may stop the app when the screen is off.");

                var connectivity = (ConnectivityManager?)context.GetSystemService (Context.ConnectivityService);
                if (connectivity?.ActiveNetwork is null)
                    return Loc.T ("There is no network.");

                return null;
            }
        }

        public void Start () => ListenerService.Start (context);

        public void Stop () => ListenerService.Stop (context);
    }
}
