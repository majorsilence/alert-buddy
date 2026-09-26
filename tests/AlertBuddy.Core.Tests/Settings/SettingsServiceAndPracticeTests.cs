using AlertBuddy.Core.Alerts;
using AlertBuddy.Core.Interpretation;
using AlertBuddy.Core.Ntfy;
using AlertBuddy.Core.Practice;
using AlertBuddy.Core.Settings;
using AlertBuddy.Core.Store;
using AlertBuddy.TestSupport;
using Xunit;

namespace AlertBuddy.Core.Tests.Settings
{
    public class SettingsServiceTests
    {
        [Fact]
        public void Current_IsWhatTheStoreHeld_AndSaveReplacesIt_AndTellsSubscribers ()
        {
            var store = new InMemorySettingsStore { Current = new AppSettings { BuddyName = "Dot" } };
            var service = new SettingsService (store);
            var told = 0;
            service.Changed += () => told++;

            Assert.Equal ("Dot", service.Current.BuddyName);

            service.Save (service.Current with { BuddyName = "Pip" });

            Assert.Equal ("Pip", service.Current.BuddyName);
            Assert.Equal ("Pip", store.Current.BuddyName);      // and it reached the store
            Assert.Equal (1, told);
        }

        [Fact]
        public void ASaveThatFails_ChangesNothing_AndTellsNobody ()
        {
            var service = new SettingsService (new ThrowingStore ());
            var told = 0;
            service.Changed += () => told++;

            Assert.Throws<IOException> (() => service.Save (new AppSettings { BuddyName = "Pip" }));

            Assert.Equal ("Pip", new AppSettings ().BuddyName);   // the default, so a changed name would show
            Assert.Equal (new AppSettings (), service.Current);   // memory still matches what is on disk
            Assert.Equal (0, told);
        }

        private sealed class ThrowingStore : ISettingsStore
        {
            public AppSettings Load () => new ();

            public void Save (AppSettings settings) => throw new IOException ("disk full");
        }
    }

    public class ConfigureTests
    {
        private static readonly DateTimeOffset T0 = new (2026, 9, 26, 12, 0, 0, TimeSpan.Zero);

        [Fact]
        public void TheStoresSilenceWindow_CanBeChangedAtRuntime ()
        {
            var store = new AlertStore ();
            var alarm = new InterpretedMessage ("a1", T0, InterpretedKind.Alert, AlertLevel.Alarm, "Workshop", "Workshop", "", null);
            var alert = store.Apply (alarm, MessageOrigin.Live, T0).Alert!;
            store.Acknowledge (alert.Id, T0);
            store.Configure (store.Options with { SilenceWindow = TimeSpan.FromMinutes (2) });

            var repeat = new InterpretedMessage ("a2", T0 + TimeSpan.FromMinutes (3), InterpretedKind.Alert, AlertLevel.Alarm, "Workshop", "Workshop", "", null);

            // Three minutes on is past the new two-minute window, though it would be inside the default ten.
            Assert.Equal (AlertSound.Alarm, store.Apply (repeat, MessageOrigin.Live, T0 + TimeSpan.FromMinutes (3)).Sound);
        }

        [Fact]
        public void Configure_KeepsTheOtherOptions ()
        {
            var store = new AlertStore (new AlertStoreOptions { MaxAlerts = 7 });

            store.Configure (store.Options with { SilenceWindow = TimeSpan.FromMinutes (1) });

            Assert.Equal ((7, TimeSpan.FromMinutes (1)), (store.Options.MaxAlerts, store.Options.SilenceWindow));
        }

        [Fact]
        public void TheEngine_AppliesNewInterpretationSettings_AndTheSilenceWindowTogether ()
        {
            var clock = new TestClock (T0);
            var hub = new AlertHub ();
            var engine = new AlertEngine (new AlertStore (), hub, clock);

            engine.Configure (new AppSettings {
                Interpretation = new InterpretationSettings { SourceSeparator = " is " },
                SilenceWindow = TimeSpan.FromMinutes (1),
            });

            var change = engine.Handle (new NtfyEvent (NtfyEventKind.Message,
                new NtfyMessage ("m1", T0, "t", "Cellar is warm", "", 4, []), MessageOrigin.Live));
            Assert.Equal ("Cellar", change!.Alert!.Source);
        }
    }

    public class PracticeAlertSourceTests
    {
        private static readonly DateTimeOffset T0 = new (2026, 9, 26, 12, 0, 0, TimeSpan.Zero);

        [Fact]
        public void TheScript_IsAWarningAnAlarmAndAnAllClear_InOrder_WithinTwentySeconds ()
        {
            var script = PracticeAlertSource.Script (T0);

            Assert.Equal ([1, 2, 3], script.Select (s => s.Number));
            Assert.Equal ([4, 5, 3], script.Select (s => s.Message.Priority));
            Assert.True (script.Select (s => s.After).SequenceEqual (script.Select (s => s.After).Order ()));
            Assert.True (script[^1].After < PracticeAlertSource.Length);
            Assert.Equal (TimeSpan.FromSeconds (20), PracticeAlertSource.Length);
        }

        [Fact]
        public void TheScript_UsesOneFictionalSource_AndRunsThroughTheRealInterpreterAndStore ()
        {
            var interpreter = new AlertInterpreter ();
            var store = new AlertStore ();

            var changes = PracticeAlertSource.Script (T0)
                .Select (s => store.Apply (interpreter.Interpret (s.Message), MessageOrigin.Live, T0))
                .ToList ();

            Assert.Equal ([AlertChangeKind.Raised, AlertChangeKind.Upgraded, AlertChangeKind.Resolved], changes.Select (c => c.Kind));
            Assert.Equal (PracticeAlertSource.Source, changes[0].Alert!.Source);
            Assert.Empty (store.Active);
        }

        [Fact]
        public void ASecondRun_IsNeverADuplicateOfTheFirst ()
        {
            var store = new AlertStore ();
            var interpreter = new AlertInterpreter ();
            foreach (var step in PracticeAlertSource.Script (T0))
                store.Apply (interpreter.Interpret (step.Message), MessageOrigin.Live, T0);

            var second = PracticeAlertSource.Script (T0.AddSeconds (30));

            Assert.All (second, s => Assert.NotEqual (AlertChangeKind.Duplicate,
                store.Apply (interpreter.Interpret (s.Message), MessageOrigin.Live, T0.AddSeconds (30)).Kind));
        }

        [Fact]
        public void TheSource_IsInventedNotARealRoom ()
            => Assert.Equal ("Practice room", PracticeAlertSource.Source);
    }
}
