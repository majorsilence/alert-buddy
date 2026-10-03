using AlertBuddy.Core.Alerts;
using AlertBuddy.Core.Ntfy;
using AlertBuddy.Core.Settings;
using AlertBuddy.Core.Store;
using AlertBuddy.ViewModels.Screens;
using Xunit;

namespace AlertBuddy.ViewModels.Tests.Screens
{
    public class HomeTests
    {
        [Fact]
        public async Task BeforeTheConnectionIsUp_TheBuddySleeps_AndSaysSo ()
        {
            await using var rig = new AppRig ();

            Assert.Equal ((BeaconMood.Asleep, "Pip can't hear the house right now.", "Connecting to the house."), (rig.Main.Mood, rig.Main.StatusText, rig.Main.ConnectionText));
            Assert.Empty (rig.Main.ActiveAlerts);
        }

        [Fact]
        public async Task OnceLive_TheBuddyWatches ()
        {
            await using var rig = new AppRig ();

            rig.GoLive ();

            Assert.Equal ((BeaconMood.Watching, "All quiet. Pip is keeping watch.", "Listening. Last heard a few seconds ago."), (rig.Main.Mood, rig.Main.StatusText, rig.Main.ConnectionText));
        }

        [Fact]
        public async Task NotSetUp_HomeSaysSo ()
        {
            await using var rig = new AppRig (new AppSettings { FirstRunComplete = true });
            rig.GoLive ();

            Assert.Equal ("Not set up yet. A grown-up can do it in settings.", rig.Main.ConnectionText);
        }

        [Fact]
        public async Task AWarning_ShowsATicket_AndTheBuddySweats ()
        {
            await using var rig = new AppRig ();
            rig.GoLive ();

            rig.Warning ();

            Assert.Equal ((BeaconMood.Warning, "The workshop is getting warm."), (rig.Main.Mood, rig.Main.StatusText));
            var ticket = Assert.Single (rig.Main.ActiveAlerts);
            Assert.Equal (("Workshop", AlertLevel.Warning, "41 degrees. Keep an eye on it.", "a few seconds"), (ticket.Source, ticket.Level, ticket.Sentence, ticket.TimeAgo));
        }

        [Fact]
        public async Task TheTicket_KeepsItsIdentity_AsTheWarningBecomesAnAlarm ()
        {
            await using var rig = new AppRig ();
            rig.GoLive ();
            rig.Warning ();
            var ticket = rig.Main.ActiveAlerts[0];

            rig.Alarm ();

            Assert.Same (ticket, rig.Main.ActiveAlerts[0]);          // one card, updated, so a view can animate it
            Assert.Equal ((AlertLevel.Alarm, "51 degrees. Tell a grown-up."), (ticket.Level, ticket.Sentence));
            Assert.Single (rig.Main.ActiveAlerts);
        }

        [Fact]
        public async Task Tickets_AreNewestFirst ()
        {
            await using var rig = new AppRig ();
            rig.GoLive ();
            rig.Warning ("Workshop");
            rig.Clock.Advance (TimeSpan.FromSeconds (30));
            rig.Warning ("Music studio");

            Assert.Equal (["Music studio", "Workshop"], rig.Main.ActiveAlerts.Select (t => t.Source));
            Assert.Equal ("2 places are getting warm.", rig.Main.StatusText);
        }

        [Fact]
        public async Task TicketsAge_WithoutAMessageHavingToArrive ()
        {
            await using var rig = new AppRig ();
            rig.GoLive ();
            rig.Warning ();

            rig.Clock.Advance (TimeSpan.FromMinutes (1));            // the 15 second refresh timer runs on its own

            Assert.Equal ("1 min", rig.Main.ActiveAlerts[0].TimeAgo);
            Assert.Equal ("Listening. Last heard 1 min ago.", rig.Main.ConnectionText);
        }

        [Fact]
        public async Task TheAllClear_IsCelebratedForFourSeconds_ThenTheBuddyWatchesAgain ()
        {
            await using var rig = new AppRig ();
            rig.GoLive ();
            rig.Warning ();

            rig.AllClear ();
            Assert.Equal ((BeaconMood.AllClear, "All clear. The workshop is cool again."), (rig.Main.Mood, rig.Main.StatusText));
            Assert.Empty (rig.Main.ActiveAlerts);

            rig.Clock.Advance (TimeSpan.FromSeconds (3.9));
            Assert.Equal (BeaconMood.AllClear, rig.Main.Mood);

            rig.Clock.Advance (TimeSpan.FromSeconds (0.2));
            Assert.Equal ((BeaconMood.Watching, "All quiet. Pip is keeping watch."), (rig.Main.Mood, rig.Main.StatusText));
        }

        [Fact]
        public async Task AReplayedAllClear_IsHistory_NotACelebration ()
        {
            await using var rig = new AppRig ();
            rig.GoLive ();
            rig.Send ("Workshop: warning", 4, "41 °C", origin: MessageOrigin.Backlog, age: TimeSpan.FromMinutes (30));

            rig.Send ("Workshop: cleared", 3, "40 °C", origin: MessageOrigin.Backlog, age: TimeSpan.FromMinutes (20));

            Assert.Equal (BeaconMood.Watching, rig.Main.Mood);
        }

        [Fact]
        public async Task AnAllClear_ForSomethingThatWasNotHeat_DoesNotSayCool ()
        {
            await using var rig = new AppRig ();
            rig.GoLive ();
            rig.Warning ("Front door", degrees: null);

            rig.AllClear ("Front door", degrees: null);

            Assert.Equal ("All clear. The front door is fine again.", rig.Main.StatusText);
        }

        [Fact]
        public async Task ANightThatIsQuiet_TheBuddySleeps ()
        {
            await using var rig = new AppRig (new AppSettings { ServerUrl = "https://ntfy.example.com", Topic = "t", FirstRunComplete = true });
            var night = new DateTimeOffset (AppRig.Start.Date, TimeSpan.Zero).AddHours (23);
            rig.Clock.Advance (night - AppRig.Start);

            rig.GoLive ();

            Assert.Equal ((BeaconMood.Asleep, "All quiet. Pip is keeping watch."), (rig.Main.Mood, rig.Main.StatusText));
        }

        [Fact]
        public async Task TheHonestBanner_AppearsWhenTheAppCannotListenInTheBackground ()
        {
            await using var rig = new AppRig ();
            rig.GoLive ();
            Assert.Null (rig.Main.Banner);

            rig.Background.WhyNot = "Notifications are turned off.";
            rig.Clock.Advance (TimeSpan.FromSeconds (15));

            Assert.Equal ("Pip may miss an alert. Notifications are turned off. A grown-up can fix this in settings.", rig.Main.Banner);
        }

        [Fact]
        public async Task ChangingTheBuddysName_ChangesWhatItSays_AtOnce ()
        {
            await using var rig = new AppRig ();
            rig.GoLive ();

            rig.App.Settings.Save (rig.App.Settings.Current with { BuddyName = "Dot" });

            Assert.Equal (("Dot", "All quiet. Dot is keeping watch."), (rig.Main.BuddyName, rig.Main.StatusText));
        }

        [Fact]
        public async Task TheUnencryptedBadge_FollowsTheConnection ()
        {
            await using var rig = new AppRig ();

            rig.Engine.SetConnection (new ConnectionInfo (ConnectionState.Live, Unencrypted: true));

            Assert.True (rig.Main.Unencrypted);
        }

        [Fact]
        public async Task PropertyChanges_AreAnnouncedByName ()
        {
            await using var rig = new AppRig ();
            rig.GoLive ();
            var log = new PropertyLog (rig.Main);

            rig.Warning ();

            Assert.Contains (nameof (MainViewModel.Mood), log.Names);
            Assert.Contains (nameof (MainViewModel.StatusText), log.Names);
        }

        [Fact]
        public async Task ALeftScreen_IsNoLongerUpdated ()
        {
            await using var rig = new AppRig ();
            rig.GoLive ();
            rig.Main.Dispose ();

            rig.Warning ();

            Assert.Equal (BeaconMood.Watching, rig.Main.Mood);       // it heard nothing after it was released
        }

        // ---- the three buttons ----

        [Fact]
        public async Task TheBookAndPracticeButtons_OpenTheirScreens ()
        {
            await using var rig = new AppRig ();

            rig.Main.OpenBookCommand.Execute (null);
            Assert.IsType<AlertBookViewModel> (rig.Navigator.Current);
            rig.Navigator.GoBack ();

            rig.Main.StartPracticeCommand.Execute (null);
            Assert.IsType<PracticeViewModel> (rig.Navigator.Current);
        }

        [Fact]
        public async Task TheGear_OpensTheGate_AndOnlyTheGateOpensSettings ()
        {
            await using var rig = new AppRig ();

            rig.Main.OpenSettingsCommand.Execute (null);

            var gate = rig.Current<GateViewModel> ();                 // not Settings: the gate comes first
            gate.HoldCompletedCommand.Execute (null);                 // no PIN has been set, so the hold alone opens it
            Assert.IsType<SettingsViewModel> (rig.Navigator.Current);
        }
    }

    public class TakeoverTests
    {
        [Fact]
        public async Task AnAlarm_TakesOverTheScreen_WithOneInstruction ()
        {
            await using var rig = new AppRig ();
            rig.GoLive ();

            rig.Alarm ();

            var alarm = rig.Current<AlarmViewModel> ();
            Assert.Equal (("Tell a grown-up now.", "The workshop is too hot.", "I told a grown-up", "Got it"), (alarm.Heading, alarm.Detail, alarm.ToldButtonText, alarm.GotItButtonText));
            Assert.False (alarm.IsAcknowledged);
            Assert.Equal (BeaconMood.Alarm, rig.Main.Mood);
        }

        [Fact]
        public async Task AnAlarmThatIsNotAboutHeat_SaysItNeedsALook_NotThatItIsHot ()
        {
            await using var rig = new AppRig ();
            rig.Alarm ("Front door", degrees: null);

            Assert.Equal ("The front door needs a look.", rig.Current<AlarmViewModel> ().Detail);
        }

        [Fact]
        public async Task AWarning_DoesNotTakeOver ()
        {
            await using var rig = new AppRig ();

            rig.Warning ();

            Assert.Same (rig.Main, rig.Navigator.Current);
        }

        [Fact]
        public async Task TheChildsButton_ThanksThem_AndReturnsToHome_WithTheAlarmStillOpen ()
        {
            await using var rig = new AppRig ();
            rig.GoLive ();
            rig.Alarm ();

            rig.Current<AlarmViewModel> ().ToldAGrownUpCommand.Execute (null);

            Assert.Same (rig.Main, rig.Navigator.Current);
            Assert.Equal ((BeaconMood.Reassured, "Thank you. A grown-up is on it."), (rig.Main.Mood, rig.Main.StatusText));
            var ticket = Assert.Single (rig.Main.ActiveAlerts);            // acknowledging silences sound only: the card stays
            Assert.Equal (AlertStatus.Acknowledged, ticket.Status);
        }

        [Fact]
        public async Task AGrownUpsGotIt_AlsoEndsTheTakeover_AndStaysUntilTheAllClear ()
        {
            await using var rig = new AppRig ();
            rig.GoLive ();
            rig.Alarm ();

            rig.Current<AlarmViewModel> ().GotItCommand.Execute (null);

            Assert.Same (rig.Main, rig.Navigator.Current);
            Assert.Equal (AlertStatus.Handled, Assert.Single (rig.Main.ActiveAlerts).Status);
        }

        [Fact]
        public async Task TheAllClear_EndsATakeoverThatIsStillUp ()
        {
            await using var rig = new AppRig ();
            rig.Alarm ();

            rig.AllClear ();

            Assert.Same (rig.Main, rig.Navigator.Current);
        }

        [Fact]
        public async Task TwoAlarms_TheNewestIsShown_ThenTheNextOnceThatIsAnswered ()
        {
            await using var rig = new AppRig ();
            rig.Alarm ("Workshop");
            rig.Clock.Advance (TimeSpan.FromSeconds (5));
            rig.Alarm ("Music studio");

            var first = rig.Current<AlarmViewModel> ();
            Assert.Equal ("Music studio", first.Alert.Source);

            first.ToldAGrownUpCommand.Execute (null);

            Assert.Equal ("Workshop", rig.Current<AlarmViewModel> ().Alert.Source);       // the other alarm still needs answering
            rig.Current<AlarmViewModel> ().ToldAGrownUpCommand.Execute (null);
            Assert.Same (rig.Main, rig.Navigator.Current);
        }

        [Fact]
        public async Task TheTakeover_CoversWhateverScreenWasOpen_AndReturnsToIt ()
        {
            await using var rig = new AppRig ();
            rig.Main.OpenBookCommand.Execute (null);
            var book = rig.Navigator.Current;

            rig.Alarm ();
            Assert.IsType<AlarmViewModel> (rig.Navigator.Current);
            rig.Current<AlarmViewModel> ().ToldAGrownUpCommand.Execute (null);

            Assert.Same (book, rig.Navigator.Current);       // the child is back where they were
        }

        [Fact]
        public async Task TheBackButton_CannotDismissAnAlarmNobodyHasAnswered ()
        {
            await using var rig = new AppRig ();
            rig.Alarm ();
            var alarm = rig.Current<AlarmViewModel> ();

            var handled = rig.Lifecycle.PressBack ();

            Assert.True (handled);                              // swallowed: the app does not leave either
            Assert.Same (alarm, rig.Navigator.Current);
        }

        [Fact]
        public async Task AnAlarmRestoredFromBeforeARestart_IsShownAtOnce ()
        {
            var alarm = new Alert ("a1", "Workshop", AlertLevel.Alarm, "Workshop: alarm", "50 °C", AppRig.Start, AppRig.Start, 50, AlertStatus.Active);

            await using var rig = new AppRig (beforeCreate: r => r.AlertState.Saved = new AlertStoreState ([alarm], ["a1"], "a1"));

            Assert.Equal ("Workshop", rig.Current<AlarmViewModel> ().Alert.Source);
        }

        [Fact]
        public async Task AnAcknowledgedAlarmRestoredFromBeforeARestart_DoesNotTakeOverAgain ()
        {
            var alarm = new Alert ("a1", "Workshop", AlertLevel.Alarm, "Workshop: alarm", "50 °C", AppRig.Start, AppRig.Start, 50, AlertStatus.Acknowledged, AppRig.Start);

            await using var rig = new AppRig (beforeCreate: r => r.AlertState.Saved = new AlertStoreState ([alarm], ["a1"], "a1"));

            Assert.Same (rig.Main, rig.Navigator.Current);
            Assert.Equal (BeaconMood.Reassured, rig.Main.Mood);
        }

        [Fact]
        public async Task AnAlarmThatIsAnsweredElsewhere_DismissesTheTakeover ()
        {
            await using var rig = new AppRig ();
            var alarm = rig.Alarm ()!.Alert!;
            Assert.IsType<AlarmViewModel> (rig.Navigator.Current);

            rig.Engine.Acknowledge (alarm.Id);                    // answered from somewhere other than this screen

            Assert.Same (rig.Main, rig.Navigator.Current);
        }

        [Fact]
        public async Task TheTakeoverScreen_FollowsTheAlarm_WhileItIsUp ()
        {
            await using var rig = new AppRig ();
            rig.Alarm (degrees: 50.6);
            var screen = rig.Current<AlarmViewModel> ();

            rig.Alarm (degrees: 55.2);                             // the server repeats it, hotter

            Assert.Same (screen, rig.Navigator.Current);          // the same screen, not a second one
            Assert.Equal (55.2, screen.Temperature);
        }

        [Fact]
        public async Task AReplayOfTwoWholeEpisodes_LeavesNothingOpen_OnHome ()
        {
            // Seen on the Android emulator: after connecting to a server whose history held warning, alarm, all clear, twice, Home listed a
            // warning from seven minutes before while its sentence said "All quiet".
            await using var rig = new AppRig ();

            foreach (var minutesAgo in new[] { 20, 10 }) {
                rig.Send ("Workshop: temperature warning", 4, "Workshop is at 41.2 °C", origin: MessageOrigin.Backlog, age: TimeSpan.FromMinutes (minutesAgo));
                rig.Send ("Workshop: temperature alarm", 5, "Workshop is at 50.6 °C", origin: MessageOrigin.Backlog, age: TimeSpan.FromMinutes (minutesAgo) - TimeSpan.FromSeconds (3));
                rig.Send ("Workshop: temperature alarm (resolved)", 3, "Workshop is at 44.0 °C", origin: MessageOrigin.Backlog, age: TimeSpan.FromMinutes (minutesAgo) - TimeSpan.FromSeconds (6));
            }

            Assert.Empty (rig.Hub.Snapshot.Active);
            Assert.Empty (rig.Main.ActiveAlerts);
            Assert.NotEqual (BeaconMood.Warning, rig.Main.Mood);
        }
    }
}
