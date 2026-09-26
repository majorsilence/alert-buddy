using AlertBuddy.Core.Alerts;
using AlertBuddy.Core.Ntfy;
using AlertBuddy.Core.Settings;
using AlertBuddy.TestSupport;
using AlertBuddy.ViewModels.Screens;
using Xunit;

namespace AlertBuddy.ViewModels.Tests.Screens
{
    /// <summary>
    /// The milestone 1 done-when: the whole alert lifecycle, driven purely through view models. The real app is built by its composition
    /// root; the only fakes are the platform (sound, notifications) and a scripted server in place of the network, in fake time.
    /// </summary>
    public class WholeAppTests
    {
        private static string Line (AppRig rig, string id, string title, int priority, string body)
            => $$"""{"id":"{{id}}","time":{{rig.Clock.Now.ToUnixTimeSeconds ()}},"event":"message","topic":"home-alerts","priority":{{priority}},"title":"{{title}}","message":"{{body}}"}""";

        [Fact]
        public async Task AWarning_AnAlarm_TheChildsTap_AndTheAllClear_ThroughTheRealListener ()
        {
            await using var rig = new AppRig ();
            rig.Server.EnqueuePoll ();
            var stream = rig.Server.EnqueueStream ();

            rig.App.Start ();
            await stream.Connected;
            await Wait.UntilAsync (() => rig.Main.Mood == BeaconMood.Watching, "the buddy to start watching");
            Assert.StartsWith ("Listening.", rig.Main.ConnectionText);

            // 1. Something is getting warm.
            stream.Send (Line (rig, "w1", "Workshop: temperature warning", 4, "Workshop is at 41.2 °C"));
            await rig.AlertEventsAsync (1);
            Assert.Equal ((BeaconMood.Warning, "The workshop is getting warm."), (rig.Main.Mood, rig.Main.StatusText));
            Assert.Equal (["Play:Warning"], rig.Sound.Calls);

            // 2. It becomes an alarm: one card, and the takeover.
            stream.Send (Line (rig, "a1", "Workshop: temperature alarm", 5, "Workshop is at 50.6 °C"));
            await rig.AlertEventsAsync (2);
            Assert.Single (rig.Main.ActiveAlerts);
            Assert.Equal ("The workshop is too hot.", rig.Current<AlarmViewModel> ().Detail);
            Assert.Equal (["Play:Warning", "Loop:Alarm"], rig.Sound.Calls);

            // 3. The child tells a grown-up: thanks, the siren stops, the card stays.
            rig.Current<AlarmViewModel> ().ToldAGrownUpCommand.Execute (null);
            Assert.Same (rig.Main, rig.Navigator.Current);
            Assert.Equal ((BeaconMood.Reassured, "Thank you. A grown-up is on it."), (rig.Main.Mood, rig.Main.StatusText));
            Assert.Equal ("StopLoop", rig.Sound.Calls[^1]);
            Assert.Single (rig.Main.ActiveAlerts);

            // 4. The server sends the all clear: it chimes, celebrates, then the buddy watches again.
            stream.Send (Line (rig, "c1", "Workshop: temperature alarm (resolved)", 3, "Workshop is at 44.0 °C"));
            await rig.AlertEventsAsync (4);      // the tap in between was the third
            Assert.Equal ((BeaconMood.AllClear, "All clear. The workshop is cool again."), (rig.Main.Mood, rig.Main.StatusText));
            Assert.Equal ("Play:AllClear", rig.Sound.Calls[^1]);
            rig.Clock.Advance (TimeSpan.FromSeconds (4));
            Assert.Equal (BeaconMood.Watching, rig.Main.Mood);

            // 5. And it is all in the Alert Book: one episode, ended.
            var episode = Assert.Single (rig.Hub.Snapshot.History);
            Assert.Equal (("Workshop", AlertStatus.Resolved, AlertLevel.Alarm), (episode.Source, episode.Status, episode.Level));
        }

        [Fact]
        public async Task ADroppedConnection_IsShownHonestly_ThenRecovers ()
        {
            await using var rig = new AppRig ();
            rig.Server.EnqueuePoll ();
            var first = rig.Server.EnqueueStream ();
            var second = rig.Server.EnqueueStream ();
            rig.App.Start ();
            await first.Connected;
            await Wait.UntilAsync (() => rig.Main.Mood == BeaconMood.Watching, "watching");

            first.Fail (new IOException ("connection reset"));
            await Wait.UntilAsync (() => rig.Hub.Snapshot.Connection.State == ConnectionState.Reconnecting, "the drop");

            Assert.Equal ((BeaconMood.Asleep, "Pip can't hear the house right now.", "Can't reach the house. Trying again."),
                (rig.Main.Mood, rig.Main.StatusText, rig.Main.ConnectionText));

            // The one second backoff. Home's 15 second refresh is also waiting on a timer, so say which is meant.
            await rig.Clock.AdvanceToNextAsync (d => d <= TimeSpan.FromSeconds (2));
            await second.Connected;
            await Wait.UntilAsync (() => rig.Main.Mood == BeaconMood.Watching, "the buddy to watch again");
        }

        [Fact]
        public async Task ARefusedSignIn_IsExplainedToTheChild_InTheBuddysVoice ()
        {
            await using var rig = new AppRig ();
            rig.Server.EnqueueStatus (System.Net.HttpStatusCode.Unauthorized);
            rig.App.Start ();

            await Wait.UntilAsync (() => rig.Hub.Snapshot.Connection.State == ConnectionState.AuthFailed, "auth failed");

            Assert.Equal ("The server didn't accept the sign-in. Ask a grown-up to check it in settings.", rig.Main.ConnectionText);
        }

        [Fact]
        public async Task HistoryAtStartup_BuildsTheCards_WithNoSoundAndNoTakeover ()
        {
            await using var rig = new AppRig ();
            rig.Server.EnqueuePoll (
                Line (rig, "h1", "Workshop: temperature warning", 4, "Workshop is at 41 °C"),
                Line (rig, "h2", "Workshop: temperature alarm", 5, "Workshop is at 50 °C"));
            var stream = rig.Server.EnqueueStream ();

            rig.App.Start ();
            await stream.Connected;
            await rig.AlertEventsAsync (2);

            Assert.Empty (rig.Sound.Calls);                       // replayed history never makes a sound
            Assert.Empty (rig.Notifier.Shown);
            Assert.IsType<AlarmViewModel> (rig.Navigator.Current); // but the child is still asked to tell a grown-up
        }

        [Fact]
        public async Task FirstRun_ThroughToTheFirstAlert ()
        {
            await using var rig = new AppRig (new AppSettings ());
            var first = rig.Current<FirstRunViewModel> ();
            first.BuddyName = "Dot";
            await first.NextCommand.ExecuteAsync (null);
            first.Pin = first.PinConfirm = "4821";
            await first.NextCommand.ExecuteAsync (null);
            first.ServerUrl = "https://ntfy.example.com";
            first.Topic = "home-alerts";
            await first.NextCommand.ExecuteAsync (null);
            first.LaterCommand.Execute (null);
            rig.Server.EnqueuePoll ();
            var stream = rig.Server.EnqueueStream ();

            await first.NextCommand.ExecuteAsync (null);          // Done: saves, and the listener starts on what was entered

            await stream.Connected;
            Assert.Same (rig.Main, rig.Navigator.Current);
            await Wait.UntilAsync (() => rig.Main.Mood == BeaconMood.Watching, "the buddy to watch");
            Assert.Equal ("All quiet. Dot is keeping watch.", rig.Main.StatusText);
            stream.Send (Line (rig, "w1", "Workshop: temperature warning", 4, "Workshop is at 41 °C"));
            await rig.AlertEventsAsync (1);
        }

        [Fact]
        public async Task TheBackButton_StepsBackThroughScreens_ThenLetsTheAppLeave ()
        {
            await using var rig = new AppRig ();
            rig.Main.OpenBookCommand.Execute (null);

            Assert.True (rig.Lifecycle.PressBack ());
            Assert.Same (rig.Main, rig.Navigator.Current);

            Assert.False (rig.Lifecycle.PressBack ());            // at Home the platform is left to close the app
        }

        [Fact]
        public async Task DisposingTheApp_StopsListening ()
        {
            var rig = new AppRig ();
            rig.Server.EnqueuePoll ();
            var stream = rig.Server.EnqueueStream ();
            rig.App.Start ();
            await stream.Connected;

            await rig.DisposeAsync ();

            Assert.False (rig.App.Listener.IsRunning);
        }
    }
}
