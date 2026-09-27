using AlertBuddy.Core.Interpretation;
using AlertBuddy.Core.Ntfy;
using AlertBuddy.Core.Security;
using AlertBuddy.Core.Settings;
using AlertBuddy.Core.Store;
using AlertBuddy.ViewModels.Screens;
using Xunit;

namespace AlertBuddy.ViewModels.Tests.Screens
{
    public class FirstRunTests
    {
        private static async Task FillEverything (FirstRunViewModel vm, AuthMode auth = AuthMode.None)
        {
            vm.BuddyName = "  Dot ";
            await vm.NextCommand.ExecuteAsync (null);
            vm.Pin = "4821";
            vm.PinConfirm = "4821";
            await vm.NextCommand.ExecuteAsync (null);
            vm.ServerUrl = "https://ntfy.example.com/";
            vm.Topic = "home-alerts";
            vm.Auth = auth;
            if (auth != AuthMode.None) {
                vm.Username = "alice";
                vm.Secret = "s3cret";
            }
            await vm.NextCommand.ExecuteAsync (null);
            await vm.NextCommand.ExecuteAsync (null);          // permissions
        }

        [Fact]
        public async Task ItOpensOnTopOfHome_WhenFirstRunHasNotFinished ()
        {
            await using var rig = new AppRig (new AppSettings ());

            var first = rig.Current<FirstRunViewModel> ();

            Assert.Equal ((FirstRunStep.NameBuddy, 1, 5), (first.Step, first.StepNumber, first.StepCount));
            Assert.Equal ("Alert Buddy is a helper. It does not replace smoke or heat alarms.", first.SafetyNote);
        }

        [Fact]
        public async Task TheBuddyNeedsAName_OfUpToSixteenLetters ()
        {
            var vm = new FormRig ().FirstRun ();
            Assert.False (vm.CanGoNext);
            Assert.Equal ("Give your buddy a name, up to 16 letters.", vm.ValidationMessage);

            vm.BuddyName = new string ('a', 17);
            Assert.False (vm.CanGoNext);
            vm.BuddyName = "   ";
            Assert.False (vm.CanGoNext);
            vm.BuddyName = "Pip";
            Assert.True (vm.CanGoNext);
            Assert.Null (vm.ValidationMessage);

            await Task.CompletedTask;
        }

        [Fact]
        public async Task NextIsDisabledUntilTheStepIsValid ()
        {
            var vm = new FormRig ().FirstRun ();
            Assert.False (vm.NextCommand.CanExecute (null));

            vm.BuddyName = "Pip";

            Assert.True (vm.NextCommand.CanExecute (null));
            await Task.CompletedTask;
        }

        [Fact]
        public async Task ThePin_IsFourDigits_TypedTwice ()
        {
            var vm = new FormRig ().FirstRun ();
            vm.BuddyName = "Pip";
            await vm.NextCommand.ExecuteAsync (null);
            Assert.Equal (FirstRunStep.GrownUpGate, vm.Step);

            vm.Pin = "123";
            Assert.Equal ("The PIN is four numbers.", vm.ValidationMessage);
            vm.Pin = "1234";
            vm.PinConfirm = "1235";
            Assert.Equal ("The two PINs don't match.", vm.ValidationMessage);
            vm.PinConfirm = "1234";
            Assert.Null (vm.ValidationMessage);
        }

        [Fact]
        public async Task TheServerStep_SaysWhatIsWrong ()
        {
            var rig = new FormRig ();
            var vm = rig.FirstRun ();
            vm.BuddyName = "Pip";
            await vm.NextCommand.ExecuteAsync (null);
            vm.Pin = vm.PinConfirm = "4821";
            await vm.NextCommand.ExecuteAsync (null);
            Assert.Equal (FirstRunStep.Server, vm.Step);

            Assert.Equal ("Enter the server address, for example https://ntfy.example.com.", vm.ValidationMessage);
            vm.ServerUrl = "http://ntfy.example.com";
            Assert.Contains ("https", vm.ValidationMessage);
            vm.ServerUrl = "https://ntfy.example.com";
            Assert.Equal ("A topic is letters, numbers, - and _, up to 64 characters.", vm.ValidationMessage);
            vm.Topic = "home-alerts";
            Assert.Null (vm.ValidationMessage);

            vm.Auth = AuthMode.Basic;
            Assert.Equal ("Enter the user name.", vm.ValidationMessage);
            vm.Username = "alice";
            Assert.Equal ("Enter the password or token.", vm.ValidationMessage);
            vm.Secret = "s3cret";
            Assert.Null (vm.ValidationMessage);
        }

        [Fact]
        public async Task PlainHttpOnTheHomeNetwork_IsAllowed_AndFlaggedUnencrypted ()
        {
            var vm = new FormRig ().FirstRun ();

            vm.ServerUrl = "http://192.168.1.10:2586";

            Assert.True (vm.ServerIsUnencrypted);
            Assert.Null (vm.ServerProblem);
            vm.ServerUrl = "https://ntfy.example.com";
            Assert.False (vm.ServerIsUnencrypted);
            await Task.CompletedTask;
        }

        [Fact]
        public async Task BackGoesBack_ButNotBeforeTheFirstStep ()
        {
            var vm = new FormRig ().FirstRun ();
            Assert.False (vm.BackCommand.CanExecute (null));
            vm.BuddyName = "Pip";
            await vm.NextCommand.ExecuteAsync (null);

            Assert.True (vm.BackCommand.CanExecute (null));
            vm.BackCommand.Execute (null);

            Assert.Equal (FirstRunStep.NameBuddy, vm.Step);
        }

        [Fact]
        public async Task TestingTheConnection_ReportsTheResult_AndUsesWhatWasTyped ()
        {
            var rig = new FormRig ();
            var vm = rig.FirstRun ();
            vm.ServerUrl = "https://ntfy.example.com";
            vm.Topic = "home-alerts";
            vm.Auth = AuthMode.Basic;
            vm.Username = "alice";
            vm.Secret = "s3cret";
            rig.Tester.Gate = new TaskCompletionSource ();

            var running = vm.TestConnectionCommand.ExecuteAsync (null);
            Assert.True (vm.IsTesting);
            Assert.Equal ("Testing the connection.", vm.TestResult);
            Assert.False (vm.TestConnectionCommand.CanExecute (null));         // not twice at once

            rig.Tester.Gate.SetResult ();
            await running;

            Assert.False (vm.IsTesting);
            Assert.Equal ("Connected. Fine.", vm.TestResult);
            Assert.Equal (("https://ntfy.example.com", "home-alerts", "Basic YWxpY2U6czNjcmV0"), Assert.Single (rig.Tester.Calls));
        }

        [Fact]
        public async Task TestingIsUnavailable_UntilTheAddressAndTopicAreUsable ()
        {
            var vm = new FormRig ().FirstRun ();
            Assert.False (vm.TestConnectionCommand.CanExecute (null));

            vm.ServerUrl = "https://ntfy.example.com";
            Assert.False (vm.TestConnectionCommand.CanExecute (null));
            vm.Topic = "home-alerts";

            Assert.True (vm.TestConnectionCommand.CanExecute (null));
            await Task.CompletedTask;
        }

        [Fact]
        public async Task ALaterOnPermissions_SkipsToThePracticeStep_AndTheProblemIsShown ()
        {
            var rig = new FormRig ();
            rig.Background.WhyNot = "notifications are turned off";
            var vm = rig.FirstRun ();
            vm.BuddyName = "Pip";
            await vm.NextCommand.ExecuteAsync (null);
            vm.Pin = vm.PinConfirm = "4821";
            await vm.NextCommand.ExecuteAsync (null);
            vm.ServerUrl = "https://ntfy.example.com";
            vm.Topic = "home-alerts";
            await vm.NextCommand.ExecuteAsync (null);
            Assert.Equal (FirstRunStep.Permissions, vm.Step);
            Assert.Equal ("notifications are turned off", vm.PermissionsProblem);

            vm.LaterCommand.Execute (null);

            Assert.Equal (FirstRunStep.Practice, vm.Step);
        }

        [Fact]
        public async Task Finishing_SavesEverything_HashesThePin_AndKeepsTheSecretOutOfTheSettings ()
        {
            var rig = new FormRig ();
            var vm = rig.FirstRun ();
            await FillEverything (vm, AuthMode.Basic);

            await vm.NextCommand.ExecuteAsync (null);         // the last step: Done

            var saved = rig.Store.Current;
            Assert.Equal (("Dot", "https://ntfy.example.com", "home-alerts", AuthMode.Basic, "alice", true),
                (saved.BuddyName, saved.ServerUrl, saved.Topic, saved.Auth, saved.Username, saved.FirstRunComplete));
            Assert.True (PinHasher.Verify ("4821", saved.Pin));
            Assert.Equal ("s3cret", rig.Secrets.Get (SecretKeys.Password));
            Assert.Null (rig.Secrets.Get (SecretKeys.Token));
            Assert.DoesNotContain ("s3cret", System.Text.Json.JsonSerializer.Serialize (saved.Username + saved.ServerUrl + saved.Topic));
        }

        [Fact]
        public async Task Finishing_RestartsTheListenerOnce_ClearsWhatWasTyped_AndGoesHome ()
        {
            var rig = new FormRig ();
            var vm = rig.FirstRun ();
            await FillEverything (vm, AuthMode.Token);

            await vm.NextCommand.ExecuteAsync (null);

            Assert.Equal (1, rig.Listener.Restarts);
            Assert.Equal (("", "", ""), (vm.Pin, vm.PinConfirm, vm.Secret));    // the secrets are not left sitting in the screen
            Assert.Same (rig.Root, rig.Navigator.Current);
            Assert.Equal ("s3cret", rig.Secrets.Get (SecretKeys.Token));
            Assert.Null (rig.Secrets.Get (SecretKeys.Password));                 // only the mode in use is kept
        }

        [Fact]
        public async Task WithNoSignIn_NoSecretIsStored ()
        {
            var rig = new FormRig ();
            var vm = rig.FirstRun ();
            await FillEverything (vm, AuthMode.None);

            await vm.NextCommand.ExecuteAsync (null);

            Assert.Empty (rig.Secrets.Keys);
            Assert.Equal ("", rig.Store.Current.Username);
        }

        [Fact]
        public async Task ThePracticeStep_OffersARehearsal ()
        {
            await using var rig = new AppRig (new AppSettings ());
            var first = rig.Current<FirstRunViewModel> ();

            first.TryPracticeCommand.Execute (null);

            Assert.IsType<PracticeViewModel> (rig.Navigator.Current);
        }
    }

    public class SettingsScreenTests
    {
        private static AppSettings Configured () => new () {
            ServerUrl = "https://ntfy.example.com", Topic = "home-alerts", Auth = AuthMode.Basic, Username = "alice",
            BuddyName = "Pip", FirstRunComplete = true, Pin = PinHasher.Create ("4821"),
        };

        private static (FormRig Rig, SettingsViewModel Vm) Open (AppSettings? s = null, string? password = "stored-secret")
        {
            var rig = new FormRig (s ?? Configured ());
            if (password is not null)
                rig.Secrets.Set (SecretKeys.Password, password);
            return (rig, rig.SettingsScreen ());
        }

        [Fact]
        public void ItLoadsTheCurrentSettings_ButNeverShowsAStoredSecret ()
        {
            var (_, vm) = Open ();

            Assert.Equal (("https://ntfy.example.com", "home-alerts", AuthMode.Basic, "alice", "Pip"), (vm.ServerUrl, vm.Topic, vm.Auth, vm.Username, vm.BuddyName));
            Assert.True (vm.HasStoredSecret);
            Assert.Equal ("", vm.Secret);                                        // the password is not echoed back into a field
            Assert.Equal ((10, true, new TimeOnly (20, 0), new TimeOnly (7, 0), MotionPreference.System), (vm.SilenceMinutes, vm.NightEnabled, vm.NightStart, vm.NightEnd, vm.Motion));
            Assert.Equal ("1.2.3", vm.Version);
        }

        [Fact]
        public void AboutSaysWhatItIs_AndWhatItIsNot ()
        {
            var (_, vm) = Open ();

            Assert.Equal ("Alert Buddy is a helper. It does not replace smoke or heat alarms.", vm.SafetyNote);
            Assert.Contains ("no tracking", vm.Privacy);
            Assert.Contains ("stays on this device", vm.Privacy);
        }

        [Fact]
        public void Validation_SaysWhatIsWrong_AndSaveWaits ()
        {
            var (_, vm) = Open ();
            Assert.True (vm.SaveCommand.CanExecute (null));

            vm.ServerUrl = "http://ntfy.example.com";
            Assert.Contains ("https", vm.ServerProblem);
            Assert.False (vm.SaveCommand.CanExecute (null));
            vm.ServerUrl = "https://ntfy.example.com";

            vm.Topic = "no spaces";
            Assert.Equal ("A topic is letters, numbers, - and _, up to 64 characters.", vm.TopicProblem);
            Assert.False (vm.SaveCommand.CanExecute (null));
            vm.Topic = "home-alerts";

            vm.Username = "";
            Assert.Equal ("Enter the user name.", vm.SignInProblem);
            Assert.False (vm.SaveCommand.CanExecute (null));
            vm.Username = "alice";

            vm.BuddyName = "";
            Assert.False (vm.SaveCommand.CanExecute (null));
            vm.BuddyName = "Pip";
            Assert.True (vm.SaveCommand.CanExecute (null));
        }

        [Fact]
        public void ASignIn_NeedsASecret_UnlessOneIsAlreadyStored ()
        {
            var (_, withStored) = Open ();
            Assert.Null (withStored.SignInProblem);

            var (_, without) = Open (password: null);
            Assert.Equal ("Enter the password or token.", without.SignInProblem);
            without.Secret = "typed";
            Assert.Null (without.SignInProblem);
        }

        [Fact]
        public async Task Saving_AppliesTheEdits_NormalisesTheAddress_AndSaysSo ()
        {
            var (rig, vm) = Open ();
            vm.ServerUrl = "https://ntfy.example.com/ntfy/";
            vm.BuddyName = "  Dot ";
            vm.Username = " alice ";
            vm.SoundsEnabled = false;
            vm.SilenceMinutes = 3;
            vm.Look = LookPreference.Night;
            vm.Motion = MotionPreference.Reduce;
            vm.NightEnabled = false;

            await vm.SaveCommand.ExecuteAsync (null);

            var s = rig.Store.Current;
            Assert.Equal (("https://ntfy.example.com/ntfy", "Dot", "alice", false, TimeSpan.FromMinutes (3), LookPreference.Night, true, false),
                (s.ServerUrl, s.BuddyName, s.Username, s.SoundsEnabled, s.SilenceWindow, s.Look, s.ReduceMotion, s.Night.Enabled));
            Assert.Equal ("Saved.", vm.SavedMessage);
        }

        [Fact]
        public async Task EditingAfterASave_ClearsTheSavedMessage ()
        {
            var (_, vm) = Open ();
            await vm.SaveCommand.ExecuteAsync (null);
            Assert.Equal ("Saved.", vm.SavedMessage);

            vm.BuddyName = "Dot";

            Assert.Null (vm.SavedMessage);
        }

        [Theory]
        [InlineData (MotionPreference.System, null)]
        [InlineData (MotionPreference.Reduce, true)]
        [InlineData (MotionPreference.Full, false)]
        public async Task ReducedMotion_CanBeForcedOrFollowTheSystem (MotionPreference choice, bool? expected)
        {
            var (rig, vm) = Open ();
            vm.Motion = choice;

            await vm.SaveCommand.ExecuteAsync (null);

            Assert.Equal (expected, rig.Store.Current.ReduceMotion);
        }

        [Theory]
        [InlineData (0, 1)]
        [InlineData (-5, 1)]
        [InlineData (1000, 240)]
        [InlineData (15, 15)]
        public async Task TheSilenceWindow_IsKeptSensible (int typed, int saved)
        {
            var (rig, vm) = Open ();
            vm.SilenceMinutes = typed;

            await vm.SaveCommand.ExecuteAsync (null);

            Assert.Equal (TimeSpan.FromMinutes (saved), rig.Store.Current.SilenceWindow);
        }

        [Fact]
        public async Task ABlankSecretField_LeavesTheStoredPasswordAlone_AndDoesNotRestartTheListener ()
        {
            var (rig, vm) = Open ();
            vm.BuddyName = "Dot";                                  // nothing that changes the connection

            await vm.SaveCommand.ExecuteAsync (null);

            Assert.Equal ("stored-secret", rig.Secrets.Get (SecretKeys.Password));
            Assert.Equal (0, rig.Listener.Restarts);
        }

        [Fact]
        public async Task ATypedSecret_ReplacesTheStoredOne_ClearsTheField_AndRestartsTheListener ()
        {
            var (rig, vm) = Open ();
            vm.Secret = "new-secret";

            await vm.SaveCommand.ExecuteAsync (null);

            Assert.Equal ("new-secret", rig.Secrets.Get (SecretKeys.Password));
            Assert.Equal ("", vm.Secret);
            Assert.Equal (1, rig.Listener.Restarts);
            Assert.DoesNotContain ("new-secret", rig.Store.Current.ToString ());
        }

        [Theory]
        [InlineData ("ServerUrl")]
        [InlineData ("Topic")]
        [InlineData ("Auth")]
        [InlineData ("Username")]
        public async Task ChangingWhatTheConnectionUses_RestartsTheListenerOnce (string field)
        {
            var (rig, vm) = Open ();
            switch (field) {
                case "ServerUrl": vm.ServerUrl = "https://other.example.com"; break;
                case "Topic": vm.Topic = "other-topic"; break;
                case "Auth": vm.Auth = AuthMode.None; break;
                case "Username": vm.Username = "bob"; break;
            }

            await vm.SaveCommand.ExecuteAsync (null);

            Assert.Equal (1, rig.Listener.Restarts);
        }

        [Fact]
        public async Task SwitchingTheSignInMode_DropsTheSecretForTheModeLeft ()
        {
            var (rig, vm) = Open ();
            vm.Auth = AuthMode.Token;
            vm.Secret = "tk_token";

            await vm.SaveCommand.ExecuteAsync (null);

            Assert.Null (rig.Secrets.Get (SecretKeys.Password));
            Assert.Equal ("tk_token", rig.Secrets.Get (SecretKeys.Token));
            Assert.True (vm.HasStoredSecret);
        }

        [Fact]
        public async Task SigningInWithNothing_RemovesBothSecrets ()
        {
            var (rig, vm) = Open ();
            vm.Auth = AuthMode.None;

            await vm.SaveCommand.ExecuteAsync (null);

            Assert.Empty (rig.Secrets.Keys);
            Assert.Equal ("", rig.Store.Current.Username);
        }

        [Fact]
        public async Task ANewPin_IsHashed_AndABlankOneKeepsTheOldPin ()
        {
            var (rig, vm) = Open ();
            await vm.SaveCommand.ExecuteAsync (null);
            Assert.True (PinHasher.Verify ("4821", rig.Store.Current.Pin));           // untouched

            vm.NewPin = "9999";
            vm.NewPinConfirm = "9999";
            await vm.SaveCommand.ExecuteAsync (null);

            Assert.True (PinHasher.Verify ("9999", rig.Store.Current.Pin));
            Assert.False (PinHasher.Verify ("4821", rig.Store.Current.Pin));
            Assert.Equal (("", ""), (vm.NewPin, vm.NewPinConfirm));
        }

        [Fact]
        public void AMismatchedNewPin_BlocksSaving ()
        {
            var (_, vm) = Open ();

            vm.NewPin = "9999";
            vm.NewPinConfirm = "9998";

            Assert.Equal ("The two PINs don't match.", vm.PinProblem);
            Assert.False (vm.SaveCommand.CanExecute (null));
        }

        [Fact]
        public async Task SavingInterpretation_ChangesHowTheNextMessageIsRead ()
        {
            var (rig, vm) = Open ();
            vm.SourceSeparator = " is ";
            vm.AlarmPriority = 4;

            await vm.SaveCommand.ExecuteAsync (null);
            var change = rig.Engine.Handle (new NtfyEvent (NtfyEventKind.Message,
                new NtfyMessage ("z1", rig.Clock.Now, "t", "Cellar is warm", "", 4, []), MessageOrigin.Live));

            Assert.Equal (("Cellar", AlertBuddy.Core.Alerts.AlertLevel.Alarm), (change!.Alert!.Source, change.Alert.Level));
        }

        [Fact]
        public void AnInvalidPattern_IsReported_ButDoesNotBlockSaving ()
        {
            var (_, vm) = Open ();

            vm.TemperaturePattern = "(unclosed";

            Assert.Single (vm.InterpretationProblems);
            Assert.True (vm.SaveCommand.CanExecute (null));           // the interpreter falls back to the default, so it is safe to save
        }

        [Fact]
        public void ResetInterpretation_RestoresTheDefaults ()
        {
            var (_, vm) = Open ();
            vm.SourceSeparator = " - ";
            vm.AlarmPriority = 3;
            vm.StripLeadingEmoji = false;
            vm.TemperaturePattern = "x";

            vm.ResetInterpretationCommand.Execute (null);

            Assert.Equal ((": ", 5, 4, true, InterpretationSettings.DefaultTemperaturePattern), (vm.SourceSeparator, vm.AlarmPriority, vm.WarningPriority, vm.StripLeadingEmoji, vm.TemperaturePattern));
            Assert.Empty (vm.InterpretationProblems);
        }

        [Fact]
        public async Task TestingUsesWhatIsOnScreen_AndTheStoredSecretWhenNoneWasTyped ()
        {
            var (rig, vm) = Open ();
            vm.Topic = "other-topic";

            await vm.TestConnectionCommand.ExecuteAsync (null);

            var call = Assert.Single (rig.Tester.Calls);
            Assert.Equal (("https://ntfy.example.com", "other-topic", "Basic " + Convert.ToBase64String ("alice:stored-secret"u8.ToArray ())), call);
            Assert.Equal ("Connected. Fine.", vm.TestResult);
        }

        [Fact]
        public async Task TestingUsesATypedSecretInPreferenceToTheStoredOne ()
        {
            var (rig, vm) = Open ();
            vm.Secret = "typed";

            await vm.TestConnectionCommand.ExecuteAsync (null);

            Assert.Equal ("Basic " + Convert.ToBase64String ("alice:typed"u8.ToArray ()), rig.Tester.Calls[0].Authorization);
        }

        [Fact]
        public void TestingIsUnavailable_WhileTheSettingsAreInvalid ()
        {
            var (_, vm) = Open ();

            vm.ServerUrl = "not a url";

            Assert.False (vm.TestConnectionCommand.CanExecute (null));
        }

        [Fact]
        public async Task ClearHistory_RemovesResolvedAlerts ()
        {
            var (rig, vm) = Open ();
            rig.Engine.Handle (new NtfyEvent (NtfyEventKind.Message, new NtfyMessage ("a", rig.Clock.Now, "t", "Workshop: alarm", "", 5, []), MessageOrigin.Backlog));
            rig.Engine.Handle (new NtfyEvent (NtfyEventKind.Message, new NtfyMessage ("b", rig.Clock.Now, "t", "Workshop: cleared", "", 3, []), MessageOrigin.Backlog));
            Assert.Single (rig.Hub.Snapshot.History);

            vm.ClearHistoryCommand.Execute (null);

            Assert.Empty (rig.Hub.Snapshot.History);
            await Task.CompletedTask;
        }

        [Fact]
        public void Back_LeavesWithoutSaving ()
        {
            var (rig, vm) = Open ();
            vm.BuddyName = "Changed";

            vm.BackCommand.Execute (null);

            Assert.Same (rig.Root, rig.Navigator.Current);
            Assert.Equal ("Pip", rig.Store.Current.BuddyName);
        }

        [Fact]
        public async Task ASavedPasswordIsNeverInThePropertyChangeLog ()
        {
            var (_, vm) = Open ();
            var log = new PropertyLog (vm);
            vm.Secret = "hunter2";

            await vm.SaveCommand.ExecuteAsync (null);

            Assert.DoesNotContain ("hunter2", string.Join (",", log.Names));
            Assert.Equal ("", vm.Secret);
        }
    }
}
