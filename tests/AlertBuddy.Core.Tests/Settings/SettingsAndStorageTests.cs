using AlertBuddy.Core.Alerts;
using AlertBuddy.Core.Interpretation;
using AlertBuddy.Core.Ntfy;
using AlertBuddy.Core.Security;
using AlertBuddy.Core.Settings;
using AlertBuddy.Core.Store;
using Xunit;

namespace AlertBuddy.Core.Tests.Settings
{
    public sealed class TempDirectory : IDisposable
    {
        public string Path { get; } = System.IO.Path.Combine (System.IO.Path.GetTempPath (), "alertbuddy-tests-" + Guid.NewGuid ().ToString ("N"));

        public TempDirectory () => Directory.CreateDirectory (Path);

        public string File (string name) => System.IO.Path.Combine (Path, name);

        public void Dispose ()
        {
            try { Directory.Delete (Path, true); } catch (IOException) { }
        }
    }

    public class SettingsStoreTests
    {
        [Fact]
        public void EverySetting_SurvivesARoundTrip ()
        {
            using var dir = new TempDirectory ();
            var store = new JsonFileSettingsStore (dir.File ("settings.json"));
            var settings = new AppSettings {
                ServerUrl = "https://ntfy.example.com",
                Topic = "home-alerts",
                Auth = AuthMode.Basic,
                Username = "alice",
                BuddyName = "Dot",
                BuddyColour = BuddyColour.Plum,
                Look = LookPreference.Night,
                ReduceMotion = true,
                SilenceWindow = TimeSpan.FromMinutes (7),
                ReplayWindow = TimeSpan.FromHours (6),
                Night = new NightPolicy { Enabled = false, Start = new TimeOnly (21, 30), End = new TimeOnly (6, 45) },
                Interpretation = new InterpretationSettings { AlarmPriority = 4, SourceSeparator = " - ", StripLeadingEmoji = false },
                Pin = PinHasher.Create ("4821"),
                FirstRunComplete = true,
                SoundsEnabled = false,
            };

            store.Save (settings);
            var loaded = new JsonFileSettingsStore (dir.File ("settings.json")).Load ();

            Assert.Equal (settings, loaded);
            Assert.True (PinHasher.Verify ("4821", loaded.Pin));
        }

        [Fact]
        public void TheFile_IsReadable_AndHoldsNoSecret ()
        {
            using var dir = new TempDirectory ();
            var path = dir.File ("settings.json");
            new JsonFileSettingsStore (path).Save (new AppSettings { Auth = AuthMode.Basic, Username = "alice", Pin = PinHasher.Create ("4821") });

            var text = File.ReadAllText (path);

            Assert.Contains ("\"Basic\"", text);                    // enums as names, so a person can read and edit the file
            Assert.DoesNotContain ("4821", text);                   // the PIN only as a hash
            Assert.DoesNotContain ("password", text, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain ("token", text, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void NoFile_MeansTheDefaults ()
        {
            using var dir = new TempDirectory ();

            var loaded = new JsonFileSettingsStore (dir.File ("missing.json")).Load ();

            Assert.Equal (new AppSettings (), loaded);
            Assert.Equal (("Pip", TimeSpan.FromMinutes (10), TimeSpan.FromHours (12), false), (loaded.BuddyName, loaded.SilenceWindow, loaded.ReplayWindow, loaded.FirstRunComplete));
        }

        [Fact]
        public void ACorruptFile_MeansTheDefaults_AndIsSetAsideNotDeleted ()
        {
            using var dir = new TempDirectory ();
            var path = dir.File ("settings.json");
            File.WriteAllText (path, "{ this is not json");

            var loaded = new JsonFileSettingsStore (path).Load ();

            Assert.Equal (new AppSettings (), loaded);
            Assert.False (File.Exists (path));
            Assert.Equal ("{ this is not json", File.ReadAllText (path + ".corrupt"));
        }

        [Fact]
        public void AFileFromANewerBuild_WithFieldsWeDoNotKnow_StillLoads ()
        {
            using var dir = new TempDirectory ();
            var path = dir.File ("settings.json");
            File.WriteAllText (path, """{ "Topic": "home-alerts", "SomethingFromTheFuture": { "x": 1 } }""");

            Assert.Equal ("home-alerts", new JsonFileSettingsStore (path).Load ().Topic);
        }

        [Fact]
        public void DefaultsMatchThePlan ()
        {
            var d = new AppSettings ();

            Assert.Equal ((true, new TimeOnly (20, 0), new TimeOnly (7, 0)), (d.Night.Enabled, d.Night.Start, d.Night.End));
            Assert.Equal ((5, 4), (d.Interpretation.AlarmPriority, d.Interpretation.WarningPriority));
            Assert.Null (d.ReduceMotion);
            Assert.Null (d.Pin);
        }
    }

    public class AtomicFileTests
    {
        [Fact]
        public void AWrite_LeavesTheNewContentsAndNoTemporaryFile ()
        {
            using var dir = new TempDirectory ();
            var path = dir.File ("sub/dir/data.bin");

            AtomicFile.WriteAllBytes (path, [1, 2, 3]);

            Assert.Equal ([1, 2, 3], File.ReadAllBytes (path));
            Assert.False (File.Exists (path + ".tmp"));
        }

        [Fact]
        public void ARewrite_ReplacesTheContents ()
        {
            using var dir = new TempDirectory ();
            var path = dir.File ("data.bin");
            AtomicFile.WriteAllBytes (path, [1, 2, 3, 4, 5, 6, 7, 8]);

            AtomicFile.WriteAllBytes (path, [9]);

            Assert.Equal ([9], File.ReadAllBytes (path));   // not the old bytes with the new ones on top
        }

        [Fact]
        public void AWriteThatFails_LeavesTheOldFileExactlyAsItWas ()
        {
            using var dir = new TempDirectory ();
            var path = dir.File ("data.bin");
            AtomicFile.WriteAllBytes (path, [1, 2, 3]);
            Directory.CreateDirectory (path + ".tmp");        // the temporary file cannot be created, so the write fails partway

            Assert.ThrowsAny<Exception> (() => AtomicFile.WriteAllBytes (path, [9, 9, 9, 9]));

            Assert.Equal ([1, 2, 3], File.ReadAllBytes (path));
        }
    }

    public class AlertStateStoreTests
    {
        private static readonly DateTimeOffset T0 = new (2026, 9, 26, 12, 0, 0, TimeSpan.Zero);

        [Fact]
        public void TheState_SurvivesARoundTrip_IncludingEveryAlertField ()
        {
            using var dir = new TempDirectory ();
            var store = new JsonFileAlertStateStore (dir.File ("alerts.json"));
            var state = new AlertStoreState ([
                new Alert ("a1", "Workshop", AlertLevel.Alarm, "Workshop: alarm", "50.6 °C 🔥", T0, T0 + TimeSpan.FromMinutes (3), 50.6, AlertStatus.Acknowledged, T0 + TimeSpan.FromMinutes (1)),
                new Alert ("a2", "Music studio", AlertLevel.Warning, "Music studio: warm", "", T0, T0, null, AlertStatus.Resolved),
            ], ["a1", "a2"], "a2");

            store.Save (state);
            var loaded = store.Load ()!;

            Assert.Equal (state.Alerts, loaded.Alerts);
            Assert.Equal (state.SeenIds, loaded.SeenIds);
            Assert.Equal ("a2", loaded.LastMessageId);
        }

        [Fact]
        public void NoFile_IsNull ()
        {
            using var dir = new TempDirectory ();
            Assert.Null (new JsonFileAlertStateStore (dir.File ("none.json")).Load ());
        }

        [Fact]
        public void ACorruptFile_IsNull_AndSetAside ()
        {
            using var dir = new TempDirectory ();
            var path = dir.File ("alerts.json");
            File.WriteAllText (path, "\0\0\0 garbage");

            Assert.Null (new JsonFileAlertStateStore (path).Load ());
            Assert.True (File.Exists (path + ".corrupt"));
        }

        [Fact]
        public void TheFile_IsBounded_ByTheStoresBounds ()
        {
            using var dir = new TempDirectory ();
            var path = dir.File ("alerts.json");
            var alerts = new AlertStore (new AlertStoreOptions { MaxAlerts = 200 });
            for (var i = 0; i < 1000; i++) {
                alerts.Apply (new InterpretedMessage ("id" + i, T0, InterpretedKind.Alert, AlertLevel.Warning, "S" + i, "S" + i, "body", 40),
                    MessageOrigin.Live, T0);
                alerts.Apply (new InterpretedMessage ("c" + i, T0, InterpretedKind.Clear, AlertLevel.Calm, "S" + i, "S" + i, "", null),
                    MessageOrigin.Live, T0);
            }

            new JsonFileAlertStateStore (path).Save (alerts.Export ());

            Assert.Equal (200, new JsonFileAlertStateStore (path).Load ()!.Alerts.Count);
            Assert.True (new FileInfo (path).Length < 200_000, "the history file should stay small");
        }
    }
}
