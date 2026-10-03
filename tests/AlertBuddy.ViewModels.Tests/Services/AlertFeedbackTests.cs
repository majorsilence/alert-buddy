using AlertBuddy.Core.Ntfy;
using AlertBuddy.Core.Settings;
using AlertBuddy.Core.Store;
using Xunit;

namespace AlertBuddy.ViewModels.Tests.Services
{
    public class AlertFeedbackTests
    {
        private static AppSettings Settings (Action<AppSettings>? _ = null, bool night = false, bool sounds = true)
            => new () {
                ServerUrl = "https://ntfy.example.com", Topic = "home-alerts", FirstRunComplete = true,
                Night = new NightPolicy { Enabled = night },
                SoundsEnabled = sounds,
            };

        [Fact]
        public async Task AWarning_MakesTheFriendlyCue_AndANotification ()
        {
            await using var rig = new AppRig ();

            rig.Warning ();

            Assert.Equal (["Play:Warning"], rig.Sound.Calls);
            Assert.Equal (AlertBuddy.Core.Alerts.AlertLevel.Warning, Assert.Single (rig.Notifier.Shown).Level);
            Assert.Empty (rig.Haptics.Calls);
        }

        private static AppSettings Reading (bool readAloud, bool night = false)
            => Settings (night: night) with { ReadAloud = readAloud };

        [Fact]
        public async Task ReadAloud_SpeaksAWarning_AndTheAlarmInstruction ()
        {
            await using var rig = new AppRig (Reading (true));

            rig.Warning ();
            rig.Alarm ();

            Assert.Equal (2, rig.Speaker.Said.Count);
            Assert.StartsWith ("The ", rig.Speaker.Said[0]);
            Assert.Contains ("needs a look", rig.Speaker.Said[0]);
            Assert.Equal (AlertBuddy.ViewModels.Copy.Words.TellAGrownUpNow, rig.Speaker.Said[1]);
        }

        [Fact]
        public async Task ReadAloud_IsOffByDefault_AndSaysNothing ()
        {
            await using var rig = new AppRig (Reading (false));

            rig.Warning ();
            rig.Alarm ();

            Assert.Empty (rig.Speaker.Said);
        }

        [Fact]
        public async Task ReadAloud_SaysNothingWhenTheNightPolicyHushesAWarning_ButStillSpeaksAnAlarm ()
        {
            await using var rig = new AppRig (Reading (true, night: true));
            rig.Clock.Advance (new DateTimeOffset (AppRig.Start.Date, TimeSpan.Zero).AddHours (22) - AppRig.Start);   // 22:00, inside quiet hours

            rig.Warning ();
            Assert.Empty (rig.Speaker.Said);

            rig.Alarm ();
            Assert.Single (rig.Speaker.Said);
        }

        [Fact]
        public async Task ReadAloud_NeverSpeaksAReplay ()
        {
            await using var rig = new AppRig (Reading (true));

            rig.Send ("Workshop: alarm", 5, "50 °C", origin: MessageOrigin.Backlog);

            Assert.Empty (rig.Speaker.Said);
        }

        [Fact]
        public async Task AnAlarm_StartsTheSiren_AndTheVibration ()
        {
            await using var rig = new AppRig ();

            rig.Alarm ();

            Assert.Equal (["Loop:Alarm"], rig.Sound.Calls);
            Assert.Equal (["Alarm"], rig.Haptics.Calls);
        }

        [Fact]
        public async Task ARepeatedAlarm_DoesNotStartASecondSiren ()
        {
            await using var rig = new AppRig ();
            rig.Alarm ();

            rig.Alarm ();
            rig.Alarm ();

            Assert.Equal (["Loop:Alarm"], rig.Sound.Calls);
        }

        [Fact]
        public async Task TheSiren_StopsTheMomentTheChildTaps ()
        {
            await using var rig = new AppRig ();
            var alarm = rig.Alarm ()!.Alert!;

            rig.Engine.Acknowledge (alarm.Id);

            Assert.Equal (["Loop:Alarm", "StopLoop"], rig.Sound.Calls);
            Assert.Equal (["Alarm", "Stop"], rig.Haptics.Calls);
            Assert.Contains (alarm.Id, rig.Notifier.Cleared);     // the alarm's ongoing notification is removed once answered
        }

        [Fact]
        public async Task TheSiren_StopsWhenAGrownUpSaysGotIt ()
        {
            await using var rig = new AppRig ();
            var alarm = rig.Alarm ()!.Alert!;

            rig.Engine.MarkHandled (alarm.Id);

            Assert.Equal (["Loop:Alarm", "StopLoop"], rig.Sound.Calls);
        }

        [Fact]
        public async Task TheSiren_StopsForTheAllClear_WhichChimes ()
        {
            await using var rig = new AppRig ();
            rig.Alarm ();

            rig.AllClear ();

            Assert.Equal (["Loop:Alarm", "Play:AllClear", "StopLoop"], rig.Sound.Calls);
        }

        [Fact]
        public async Task AnAlarmTheChildAnsweredAndThatRepeatsWithinTheSilenceWindow_DoesNotSoundAgain ()
        {
            await using var rig = new AppRig ();
            var alarm = rig.Alarm ()!.Alert!;
            rig.Engine.Acknowledge (alarm.Id);
            rig.Sound.Calls.Clear ();

            rig.Clock.Advance (TimeSpan.FromMinutes (5));
            rig.Alarm ();

            Assert.Empty (rig.Sound.Calls);
        }

        [Fact]
        public async Task AfterTheSilenceWindow_TheSirenReturns ()
        {
            await using var rig = new AppRig ();
            var alarm = rig.Alarm ()!.Alert!;
            rig.Engine.Acknowledge (alarm.Id);
            rig.Sound.Calls.Clear ();

            rig.Clock.Advance (TimeSpan.FromMinutes (11));
            rig.Alarm ();

            Assert.Equal (["Loop:Alarm"], rig.Sound.Calls);
        }

        [Fact]
        public async Task AnAlarmThatIsReplayedHistory_MakesNoSoundAndNoNotification ()
        {
            await using var rig = new AppRig ();

            rig.Send ("Workshop: alarm", 5, "50 °C", origin: MessageOrigin.Backlog);

            Assert.Empty (rig.Sound.Calls);
            Assert.Empty (rig.Haptics.Calls);
            Assert.Empty (rig.Notifier.Shown);
        }

        [Fact]
        public async Task AnAlarmThatIsAlreadyStale_DoesNotStartASiren ()
        {
            await using var rig = new AppRig ();

            rig.Send ("Workshop: alarm", 5, "50 °C", age: TimeSpan.FromMinutes (20));

            Assert.Empty (rig.Sound.Calls);       // missed during an outage: shown, but not a siren for something 20 minutes old
        }

        [Fact]
        public async Task ATestMessage_Cheers_AndCreatesNoAlert ()
        {
            await using var rig = new AppRig ();

            rig.Send ("Test", 3, "it works");

            Assert.Equal (["Play:Cheer"], rig.Sound.Calls);
            Assert.Empty (rig.Notifier.Shown);
            Assert.Empty (rig.Hub.Snapshot.Active);
        }

        // ---- the night policy and the mute ----

        private static async Task<AppRig> NightRig (bool sounds = true)
        {
            var rig = new AppRig (Settings (night: true, sounds: sounds));
            var night = new DateTimeOffset (AppRig.Start.Date, TimeSpan.Zero).AddHours (22);
            rig.Clock.Advance (night - AppRig.Start);
            await Task.CompletedTask;
            return rig;
        }

        [Fact]
        public async Task AtNight_AWarningIsSilent_ButStillNotified ()
        {
            await using var rig = await NightRig ();

            rig.Warning ();

            Assert.Empty (rig.Sound.Calls);
            Assert.Single (rig.Notifier.Shown);
        }

        [Fact]
        public async Task AtNight_AnAllClearIsSilent ()
        {
            await using var rig = await NightRig ();
            rig.Warning ();

            rig.AllClear ();

            Assert.Empty (rig.Sound.Calls);
        }

        [Fact]
        public async Task AtNight_AnAlarmStillSounds ()
        {
            await using var rig = await NightRig ();

            rig.Alarm ();

            Assert.Equal (["Loop:Alarm"], rig.Sound.Calls);     // alarms are never muted by quiet hours
        }

        [Fact]
        public async Task MutingTheFriendlySounds_NeverMutesAnAlarm ()
        {
            await using var rig = new AppRig (Settings (sounds: false));

            rig.Warning ();
            rig.Send ("Test", 3, "it works");
            Assert.Empty (rig.Sound.Calls);

            rig.Alarm ("Studio");
            Assert.Equal (["Loop:Alarm"], rig.Sound.Calls);
        }

        [Fact]
        public async Task DisposingTheApp_SilencesTheSiren ()
        {
            var rig = new AppRig ();
            rig.Alarm ();

            await rig.DisposeAsync ();

            Assert.Equal (["Loop:Alarm", "StopLoop"], rig.Sound.Calls);
        }

        [Fact]
        public async Task ResolvedAlerts_AreNotifiedCalmly ()
        {
            await using var rig = new AppRig ();
            rig.Warning ();

            rig.AllClear ();

            Assert.Equal (AlertBuddy.Core.Alerts.AlertStatus.Resolved, rig.Notifier.Shown[^1].Status);
        }
    }
}
