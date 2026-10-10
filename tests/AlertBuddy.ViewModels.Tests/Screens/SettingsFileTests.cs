using AlertBuddy.Core.Localization;
using AlertBuddy.Core.Security;
using AlertBuddy.Core.Settings;
using AlertBuddy.ViewModels.Screens;
using Xunit;

namespace AlertBuddy.ViewModels.Tests.Screens
{
    public class SettingsFileTests
    {
        private static AppSettings Configured () => new () {
            ServerUrl = "https://ntfy.example.com", Topic = "home-alerts", FirstRunComplete = true, BuddyName = "Pip",
            Pin = PinHasher.Create ("4821"), AlarmTone = AlarmTone.VoiceEvacuation,
        };

        [Fact]
        public void SettingsAreOnlyOfferedToSaveToAFile_WhereTheDeviceCanPickOne ()
        {
            Assert.False (new FormRig (Configured ()).SettingsScreen ().CanTransferSettings);
            Assert.False (new FormRig (Configured ()).SettingsScreen (transfer: new FakeSettingsTransfer { IsSupported = false }).CanTransferSettings);
            Assert.True (new FormRig (Configured ()).SettingsScreen (transfer: new FakeSettingsTransfer ()).CanTransferSettings);
        }

        [Fact]
        public async Task SavingToAFile_WritesTheSavedSettings_UnderTheSuggestedName ()
        {
            var transfer = new FakeSettingsTransfer ();
            var rig = new FormRig (Configured ());
            var vm = rig.SettingsScreen (transfer: transfer);

            await vm.SaveSettingsFileCommand.ExecuteAsync (null);

            Assert.Equal (SettingsBackup.FileName, transfer.SavedName);
            Assert.Equal ("home-alerts", SettingsBackup.TryRead (transfer.SavedText!)!.Topic);
            Assert.Equal ("Settings saved to the file.", vm.TransferMessage);
        }

        [Fact]
        public async Task CancellingThePicker_SaysNothingWasSaved ()
        {
            var rig = new FormRig (Configured ());
            var vm = rig.SettingsScreen (transfer: new FakeSettingsTransfer { Cancel = true });

            await vm.SaveSettingsFileCommand.ExecuteAsync (null);
            Assert.Equal ("Nothing was saved.", vm.TransferMessage);

            await vm.LoadSettingsFileCommand.ExecuteAsync (null);
            Assert.Equal ("Nothing was loaded.", vm.TransferMessage);
        }

        [Fact]
        public async Task LoadingAFile_AppliesItAtOnce_AndSaysTheSecretIsEnteredAgain ()
        {
            var other = Configured () with { Topic = "other-topic", Auth = AuthMode.Basic, Username = "alice", BuddyName = "Mo", AlarmTone = AlarmTone.Code3 };
            var transfer = new FakeSettingsTransfer { FileToLoad = SettingsBackup.Write (other) };
            var rig = new FormRig (Configured ());
            var vm = rig.SettingsScreen (transfer: transfer);

            await vm.LoadSettingsFileCommand.ExecuteAsync (null);

            Assert.Equal (("other-topic", "alice", "Mo", AlarmTone.Code3), (rig.Settings.Current.Topic, rig.Settings.Current.Username, rig.Settings.Current.BuddyName, rig.Settings.Current.AlarmTone));
            Assert.Equal (("other-topic", "Mo", AlarmTone.Code3), (vm.Topic, vm.BuddyName, vm.AlarmTone));      // the screen shows it too
            Assert.Equal ("Settings loaded. Enter the password or token again.", vm.TransferMessage);
        }

        [Fact]
        public async Task LoadingAFileThatIsNotAlertBuddys_ChangesNothing ()
        {
            var transfer = new FakeSettingsTransfer { FileToLoad = """{ "Topic": "x" }""" };
            var rig = new FormRig (Configured ());
            var vm = rig.SettingsScreen (transfer: transfer);

            await vm.LoadSettingsFileCommand.ExecuteAsync (null);

            Assert.Equal ("home-alerts", rig.Settings.Current.Topic);
            Assert.Equal ("That is not an Alert Buddy settings file.", vm.TransferMessage);
        }

        [Fact]
        public async Task ARestoreOnFirstRun_AppliesTheFile_AndGoesHome ()
        {
            var transfer = new FakeSettingsTransfer { FileToLoad = SettingsBackup.Write (Configured ()) };
            var rig = new FormRig (new AppSettings ());           // a new install: nothing set up
            var vm = rig.FirstRunWith (transfer);
            Assert.True (vm.CanRestoreSettings);

            await vm.RestoreFromFileCommand.ExecuteAsync (null);

            Assert.Equal (("home-alerts", true), (rig.Settings.Current.Topic, rig.Settings.Current.FirstRunComplete));
            Assert.True (PinHasher.Verify ("4821", rig.Settings.Current.Pin));
            Assert.Equal (1, rig.Listener.Restarts);                 // the listener starts on the restored server
        }

        [Fact]
        public async Task ARestoreOnFirstRun_WithABadFile_StaysOnFirstRunAndSaysSo ()
        {
            var transfer = new FakeSettingsTransfer { FileToLoad = "nonsense" };
            var rig = new FormRig (new AppSettings ());
            var vm = rig.FirstRunWith (transfer);

            await vm.RestoreFromFileCommand.ExecuteAsync (null);

            Assert.False (rig.Settings.Current.FirstRunComplete);
            Assert.Equal ("That is not an Alert Buddy settings file.", vm.RestoreMessage);
        }

        [Fact]
        public void FirstRun_OffersNoRestore_WhereTheDeviceHasNoFilePicker ()
        {
            Assert.False (new FormRig (new AppSettings ()).FirstRun ().CanRestoreSettings);
        }
    }
}
