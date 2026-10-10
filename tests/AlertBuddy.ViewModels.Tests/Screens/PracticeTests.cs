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
            Assert.DoesNotContain ("Loop:Alarm", rig.Sound.Calls);     // never the real siren at full volume: Practice is quiet
        }

        [Fact]
        public async Task ByDefault_PracticeSoundsLikeARealAlert_TheWarning_TheAlarmTone_ThenTheAllClear_Quietly ()
        {
            await using var rig = new AppRig ();
            var practice = Open (rig);

            practice.StartCommand.Execute (null);
            rig.Clock.Advance (TimeSpan.FromSeconds (21));

            Assert.Equal (["Play:Warning@0.4", "Loop:Alarm@0.4", "StopLoop", "Play:AllClear@0.4"], rig.Sound.Calls);
        }

        [Fact]
        public async Task ByDefault_PracticeFollowsTheChosenAlarmSound ()
        {
            await using var rig = new AppRig (new AppSettings { FirstRunComplete = true, AlarmTone = AlarmTone.Code3 });
            var practice = Open (rig);

            practice.StartCommand.Execute (null);
            rig.Clock.Advance (TimeSpan.FromSeconds (21));

            Assert.Equal (["Play:Warning@0.4", "Loop:Code3@0.4", "StopLoop", "Play:AllClear@0.4"], rig.Sound.Calls);
        }

        [Fact]
        public async Task WhenTheAlarmSoundIsTheVoice_PracticeReadsTheAlertAfterTheTone_WithoutPickingItAgain ()
        {
            await using var rig = new AppRig (new AppSettings { FirstRunComplete = true, AlarmTone = AlarmTone.VoiceEvacuation });
            var practice = Open (rig);

            practice.StartCommand.Execute (null);
            rig.Clock.Advance (TimeSpan.FromSeconds (7));
            rig.Clock.Advance (AlertBuddy.ViewModels.Services.AlertFeedback.AnnounceAfterTone);

            Assert.Equal (["Alert. The practice room is too hot. Tell a grown-up now."], rig.Speaker.Said);
        }

        [Fact]
        public async Task ThePracticeCueCanStillBeChosen_AsTheGentleOne ()
        {
            await using var rig = new AppRig (new AppSettings { FirstRunComplete = true, PracticeGentle = true });
            var practice = Open (rig);

            practice.StartCommand.Execute (null);
            rig.Clock.Advance (TimeSpan.FromSeconds (21));

            Assert.Equal (["Play:Practice", "Loop:Practice@0.4", "StopLoop", "Play:Practice"], rig.Sound.Calls);
        }

        [Theory]
        [InlineData (AlarmTone.Code3, "Code3")]
        [InlineData (AlarmTone.MarchTime, "MarchTime")]
        [InlineData (AlarmTone.Continuous, "Continuous")]
        [InlineData (AlarmTone.VoiceEvacuation, "VoiceEvacuation")]
        public async Task AChosenPracticeTone_PlaysForTheWarning_LoopsThroughTheAlarm_AndEndsAtTheAllClear_Quietly (AlarmTone tone, string cue)
        {
            await using var rig = new AppRig (new AppSettings { FirstRunComplete = true, PracticeTone = tone });
            var practice = Open (rig);

            practice.StartCommand.Execute (null);
            rig.Clock.Advance (TimeSpan.FromSeconds (21));

            // The voice evacuation sound is a chime and then a voice, so the alarm plays the chime once and the voice follows it; the others loop.
            var alarm = tone == AlarmTone.VoiceEvacuation ? $"Play:{cue}@0.4" : $"Loop:{cue}@0.4";
            Assert.Equal ([$"Play:{cue}@0.4", alarm, "StopLoop", $"Play:{cue}@0.4"], rig.Sound.Calls);
        }

        [Theory]
        [InlineData (PracticeSound.Gentle, "Play:Practice")]
        [InlineData (PracticeSound.Whoop, "Play:Alarm@0.4")]
        [InlineData (PracticeSound.Code3, "Play:Code3@0.4")]
        [InlineData (PracticeSound.MarchTime, "Play:MarchTime@0.4")]
        [InlineData (PracticeSound.Continuous, "Play:Continuous@0.4")]
        [InlineData (PracticeSound.VoiceEvacuation, "Play:VoiceEvacuation@0.4")]
        public async Task TheChildCanHearEachSound_OnceAndQuietly (PracticeSound which, string call)
        {
            await using var rig = new AppRig (new AppSettings { FirstRunComplete = true });
            var practice = Open (rig);

            practice.HearSoundCommand.Execute (which);

            Assert.Equal ([call], rig.Sound.Calls);
        }

        [Fact]
        public async Task HearingASound_WithSoundsOff_IsSilent_AndSaysWhy ()
        {
            await using var rig = new AppRig (new AppSettings { FirstRunComplete = true, SoundsEnabled = false });
            var practice = Open (rig);

            practice.HearSoundCommand.Execute (PracticeSound.Whoop);

            Assert.Empty (rig.Sound.Calls);
            Assert.Equal (AlertBuddy.ViewModels.Copy.Words.SoundsAreOff, practice.SoundNote);
        }

        [Fact]
        public async Task HearingASound_IsNotOfferedWhileThePracticeRuns ()
        {
            await using var rig = new AppRig (new AppSettings { FirstRunComplete = true });
            var practice = Open (rig);
            practice.StartCommand.Execute (null);
            rig.Sound.Calls.Clear ();

            Assert.False (practice.HearSoundCommand.CanExecute (PracticeSound.Whoop));
            practice.HearSoundCommand.Execute (PracticeSound.Whoop);

            Assert.Empty (rig.Sound.Calls);
        }

        [Fact]
        public async Task TheAlarmSound_KeepsPlayingUntilTheAllClear ()
        {
            await using var rig = new AppRig (new AppSettings { FirstRunComplete = true, PracticeTone = AlarmTone.Whoop });
            var practice = Open (rig);

            practice.StartCommand.Execute (null);
            rig.Clock.Advance (TimeSpan.FromSeconds (7));
            Assert.Equal (["Play:Alarm@0.4", "Loop:Alarm@0.4"], rig.Sound.Calls);

            rig.Clock.Advance (TimeSpan.FromSeconds (6));                    // still the alarm, 13 seconds in
            Assert.DoesNotContain ("StopLoop", rig.Sound.Calls);

            rig.Clock.Advance (TimeSpan.FromSeconds (1));                    // the all clear arrives
            Assert.Equal ("StopLoop", rig.Sound.Calls[2]);
        }

        [Fact]
        public async Task Stop_SilencesTheAlarmSoundAtOnce ()
        {
            await using var rig = new AppRig (new AppSettings { FirstRunComplete = true, PracticeTone = AlarmTone.Whoop });
            var practice = Open (rig);
            practice.StartCommand.Execute (null);
            rig.Clock.Advance (TimeSpan.FromSeconds (8));

            practice.StopCommand.Execute (null);

            Assert.Equal ("StopLoop", rig.Sound.Calls[^1]);
            rig.Clock.Advance (TimeSpan.FromSeconds (30));
            Assert.Single (rig.Sound.Calls, c => c == "StopLoop");           // and nothing starts it again
        }

        [Fact]
        public async Task TellingAGrownUp_SilencesTheAlarmSound_AsItDoesForARealAlarm ()
        {
            await using var rig = new AppRig (new AppSettings { FirstRunComplete = true, PracticeTone = AlarmTone.Whoop });
            var practice = Open (rig);
            practice.StartCommand.Execute (null);
            rig.Clock.Advance (TimeSpan.FromSeconds (8));

            practice.ToldAGrownUpCommand.Execute (null);

            Assert.Equal ("StopLoop", rig.Sound.Calls[^1]);
        }

        [Fact]
        public async Task LeavingPractice_WhileTheAlarmSounds_SilencesIt ()
        {
            await using var rig = new AppRig (new AppSettings { FirstRunComplete = true, PracticeTone = AlarmTone.Whoop });
            var practice = Open (rig);
            practice.StartCommand.Execute (null);
            rig.Clock.Advance (TimeSpan.FromSeconds (8));

            practice.BackCommand.Execute (null);

            Assert.Equal ("StopLoop", rig.Sound.Calls[^1]);
        }

        [Fact]
        public async Task TheVoiceEvacuationAlarm_SaysWhichPlaceAfterTheTone_UntilTheAllClear ()
        {
            await using var rig = new AppRig (new AppSettings { FirstRunComplete = true, PracticeTone = AlarmTone.VoiceEvacuation, Voice = VoiceType.Deep });
            var practice = Open (rig);

            practice.StartCommand.Execute (null);
            rig.Clock.Advance (TimeSpan.FromSeconds (7));                    // the alarm starts: the chime plays
            Assert.Empty (rig.Speaker.Said);
            rig.Clock.Advance (AlertBuddy.ViewModels.Services.AlertFeedback.AnnounceAfterTone);             // and the voice follows the tone
            Assert.Equal (["Alert. The practice room is too hot. Tell a grown-up now."], rig.Speaker.Said);
            Assert.Equal ((VoiceType.Deep, 1.0), Assert.Single (rig.Speaker.Voices));          // a voice is spoken at full volume: it must be understood

            rig.Clock.Advance (TimeSpan.FromSeconds (6));                    // 14 s: the all clear
            var said = rig.Speaker.Said.Count;
            rig.Clock.Advance (TimeSpan.FromSeconds (60));
            Assert.Equal (said, rig.Speaker.Said.Count);                     // and it stops
        }

        [Fact]
        public async Task HearingTheVoiceSound_OnADeviceWithNoVoice_PlaysTheChime_AndSaysThereIsNoVoice ()
        {
            await using var rig = new AppRig (new AppSettings { FirstRunComplete = true });
            rig.Speaker.IsSupported = false;
            var practice = Open (rig);

            practice.HearSoundCommand.Execute (PracticeSound.VoiceEvacuation);

            Assert.Equal (["Play:VoiceEvacuation@0.4"], rig.Sound.Calls);
            Assert.Equal (AlertBuddy.ViewModels.Copy.Words.NoVoice, practice.SoundNote);
            practice.HearSoundCommand.Execute (PracticeSound.Whoop);                 // another sound clears it
            Assert.Equal ("", practice.SoundNote);
        }

        [Fact]
        public async Task HearingTheVoiceSound_PlaysTheChimeThenTheVoice_Once ()
        {
            await using var rig = new AppRig (new AppSettings { FirstRunComplete = true });
            var practice = Open (rig);

            practice.HearSoundCommand.Execute (PracticeSound.VoiceEvacuation);
            Assert.Equal (["Play:VoiceEvacuation@0.4"], rig.Sound.Calls);
            Assert.Empty (rig.Speaker.Said);
            rig.Clock.Advance (AlertBuddy.ViewModels.Services.AlertFeedback.AnnounceAfterTone);
            rig.Clock.Advance (TimeSpan.FromSeconds (60));

            Assert.Single (rig.Speaker.Said);
        }

        [Fact]
        public async Task WithSoundsOff_TheAlarmDoesNotLoop ()
        {
            await using var rig = new AppRig (new AppSettings { FirstRunComplete = true, SoundsEnabled = false });
            var practice = Open (rig);

            practice.StartCommand.Execute (null);
            rig.Clock.Advance (TimeSpan.FromSeconds (21));

            Assert.DoesNotContain (rig.Sound.Calls, c => c.StartsWith ("Loop"));
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
            Assert.Equal (["Play:Warning@0.4"], rig.Sound.Calls);       // no later steps happened
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

            Assert.Equal (["Play:Warning@0.4", "Loop:Alarm@0.4", "StopLoop", "Play:AllClear@0.4"], rig.Sound.Calls);      // one run, not two
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
            rig.Clock.Advance (TimeSpan.FromSeconds (7));               // step 2: one alarm sound, not one for each run that ever started

            Assert.Equal (["Play:Warning@0.4", "Play:Warning@0.4", "Loop:Alarm@0.4"], rig.Sound.Calls);
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
            Assert.Equal (["Play:Warning@0.4"], rig.Sound.Calls);
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

            Assert.Equal (["Play:Warning@0.4"], rig.Sound.Calls);
        }
    }
}
