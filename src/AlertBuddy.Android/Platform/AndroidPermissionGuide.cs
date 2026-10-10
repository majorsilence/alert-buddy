using AlertBuddy.Core.Localization;
using Android.App;
using Android.Content;
using Android.OS;
using Android.Provider;
using AlertBuddy.ViewModels.Services;
using Majorsilence.Forms.Notifications;

namespace AlertBuddy.Android.Platform
{
    /// <summary>
    /// The grown-up's onboarding steps of PLAN.md section 6.2, each with its present state read fresh from Android: notifications, the
    /// alarm taking over the screen, the alarm volume, battery optimisation and Do Not Disturb. Most can only be allowed on a system
    /// screen, so <see cref="Open"/> goes there and the answer is read again when the person comes back.
    /// </summary>
    internal sealed class AndroidPermissionGuide (Context context, ISoundPlayer sound) : IPermissionGuide
    {
        private static readonly TimeSpan TestSoundFor = TimeSpan.FromSeconds (2);
        private int notificationAsks;

        public IReadOnlyList<PermissionItem> Items {
            get {
                var items = new List<PermissionItem> {
                    new (PermissionKind.Notifications, Loc.T ("Notifications"),
                        Loc.T ("So an alarm can show up on the screen."), NotificationsAllowed),
                };

                // Before Android 14 the app may take over the screen without being asked.
                if (OperatingSystem.IsAndroidVersionAtLeast (34))
                    items.Add (new (PermissionKind.FullScreenAlarm, Loc.T ("Alarm on the whole screen"),
                        Loc.T ("So an alarm can take over a locked screen. Android asks grown-ups to allow this one."), Manager?.CanUseFullScreenIntent ()));

                items.Add (new (PermissionKind.AlarmVolume, Loc.T ("Alarm volume"),
                    Loc.T ("The alarm has its own volume. Turn it up with the buttons while the test sound plays."), null));

                items.Add (new (PermissionKind.BatteryOptimisation, Loc.T ("Keep listening with the screen off"),
                    Loc.T ("Some phones stop apps they think are idle. Choose Alert Buddy and \"Don't optimise\" or \"Unrestricted\"."), BatteryUnrestricted));

                items.Add (new (PermissionKind.DoNotDisturb, Loc.T ("Ring through Do Not Disturb"),
                    Loc.T ("So an alarm is heard at night, when quiet time is on."), Manager?.IsNotificationPolicyAccessGranted));

                return items;
            }
        }

        public void Open (PermissionKind kind)
        {
            try {
                switch (kind) {
                    case PermissionKind.Notifications:
                        // The first press asks. Once Android has been told no it stops showing the question, so a later press goes to the
                        // app's own notification settings instead.
                        if (notificationAsks++ == 0 && !NotificationsAllowed)
                            LocalNotifications.RequestPermission ();
                        else if (OperatingSystem.IsAndroidVersionAtLeast (26))
                            OpenSettings (Settings.ActionAppNotificationSettings, withPackage: false, extraPackage: true);
                        else
                            OpenSettings (Settings.ActionApplicationDetailsSettings, withPackage: true);
                        break;
                    case PermissionKind.FullScreenAlarm when OperatingSystem.IsAndroidVersionAtLeast (34):
                        OpenSettings (Settings.ActionManageAppUseFullScreenIntent, withPackage: true);
                        break;
                    case PermissionKind.AlarmVolume:
                        PlayTestSound ();
                        break;
                    case PermissionKind.BatteryOptimisation:
                        OpenSettings (Settings.ActionIgnoreBatteryOptimizationSettings, withPackage: false);
                        break;
                    case PermissionKind.DoNotDisturb:
                        OpenSettings (Settings.ActionNotificationPolicyAccessSettings, withPackage: false);
                        break;
                }
            } catch (Exception ex) {
                // A settings screen a vendor has removed must not take the app down; the step simply stays "Not yet".
                global::Android.Util.Log.Warn ("AlertBuddy", $"Opening {kind} failed: {ex.Message}");
            }
        }

        private NotificationManager? Manager => (NotificationManager?)context.GetSystemService (Context.NotificationService);

        private bool NotificationsAllowed
            => !OperatingSystem.IsAndroidVersionAtLeast (33)
               || context.CheckSelfPermission (global::Android.Manifest.Permission.PostNotifications) == global::Android.Content.PM.Permission.Granted;

        private bool BatteryUnrestricted
            => (PowerManager?)context.GetSystemService (Context.PowerService) is { } power && power.IsIgnoringBatteryOptimizations (context.PackageName);

        private void OpenSettings (string action, bool withPackage, bool extraPackage = false)
        {
            var intent = new Intent (action);
            if (withPackage)
                intent.SetData (global::Android.Net.Uri.FromParts ("package", context.PackageName, null));
            if (extraPackage && OperatingSystem.IsAndroidVersionAtLeast (26))
                intent.PutExtra (Settings.ExtraAppPackage, context.PackageName);
            intent.AddFlags (ActivityFlags.NewTask);
            context.StartActivity (intent);
        }

        // The siren is a loop: play it for two seconds so the grown-up can hear it and set the volume, then stop it.
        private void PlayTestSound ()
        {
            sound.StartLoop (Cue.Alarm);
            _ = Task.Delay (TestSoundFor).ContinueWith (_ => sound.StopLoop (), TaskScheduler.Default);
        }
    }
}
