using AlertBuddy.Core.Settings;
using AlertBuddy.ViewModels.Screens;
using Xunit;

namespace AlertBuddy.ViewModels.Tests.Screens
{
    public class PracticeTests
    {
        private static PracticeViewModel Open (AppRig rig)
        {
            rig.Main.StartPracticeCommand.Execute (null);
            return rig.Current<PracticeViewModel> ();
        }

        [Fact]
        public async Task ItIsLabelledAsPractice_AndStartsQuiet ()
        {
            await using var rig = new AppRig ();

            var practice = Open (rig);

            Assert.Equal ("Practice. Nothing is really hot.", practice.Banner);
            Assert.False (practice.IsRunning);
            Assert.Equal (0, practice.Step);
        }

        [Fact]
        public async Task ARun_IsAWarning_AnAlarm_ThenAnAllClear_Over20Seconds ()
        {
            await using var rig = new AppRig ();
            var practice = Open (rig);

            practice.StartCommand.Execute (null);
            rig.Clock.Advance (TimeSpan.Zero);
            Assert.Equal ((1, "A warning arrives.", BeaconMood.Warning, "The practice room is getting warm."), (practice.Step, practice.Caption, practice.Mood, practice.StatusText));

            rig.Clock.Advance (TimeSpan.FromSeconds (7));
            Assert.Equal ((2, "An alarm arrives. Tell a grown-up.", BeaconMood.Alarm, "Tell a grown-up now."), (practice.Step, practice.Caption, practice.Mood, practice.StatusText));
            Assert.True (practice.CanTellAGrownUp);

            rig.Clock.Advance (TimeSpan.FromSeconds (7));
            Assert.Equal ((3, BeaconMood.AllClear, "All clear. The practice room is cool again."), (practice.Step, practice.Mood, practice.StatusText));

            Assert.True (practice.IsRunning);
            rig.Clock.Advance (TimeSpan.FromSeconds (6));
            Assert.Equal ((false, true, BeaconMood.Watching), (practice.IsRunning, practice.IsFinished, practice.Mood));
            Assert.Equal ("All quiet. Pip is keeping watch.", practice.StatusText);
        }

        [Fact]
        public async Task ItTouchesNothingReal ()
        {
            await using var rig = new AppRig ();
            var practice = Open (rig);

            practice.StartCommand.Execute (null);
            rig.Clock.Advance (TimeSpan.FromSeconds (21));

            Assert.Empty (rig.Hub.Snapshot.History);                   // nothing is saved to the Alert Book
            Assert.Equal (0, rig.AlertState.SaveCount);                // nothing was written to disk
            Assert.Empty (rig.Notifier.Shown);                         // no real notification
            Assert.Empty (rig.Haptics.Calls);                          // no vibration
            Assert.Same (practice, rig.Navigator.Current);             // and the real takeover never appeared over it
            Assert.DoesNotContain ("Loop:Alarm", rig.Sound.Calls);     // a rehearsal never runs the siren
        }

        [Fact]
        public async Task ItUsesTheQuietPracticeCue_OnceForEachStep ()
        {
            await using var rig = new AppRig ();
            var practice = Open (rig);

            practice.StartCommand.Execute (null);
            rig.Clock.Advance (TimeSpan.FromSeconds (21));

            Assert.Equal (["Play:Practice", "Play:Practice", "Play:Practice"], rig.Sound.Calls);
        }

        [Theory]
        [InlineData (AlarmTone.Code3, "Code3")]
        [InlineData (AlarmTone.MarchTime, "MarchTime")]
        [InlineData (AlarmTone.Continuous, "Continuous")]
        [InlineData (AlarmTone.VoiceEvacuation, "VoiceEvacuation")]
        public async Task AChosenPracticeTone_PlaysOnceForEachStep_Quietly (AlarmTone tone, string cue)
        {
            await using var rig = new AppRig (new AppSettings { FirstRunComplete = true, PracticeTone = tone });
            var practice = Open (rig);

            practice.StartCommand.Execute (null);
            rig.Clock.Advance (TimeSpan.FromSeconds (21));

            Assert.Equal (Enumerable.Repeat ($"Play:{cue}@0.4", 3), rig.Sound.Calls);
        }

        [Fact]
        public async Task WithSoundsOff_ItIsSilent ()
        {
            await using var rig = new AppRig (new AppSettings { FirstRunComplete = true, SoundsEnabled = false });
            var practice = Open (rig);

            practice.StartCommand.Execute (null);
            rig.Clock.Advance (TimeSpan.FromSeconds (21));

            Assert.Empty (rig.Sound.Calls);
        }

        [Fact]
        public async Task TheChildCanPractiseTheirButton ()
        {
            await using var rig = new AppRig ();
            var practice = Open (rig);
            practice.StartCommand.Execute (null);
            rig.Clock.Advance (TimeSpan.FromSeconds (7));

            practice.ToldAGrownUpCommand.Execute (null);

            Assert.Equal ((BeaconMood.Reassured, "Thank you. A grown-up is on it.", false), (practice.Mood, practice.StatusText, practice.CanTellAGrownUp));
        }

        [Fact]
        public async Task TheButton_IsOnlyAvailableWhileThereIsAPretendAlarm ()
        {
            await using var rig = new AppRig ();
            var practice = Open (rig);

            Assert.False (practice.ToldAGrownUpCommand.CanExecute (null));
            practice.StartCommand.Execute (null);
            rig.Clock.Advance (TimeSpan.Zero);
            Assert.False (practice.ToldAGrownUpCommand.CanExecute (null));     // only a warning so far
            rig.Clock.Advance (TimeSpan.FromSeconds (7));
            Assert.True (practice.ToldAGrownUpCommand.CanExecute (null));
        }

        [Fact]
        public async Task Stopping_CancelsTheRest ()
        {
            await using var rig = new AppRig ();
            var practice = Open (rig);
            practice.StartCommand.Execute (null);
            rig.Clock.Advance (TimeSpan.Zero);

            practice.StopCommand.Execute (null);
            rig.Clock.Advance (TimeSpan.FromSeconds (30));

            Assert.Equal ((false, 0, false), (practice.IsRunning, practice.Step, practice.IsFinished));
            Assert.Equal (["Play:Practice"], rig.Sound.Calls);          // no later steps happened
        }

        [Fact]
        public async Task StartingWhileRunning_IsIgnored ()
        {
            await using var rig = new AppRig ();
            var practice = Open (rig);
            practice.StartCommand.Execute (null);
            rig.Clock.Advance (TimeSpan.Zero);

            practice.StartCommand.Execute (null);
            rig.Clock.Advance (TimeSpan.FromSeconds (21));

            Assert.Equal (3, rig.Sound.Calls.Count);                    // one run, not two
        }

        [Fact]
        public async Task ASecondRun_IsAFreshPretendHouse ()
        {
            await using var rig = new AppRig ();
            var practice = Open (rig);
            practice.StartCommand.Execute (null);
            rig.Clock.Advance (TimeSpan.FromSeconds (21));
            Assert.True (practice.IsFinished);

            practice.StartCommand.Execute (null);

            // The moment the second run starts, before its first step, the house is quiet: nothing is left over from the first run, such as a
            // stale "All clear" celebration.
            Assert.Equal ((BeaconMood.Watching, "All quiet. Pip is keeping watch."), (practice.Mood, practice.StatusText));
            rig.Clock.Advance (TimeSpan.Zero);

            Assert.Equal ((true, false, 1, BeaconMood.Warning), (practice.IsRunning, practice.IsFinished, practice.Step, practice.Mood));
        }

        [Fact]
        public async Task AfterStoppingAndStartingAgain_TheOldRunsTimersDoNotLeakIn ()
        {
            await using var rig = new AppRig ();
            var practice = Open (rig);
            practice.StartCommand.Execute (null);
            rig.Clock.Advance (TimeSpan.Zero);                          // the first run's step 1
            practice.StopCommand.Execute (null);

            practice.StartCommand.Execute (null);
            rig.Clock.Advance (TimeSpan.Zero);                          // the second run's step 1
            rig.Clock.Advance (TimeSpan.FromSeconds (7));               // step 2: once, not once for each run that ever started

            Assert.Equal (["Play:Practice", "Play:Practice", "Play:Practice"], rig.Sound.Calls);
            Assert.Equal (2, practice.Step);
        }

        [Fact]
        public async Task Back_StopsAndReturns ()
        {
            await using var rig = new AppRig ();
            var practice = Open (rig);
            practice.StartCommand.Execute (null);
            rig.Clock.Advance (TimeSpan.Zero);

            practice.BackCommand.Execute (null);
            rig.Clock.Advance (TimeSpan.FromSeconds (30));

            Assert.Same (rig.Main, rig.Navigator.Current);
            Assert.Equal (["Play:Practice"], rig.Sound.Calls);
        }

        [Fact]
        public async Task LeavingTheScreen_CancelsItsTimers ()
        {
            await using var rig = new AppRig ();
            var practice = Open (rig);
            practice.StartCommand.Execute (null);
            rig.Clock.Advance (TimeSpan.Zero);

            rig.Navigator.GoBack ();                                     // released by the navigator
            rig.Clock.Advance (TimeSpan.FromSeconds (30));

            Assert.Equal (["Play:Practice"], rig.Sound.Calls);
        }
    }
}
