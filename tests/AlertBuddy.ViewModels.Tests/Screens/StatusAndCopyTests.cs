using AlertBuddy.Core.Alerts;
using AlertBuddy.Core.Ntfy;
using AlertBuddy.Core.Store;
using AlertBuddy.ViewModels.Copy;
using AlertBuddy.ViewModels.Screens;
using Xunit;

namespace AlertBuddy.ViewModels.Tests.Screens
{
    public class HomeStatusCalculatorTests
    {
        private static readonly DateTimeOffset T0 = new (2026, 9, 26, 12, 0, 0, TimeSpan.Zero);

        private static Alert A (string source, AlertLevel level, AlertStatus status = AlertStatus.Active, double? temp = 40)
            => new ("id-" + source, source, level, source, "", T0, T0, temp, status);

        private static HubSnapshot Snap (ConnectionState state = ConnectionState.Live, params Alert[] active)
            => new (active, active, new ConnectionInfo (state));

        private static HomeStatus Compute (HubSnapshot s, AllClearInfo? clear = null, bool night = false) => HomeStatusCalculator.Compute (s, "Pip", clear, night);

        // ---- calm ----

        [Fact]
        public void ConnectedAndQuiet_TheBuddyWatches ()
            => Assert.Equal (new HomeStatus (BeaconMood.Watching, "All quiet. Pip is keeping watch."), Compute (Snap ()));

        [Fact]
        public void ConnectedAndQuietAtNight_TheBuddySleeps_ButSaysTheSame ()
            => Assert.Equal (new HomeStatus (BeaconMood.Asleep, "All quiet. Pip is keeping watch."), Compute (Snap (), night: true));

        [Theory]
        [InlineData (ConnectionState.Connecting)]
        [InlineData (ConnectionState.Reconnecting)]
        [InlineData (ConnectionState.Offline)]
        [InlineData (ConnectionState.AuthFailed)]
        [InlineData (ConnectionState.Misconfigured)]
        public void WithoutAConnection_TheBuddySleeps_AndDoesNotClaimAllIsQuiet (ConnectionState state)
            => Assert.Equal (new HomeStatus (BeaconMood.Asleep, "Pip can't hear the house right now."), Compute (Snap (state)));

        // ---- warnings: heat is only claimed when a temperature was read ----

        [Fact]
        public void OneWarmWarning ()
            => Assert.Equal (new HomeStatus (BeaconMood.Warning, "The workshop is getting warm."), Compute (Snap (active: A ("Workshop", AlertLevel.Warning))));

        [Fact]
        public void OneWarning_WithNoTemperature_NeedsALook_NotWarm ()
            => Assert.Equal (new HomeStatus (BeaconMood.Warning, "The front door needs a look."), Compute (Snap (active: A ("Front door", AlertLevel.Warning, temp: null))));

        [Fact]
        public void SeveralWarmWarnings_AreCounted ()
            => Assert.Equal ("2 places are getting warm.", Compute (Snap (active: [A ("Workshop", AlertLevel.Warning), A ("Studio", AlertLevel.Warning)])).Text);

        [Fact]
        public void SeveralWarnings_NotAllHeat_SayNeedALook ()
            => Assert.Equal ("2 places need a look.", Compute (Snap (active: [A ("Workshop", AlertLevel.Warning), A ("Front door", AlertLevel.Warning, temp: null)])).Text);

        // ---- alarms ----

        [Fact]
        public void AnAlarmNobodyHasAnsweredIsTheAlarmMood ()
            => Assert.Equal (new HomeStatus (BeaconMood.Alarm, "Tell a grown-up now."), Compute (Snap (active: A ("Workshop", AlertLevel.Alarm))));

        [Theory]
        [InlineData (AlertStatus.Acknowledged)]
        [InlineData (AlertStatus.Handled)]
        public void AnAnsweredAlarm_IsReassured (AlertStatus status)
            => Assert.Equal (new HomeStatus (BeaconMood.Reassured, "Thank you. A grown-up is on it."), Compute (Snap (active: A ("Workshop", AlertLevel.Alarm, status))));

        [Fact]
        public void IfAnyAlarmIsStillUnanswered_TheBuddyIsStillInAlarm ()
            => Assert.Equal (BeaconMood.Alarm, Compute (Snap (active: [A ("Workshop", AlertLevel.Alarm, AlertStatus.Acknowledged), A ("Studio", AlertLevel.Alarm)])).Mood);

        [Fact]
        public void AnAlarm_OutranksALostConnection_AWarning_AndAnAllClear ()
        {
            // A stale alarm is still worth showing; hiding it because the network dropped would be the wrong way to be wrong.
            var alarm = A ("Workshop", AlertLevel.Alarm);

            Assert.Equal (BeaconMood.Alarm, Compute (Snap (ConnectionState.Offline, alarm)).Mood);
            Assert.Equal (BeaconMood.Alarm, Compute (Snap (ConnectionState.Live, alarm, A ("Studio", AlertLevel.Warning))).Mood);
            Assert.Equal (BeaconMood.Alarm, Compute (Snap (active: alarm), new AllClearInfo ("Studio", true)).Mood);
        }

        // ---- the all clear ----

        [Fact]
        public void AnAllClear_IsCelebrated_WithHeatWordsOnlyForHeat ()
        {
            Assert.Equal (new HomeStatus (BeaconMood.AllClear, "All clear. The workshop is cool again."), Compute (Snap (), new AllClearInfo ("Workshop", true)));
            Assert.Equal (new HomeStatus (BeaconMood.AllClear, "All clear. The front door is fine again."), Compute (Snap (), new AllClearInfo ("Front door", false)));
        }

        [Fact]
        public void AWarning_OutranksTheCelebration ()
            => Assert.Equal (BeaconMood.Warning, Compute (Snap (active: A ("Studio", AlertLevel.Warning)), new AllClearInfo ("Workshop", true)).Mood);

        [Fact]
        public void TheCelebrationLasts4Seconds ()
            => Assert.Equal (TimeSpan.FromSeconds (4), HomeStatusCalculator.AllClearFor);

        // ---- the connection line ----

        [Fact]
        public void TheConnectionLine_SaysNotSetUpBeforeSetup_WhateverTheState ()
            => Assert.Equal ("Not set up yet. A grown-up can do it in settings.", HomeStatusCalculator.ConnectionSentence (new ConnectionInfo (ConnectionState.Live), T0, configured: false));

        [Fact]
        public void TheConnectionLine_ForLive_SaysHowLongAgoItLastHeard ()
        {
            Assert.Equal ("Listening.", HomeStatusCalculator.ConnectionSentence (new ConnectionInfo (ConnectionState.Live), T0, true));
            Assert.Equal ("Listening. Last heard 3 min ago.", HomeStatusCalculator.ConnectionSentence (new ConnectionInfo (ConnectionState.Live, LastHeard: T0), T0.AddMinutes (3), true));
            Assert.Equal ("Listening. Last heard a few seconds ago.", HomeStatusCalculator.ConnectionSentence (new ConnectionInfo (ConnectionState.Live, LastHeard: T0), T0.AddSeconds (10), true));
        }

        [Theory]
        [InlineData (ConnectionState.Connecting, "Connecting to the house.")]
        [InlineData (ConnectionState.Reconnecting, "Can't reach the house. Trying again.")]
        [InlineData (ConnectionState.Offline, "Can't reach the house. Trying again.")]
        [InlineData (ConnectionState.AuthFailed, "The server didn't accept the sign-in. Ask a grown-up to check it in settings.")]
        public void EveryOtherState_HasItsOwnSentence (ConnectionState state, string expected)
            => Assert.Equal (expected, HomeStatusCalculator.ConnectionSentence (new ConnectionInfo (state), T0, true));

        [Theory]
        [InlineData (ConnectionProblem.TopicNotFound, "The server doesn't know this topic. Ask a grown-up to check it in settings.")]
        [InlineData (ConnectionProblem.Certificate, "The secure connection failed. Ask a grown-up to check the server's certificate.")]
        [InlineData (ConnectionProblem.InvalidAddress, "The server address doesn't work. Ask a grown-up to check it in settings.")]
        public void AMisconfiguration_SaysWhichKind (ConnectionProblem problem, string expected)
            => Assert.Equal (expected, HomeStatusCalculator.ConnectionSentence (new ConnectionInfo (ConnectionState.Misconfigured, problem), T0, true));
    }

    public class WordsTests
    {
        [Theory]
        [InlineData ("Workshop", "workshop")]
        [InlineData ("Music studio", "music studio")]
        [InlineData ("NAS", "NAS")]                  // an acronym keeps its capitals
        [InlineData ("TV room", "TV room")]
        [InlineData ("workshop", "workshop")]
        [InlineData ("A", "a")]
        [InlineData ("", "")]
        [InlineData ("3D printer", "3D printer")]     // starts with a digit: nothing to lower
        public void Room_LowersOnlyAProperNounsFirstLetter (string source, string expected)
            => Assert.Equal (expected, Words.Room (source));

        [Theory]
        [InlineData (0, "a few seconds")]
        [InlineData (44, "a few seconds")]
        [InlineData (45, "1 min")]                   // the boundary: 45 seconds is already a minute
        [InlineData (59, "1 min")]
        [InlineData (60, "1 min")]
        [InlineData (180, "3 min")]
        [InlineData (3540, "59 min")]
        [InlineData (7200, "2 h")]
        [InlineData (47 * 3600, "47 h")]
        [InlineData (72 * 3600, "3 days")]
        public void TimeAgo_IsAFewPlainWords (int seconds, string expected)
            => Assert.Equal (expected, Words.TimeAgo (TimeSpan.FromSeconds (seconds)));

        [Theory]
        [InlineData (41.2, "41 degrees")]
        [InlineData (41.6, "42 degrees")]
        [InlineData (-5.4, "-5 degrees")]
        public void Degrees_AreWholeNumbers (double celsius, string expected)
            => Assert.Equal (expected, Words.Degrees (celsius));

        [Fact]
        public void TheVoice_UsesNoExclamationMarks_AndNoAllCaps ()
        {
            // "One exclamation mark at most in the whole app" and no all-caps labels (PLAN.md sections 8.2 and 8.10).
            var sentences = new[] {
                Words.AllQuiet ("Pip"), Words.CannotHear ("Pip"), Words.GettingWarm ("Workshop"), Words.SeveralWarm (2), Words.NeedsALook ("Door"),
                Words.TellAGrownUpNow, Words.TooHot ("Workshop"), Words.ToldAGrownUp, Words.ThankYou, Words.GotIt, Words.AllClear ("Workshop"),
                Words.PracticeBanner, Words.AlertBookEmpty, Words.CannotListenInBackground ("Pip"), Words.SafetyNote, Words.CannotReachTheHouse,
                Words.SignInRefused, Words.TopicNotFound, Words.CertificateProblem, Words.AddressProblem, Words.NotSetUp, Words.GateLocked,
            };

            Assert.All (sentences, s => Assert.DoesNotContain ('!', s));
            Assert.All (sentences, s => Assert.NotEqual (s, s.ToUpperInvariant ()));
        }
    }
}
