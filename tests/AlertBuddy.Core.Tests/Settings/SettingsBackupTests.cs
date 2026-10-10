using AlertBuddy.Core.Localization;
using AlertBuddy.Core.Security;
using AlertBuddy.Core.Settings;
using Xunit;

namespace AlertBuddy.Core.Tests.Settings
{
    public class SettingsBackupTests
    {
        private static AppSettings Sample () => new () {
            ServerUrl = "https://ntfy.example.com", Topic = "home-alerts", Auth = AuthMode.Basic, Username = "alice", BuddyName = "Pip",
            FirstRunComplete = true, AlarmTone = AlarmTone.VoiceEvacuation, PracticeGentle = true, VoiceId = "en-us-x-iom", Language = AppLanguage.French,
            Pin = PinHasher.Create ("4821"),
        };

        [Fact]
        public void ASavedFile_RestoresEverySetting ()
        {
            var original = Sample ();

            var restored = SettingsBackup.TryRead (SettingsBackup.Write (original));

            Assert.NotNull (restored);
            Assert.Equal (("https://ntfy.example.com", "home-alerts", AuthMode.Basic, "alice", "Pip"), (restored.ServerUrl, restored.Topic, restored.Auth, restored.Username, restored.BuddyName));
            Assert.Equal ((AlarmTone.VoiceEvacuation, true, "en-us-x-iom", AppLanguage.French), (restored.AlarmTone, restored.PracticeGentle, restored.VoiceId, restored.Language));
            Assert.True (PinHasher.Verify ("4821", restored.Pin));       // the same PIN opens the gate after the restore
        }

        [Fact]
        public void TheFile_NeverCarriesAPasswordOrToken ()
        {
            var text = SettingsBackup.Write (Sample ());

            // They live in the secret store and are not in AppSettings at all, so nothing could put them in the file; this keeps it that way.
            Assert.DoesNotContain ("assword\\\":", text, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain ("\"Secret\"", text);
            Assert.DoesNotContain ("\"Token\"", text);
        }

        [Fact]
        public void TheFile_IsNamedAndVersioned_SoAnotherFileIsRefused ()
        {
            Assert.Contains ("AlertBuddy", SettingsBackup.Write (Sample ()));

            Assert.Null (SettingsBackup.TryRead ("""{ "ServerUrl": "https://x.example.com" }"""));       // someone else's JSON
            Assert.Null (SettingsBackup.TryRead ("""{ "app": "SomethingElse", "version": 1, "settings": {} }"""));
            Assert.Null (SettingsBackup.TryRead ("not json at all"));
            Assert.Null (SettingsBackup.TryRead (""));
            Assert.Null (SettingsBackup.TryRead ("""{ "app": "AlertBuddy", "version": 99, "settings": {} }"""));   // from a newer app than this one
        }

        [Fact]
        public void AnImportedFile_IsAlwaysMarkedFirstRunComplete_SoARestoreLandsOnHome ()
        {
            var restored = SettingsBackup.TryRead (SettingsBackup.Write (Sample () with { FirstRunComplete = false }));

            Assert.True (restored!.FirstRunComplete);
        }
    }
}
