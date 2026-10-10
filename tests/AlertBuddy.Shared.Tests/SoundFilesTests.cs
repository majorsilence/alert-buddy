using AlertBuddy.Shared.Platform;
using AlertBuddy.ViewModels.Services;
using Xunit;

namespace AlertBuddy.Shared.Tests
{
    public class SoundFilesTests
    {
        private static string SoundsDirectory ()
        {
            var dir = Directory.GetCurrentDirectory ();
            while (dir is not null && !File.Exists (Path.Combine (dir, "AlertBuddy.slnx")))
                dir = Path.GetDirectoryName (dir);

            return Path.Combine (dir!, "src", "AlertBuddy.Android", "Assets", "sounds");
        }

        [Fact]
        public void EveryCue_HasItsOwnFile_ThatExists ()
        {
            var files = new HashSet<string> ();
            foreach (var cue in Enum.GetValues<Cue> ()) {
                var name = SoundFiles.FileName (cue);
                Assert.True (File.Exists (Path.Combine (SoundsDirectory (), name)), $"{cue}: no {name}");
                Assert.True (files.Add (name), $"{cue} shares {name} with another cue");
            }
        }

        [Fact]
        public void EveryGeneratedFile_IsPlayedByACue ()
        {
            var played = Enum.GetValues<Cue> ().Select (SoundFiles.FileName).ToHashSet ();

            foreach (var file in Directory.EnumerateFiles (SoundsDirectory (), "*.wav").Select (Path.GetFileName))
                Assert.Contains (file!, played);
        }
    }

    public class DesktopSoundPlayerTests
    {
        [Fact]
        public void WithNoSoundsBesideTheProgram_ItIsNotSupported_AndEveryCallIsSilentNotAnError ()
        {
            using var player = new DesktopSoundPlayer (Path.Combine (Path.GetTempPath (), "alertbuddy-no-sounds-" + Guid.NewGuid ()));

            Assert.False (player.IsSupported);
            player.Play (Cue.Alarm);
            player.StartLoop (Cue.Alarm);
            player.StopLoop ();
            player.StopLoop ();
        }

        [Fact]
        public void AMissingFile_IsSilent_WhenTheDirectoryExists ()
        {
            var dir = Directory.CreateTempSubdirectory ("alertbuddy-sounds-");
            try {
                using var player = new DesktopSoundPlayer (dir.FullName);

                Assert.True (player.IsSupported);
                player.Play (Cue.Warning);
                player.StartLoop (Cue.Alarm);
                player.StopLoop ();
            } finally {
                dir.Delete (true);
            }
        }

        [Fact]
        public void TheDesktopHeadShipsTheSoundsBesideTheProgram ()
        {
            // The default place is Assets/sounds beside the program, which the desktop project copies from the generated set.
            var shipped = Path.Combine (AppContext.BaseDirectory, "Assets", "sounds");
            if (!Directory.Exists (shipped))
                return;        // this test host is not the desktop head

            Assert.True (new DesktopSoundPlayer ().IsSupported);
        }
    }
}
