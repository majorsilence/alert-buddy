using AlertBuddy.Core.Localization;
using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.OS;
using Android.Runtime;

namespace AlertBuddy.Android
{
    /// <summary>
    /// The foreground service that keeps the ntfy stream open while the app is closed or the screen is off (PLAN.md section 6.2). The
    /// ongoing "listening" notification is the one Android requires of a foreground service; alert notifications are separate and go
    /// through the framework. It owns nothing itself: it only makes sure the shared <see cref="AppHost"/> app exists and is started.
    /// Declared by hand in AndroidManifest.xml, because a foreground service type of special use needs a subtype property that the
    /// [Service] attribute cannot write. The type and why it was chosen are in docs/android-background.md.
    /// </summary>
    [Register ("alertbuddy.ListenerService")]
    public sealed class ListenerService : Service
    {
        private const string ListeningChannel = "listening";
        private const int ListeningId = 1;

        public static void Start (Context context)
        {
            var intent = new Intent (context, typeof (ListenerService));
            if (OperatingSystem.IsAndroidVersionAtLeast (26))
                context.StartForegroundService (intent);
            else
                context.StartService (intent);
        }

        public static void Stop (Context context) => context.StopService (new Intent (context, typeof (ListenerService)));

        public override IBinder? OnBind (Intent? intent) => null;

        [return: GeneratedEnum]
        public override StartCommandResult OnStartCommand (Intent? intent, [GeneratedEnum] StartCommandFlags flags, int startId)
        {
            var notification = BuildNotification ();
            if (OperatingSystem.IsAndroidVersionAtLeast (34))
                StartForeground (ListeningId, notification, ForegroundService.TypeSpecialUse);
            else if (OperatingSystem.IsAndroidVersionAtLeast (29))
                StartForeground (ListeningId, notification, ForegroundService.TypeManifest);
            else
                StartForeground (ListeningId, notification);

            AppHost.Get (this).Start ();

            // If Android stops the service for memory, it starts it again: an alarm listener that quietly stays dead is the worst failure.
            return StartCommandResult.Sticky;
        }

        private Notification BuildNotification ()
        {
            var launch = PackageManager?.GetLaunchIntentForPackage (PackageName!);
            var tap = launch is null ? null : PendingIntent.GetActivity (this, 0, launch, PendingIntentFlags.Immutable | PendingIntentFlags.UpdateCurrent);
            var name = AppHost.Get (this).Settings.Current.BuddyName;
            var text = Loc.F ("{0} is listening.", string.IsNullOrWhiteSpace (name) ? Loc.T ("The buddy") : name);

            if (!OperatingSystem.IsAndroidVersionAtLeast (26)) {
#pragma warning disable CS0618 // The builder without a channel is the only one before API 26.
                return new Notification.Builder (this)
                    .SetContentTitle ("Alert Buddy")!
                    .SetContentText (text)!
                    .SetSmallIcon (global::Android.Resource.Drawable.IcDialogInfo)!
                    .SetOngoing (true)!
                    .SetContentIntent (tap)!
                    .Build ()!;
#pragma warning restore CS0618
            }

            var manager = (NotificationManager)GetSystemService (NotificationService)!;
            manager.CreateNotificationChannel (new NotificationChannel (ListeningChannel, Loc.T ("Listening"), NotificationImportance.Low) {
                Description = Loc.T ("Shows that Alert Buddy is listening for alerts."),
            });

            return new Notification.Builder (this, ListeningChannel)
                .SetContentTitle ("Alert Buddy")!
                .SetContentText (text)!
                .SetSmallIcon (global::Android.Resource.Drawable.IcDialogInfo)!
                .SetOngoing (true)!
                .SetContentIntent (tap)!
                .Build ()!;
        }
    }
}
