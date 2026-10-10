using AlertBuddy.Core.Localization;
using AlertBuddy.Core.Alerts;
using AlertBuddy.ViewModels.Copy;
using AlertBuddy.ViewModels.Services;
using Majorsilence.Forms.Notifications;
using FormsChannel = Majorsilence.Forms.Notifications.NotificationChannel;
using FormsImportance = Majorsilence.Forms.Notifications.NotificationImportance;

namespace AlertBuddy.Android.Platform
{
    /// <summary>
    /// Alert notifications through the framework's <see cref="LocalNotifications"/> (PLAN.md section 6.2). The channels carry no sound of
    /// their own: the cues are played by <see cref="AndroidSoundPlayer"/>, so the alarm can use the alarm stream.
    /// </summary>
    internal sealed class AndroidAlertNotifier : IAlertNotifier
    {
        public const string AlarmChannel = "alarm";
        public const string WarningChannel = "warning";
        public const string CalmChannel = "calm";

        private static bool channelsRegistered;

        public void Show (Alert alert)
        {
            EnsureChannels ();

            var (channel, title) = alert.Status == AlertStatus.Resolved ? (CalmChannel, Loc.T ("All clear"))
                : alert.Level == AlertLevel.Alarm ? (AlarmChannel, Loc.T ("Alarm"))
                : (WarningChannel, Loc.T ("Warning"));

            // Posting again under the same id does not move a notification to another channel on Android (seen on API 36: an ongoing alarm
            // stayed ongoing through its all clear), so the old one is removed first. An upgrade stays on its own channel, so it can update.
            if (alert.Status == AlertStatus.Resolved)
                LocalNotifications.Cancel (IdFor (alert.Id));

            LocalNotifications.Show (IdFor (alert.Id), new LocalNotification {
                ChannelId = channel,
                Title = $"{title}: {alert.Source}",
                Text = alert.Status == AlertStatus.Resolved ? Loc.T ("All clear.") : alert.Level == AlertLevel.Alarm ? Words.TellAGrownUpNow : Loc.T ("Keep an eye on it."),
                // An open alarm stays until it is answered or resolved, and may take over the screen when Android allows it.
                Ongoing = alert.Level == AlertLevel.Alarm && alert.Status == AlertStatus.Active,
                FullScreen = alert.Level == AlertLevel.Alarm && alert.Status == AlertStatus.Active,
            });
        }

        public void Clear (string alertId) => LocalNotifications.Cancel (IdFor (alertId));

        public static void EnsureChannels ()
        {
            if (channelsRegistered)
                return;

            channelsRegistered = true;
            LocalNotifications.RegisterChannel (new FormsChannel (AlarmChannel, Loc.T ("Alarms")) {
                Description = Loc.T ("A temperature alarm. A grown-up needs to know now."),
                Importance = FormsImportance.High,
                Sound = false,
            });
            LocalNotifications.RegisterChannel (new FormsChannel (WarningChannel, Loc.T ("Warnings")) {
                Description = Loc.T ("Something is getting warm."),
                Importance = FormsImportance.High,
                Sound = false,
            });
            LocalNotifications.RegisterChannel (new FormsChannel (CalmChannel, Loc.T ("All clear")) {
                Description = Loc.T ("Things are back to normal."),
                Importance = FormsImportance.Low,
                Sound = false,
            });
        }

        // A stable int per alert, so an update replaces its notification and an all clear can remove it.
        private static int IdFor (string alertId)
        {
            unchecked {
                var hash = 17;
                foreach (var c in alertId)
                    hash = hash * 31 + c;
                return hash & 0x7FFFFFFF;
            }
        }
    }
}
