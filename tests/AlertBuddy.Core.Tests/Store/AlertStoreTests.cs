using AlertBuddy.Core.Alerts;
using AlertBuddy.Core.Interpretation;
using AlertBuddy.Core.Ntfy;
using AlertBuddy.Core.Store;
using Xunit;

namespace AlertBuddy.Core.Tests.Store
{
    public class AlertStoreTests
    {
        private static readonly DateTimeOffset T0 = new (2026, 9, 26, 12, 0, 0, TimeSpan.Zero);

        private int counter;

        /// <summary>A message about a source at a level, <paramref name="ago"/> before <see cref="T0"/>.</summary>
        private InterpretedMessage Msg (string source, AlertLevel level, TimeSpan? ago = null, string? id = null, double? temperature = null, string body = "body")
            => new (id ?? $"m{++counter}", T0 - (ago ?? TimeSpan.Zero), level == AlertLevel.Calm ? InterpretedKind.Clear : InterpretedKind.Alert,
                level, source, $"{source}: title", body, temperature);

        private static AlertChange Live (AlertStore store, InterpretedMessage m, DateTimeOffset? now = null) => store.Apply (m, MessageOrigin.Live, now ?? T0);

        // ---- raising ----

        [Theory]
        [InlineData (AlertLevel.Warning, AlertSound.Warning)]
        [InlineData (AlertLevel.Alarm, AlertSound.Alarm)]
        public void ANewWarningOrAlarm_CreatesAnActiveAlert_AndSounds (AlertLevel level, AlertSound sound)
        {
            var store = new AlertStore ();

            var change = Live (store, Msg ("Workshop", level, temperature: 41.2));

            Assert.Equal (AlertChangeKind.Raised, change.Kind);
            Assert.Equal (sound, change.Sound);
            var alert = Assert.Single (store.Active);
            Assert.Equal (("Workshop", level, AlertStatus.Active, 41.2), (alert.Source, alert.Level, alert.Status, alert.Temperature));
        }

        [Fact]
        public void TwoSources_AreTwoCards ()
        {
            var store = new AlertStore ();

            Live (store, Msg ("Workshop", AlertLevel.Warning));
            Live (store, Msg ("Music studio", AlertLevel.Warning));

            Assert.Equal (2, store.Active.Count);
        }

        [Fact]
        public void TheSourceMatch_IgnoresCase ()
        {
            var store = new AlertStore ();

            Live (store, Msg ("Workshop", AlertLevel.Warning));
            var second = Live (store, Msg ("workshop", AlertLevel.Warning));

            Assert.Equal (AlertChangeKind.Updated, second.Kind);
            Assert.Single (store.Active);
        }

        // ---- upgrade: one card, not two ----

        [Fact]
        public void AnAlarm_ForASourceWithAWarning_UpgradesTheSameCard ()
        {
            var store = new AlertStore ();
            var warning = Live (store, Msg ("Workshop", AlertLevel.Warning, temperature: 41.2)).Alert!;

            var change = Live (store, Msg ("Workshop", AlertLevel.Alarm, temperature: 50.6));

            Assert.Equal (AlertChangeKind.Upgraded, change.Kind);
            Assert.Equal (AlertSound.Alarm, change.Sound);
            var alert = Assert.Single (store.Active);
            Assert.Equal (warning.Id, alert.Id);                       // the card keeps its identity as it upgrades
            Assert.Equal ((AlertLevel.Alarm, 50.6), (alert.Level, alert.Temperature));
        }

        [Fact]
        public void AnUpgrade_IsANewEmergency_SoAnEarlierAcknowledgementDoesNotSilenceIt ()
        {
            var store = new AlertStore ();
            var warning = Live (store, Msg ("Workshop", AlertLevel.Warning)).Alert!;
            store.Acknowledge (warning.Id, T0);

            var change = Live (store, Msg ("Workshop", AlertLevel.Alarm));

            Assert.Equal (AlertSound.Alarm, change.Sound);
            Assert.Equal (AlertStatus.Active, change.Alert!.Status);
            Assert.Null (change.Alert.AcknowledgedAt);
        }

        // ---- repeats and the silence window ----

        [Fact]
        public void ARepeatedWarning_RefreshesTheCard_ButNeverSoundsAgain ()
        {
            var store = new AlertStore ();
            Live (store, Msg ("Workshop", AlertLevel.Warning, temperature: 41.0));

            var change = Live (store, Msg ("Workshop", AlertLevel.Warning, temperature: 42.5, body: "warmer"));

            Assert.Equal (AlertChangeKind.Updated, change.Kind);
            Assert.Equal (AlertSound.None, change.Sound);
            Assert.Equal ((42.5, "warmer"), (change.Alert!.Temperature, change.Alert.Body));
        }

        [Fact]
        public void ARepeatedAlarm_NobodyHasRespondedTo_SoundsAgain ()
        {
            var store = new AlertStore ();
            Live (store, Msg ("Workshop", AlertLevel.Alarm));

            Assert.Equal (AlertSound.Alarm, Live (store, Msg ("Workshop", AlertLevel.Alarm)).Sound);
            Assert.Equal (AlertSound.Alarm, Live (store, Msg ("Workshop", AlertLevel.Alarm)).Sound);
        }

        [Fact]
        public void ARepeatedAlarm_WithinTheSilenceWindowOfAnAcknowledgement_IsSilent ()
        {
            var store = new AlertStore ();
            var alarm = Live (store, Msg ("Workshop", AlertLevel.Alarm)).Alert!;
            store.Acknowledge (alarm.Id, T0);

            var change = Live (store, Msg ("Workshop", AlertLevel.Alarm), now: T0 + TimeSpan.FromMinutes (9));

            Assert.Equal (AlertSound.None, change.Sound);
            Assert.Equal (AlertStatus.Acknowledged, change.Alert!.Status);
        }

        [Fact]
        public void ARepeatedAlarm_AfterTheSilenceWindow_SoundsAgain_AndTheChildIsAskedAgain ()
        {
            var store = new AlertStore ();
            var alarm = Live (store, Msg ("Workshop", AlertLevel.Alarm)).Alert!;
            store.Acknowledge (alarm.Id, T0);
            var later = T0 + TimeSpan.FromMinutes (10);

            var change = Live (store, Msg ("Workshop", AlertLevel.Alarm, id: "again"), now: later);

            Assert.Equal (AlertSound.Alarm, change.Sound);
            Assert.Equal (AlertStatus.Active, change.Alert!.Status);    // so the takeover screen returns
            Assert.Null (change.Alert.AcknowledgedAt);
        }

        [Fact]
        public void TheSilenceWindow_IsASetting ()
        {
            var store = new AlertStore (new AlertStoreOptions { SilenceWindow = TimeSpan.FromMinutes (2) });
            var alarm = Live (store, Msg ("Workshop", AlertLevel.Alarm)).Alert!;
            store.Acknowledge (alarm.Id, T0);

            Assert.Equal (AlertSound.None, Live (store, Msg ("Workshop", AlertLevel.Alarm), T0 + TimeSpan.FromMinutes (1)).Sound);
            Assert.Equal (AlertSound.Alarm, Live (store, Msg ("Workshop", AlertLevel.Alarm), T0 + TimeSpan.FromMinutes (3)).Sound);
        }

        [Fact]
        public void AHandledAlarm_StaysSilent_UntilItsSourceResolves ()
        {
            var store = new AlertStore ();
            var alarm = Live (store, Msg ("Workshop", AlertLevel.Alarm)).Alert!;
            store.Handle (alarm.Id);

            // Hours later, still repeating: a grown-up said "Got it", so it stays quiet however long it goes on. The repeat is stamped
            // with the later time as well, so it is fresh: a stale message would be silent whatever the rule said, and prove nothing.
            var later = T0 + TimeSpan.FromHours (3);
            var change = Live (store, Msg ("Workshop", AlertLevel.Alarm, ago: -TimeSpan.FromHours (3)), now: later);

            Assert.Equal (AlertSound.None, change.Sound);
            Assert.Equal (AlertStatus.Handled, change.Alert!.Status);

            // The control: the same repeat for an alarm nobody handled DOES sound, so the silence above came from "Got it".
            var other = Live (store, Msg ("Music studio", AlertLevel.Alarm, ago: -TimeSpan.FromHours (3)), now: later);
            Assert.Equal (AlertSound.Alarm, other.Sound);
        }

        [Fact]
        public void AnAlarmEasingToAWarning_LowersTheCardQuietly_AndKeepsWhatWasSaid ()
        {
            var store = new AlertStore ();
            var alarm = Live (store, Msg ("Workshop", AlertLevel.Alarm)).Alert!;
            store.Acknowledge (alarm.Id, T0);

            var change = Live (store, Msg ("Workshop", AlertLevel.Warning));

            Assert.Equal ((AlertChangeKind.Downgraded, AlertSound.None), (change.Kind, change.Sound));
            Assert.Equal ((AlertLevel.Warning, AlertStatus.Acknowledged), (change.Alert!.Level, change.Alert.Status));
        }

        // ---- acknowledging ----

        [Fact]
        public void Acknowledging_SilencesSoundOnly_TheCardStays ()
        {
            var store = new AlertStore ();
            var alarm = Live (store, Msg ("Workshop", AlertLevel.Alarm)).Alert!;

            var acknowledged = store.Acknowledge (alarm.Id, T0);

            Assert.Equal ((AlertStatus.Acknowledged, T0), (acknowledged!.Status, acknowledged.AcknowledgedAt));
            Assert.Single (store.Active);
        }

        [Fact]
        public void AGrownUpsGotIt_IsNotUndoneByTheChildsTap ()
        {
            var store = new AlertStore ();
            var alarm = Live (store, Msg ("Workshop", AlertLevel.Alarm)).Alert!;
            store.Handle (alarm.Id);

            Assert.Equal (AlertStatus.Handled, store.Acknowledge (alarm.Id, T0)!.Status);
        }

        [Fact]
        public void ActingOnAnUnknownOrResolvedAlert_ReturnsNull ()
        {
            var store = new AlertStore ();
            var alarm = Live (store, Msg ("Workshop", AlertLevel.Alarm)).Alert!;
            Live (store, Msg ("Workshop", AlertLevel.Calm));

            Assert.Null (store.Acknowledge ("nope", T0));
            Assert.Null (store.Acknowledge (alarm.Id, T0));   // already resolved
            Assert.Null (store.Handle (alarm.Id));
        }

        // ---- the all clear ----

        [Fact]
        public void ACalmMessage_ForASourceWithAnOpenAlert_ResolvesIt_AndChimes ()
        {
            var store = new AlertStore ();
            var alarm = Live (store, Msg ("Workshop", AlertLevel.Alarm, temperature: 50.6)).Alert!;

            var change = Live (store, Msg ("Workshop", AlertLevel.Calm, temperature: 44.0));

            Assert.Equal ((AlertChangeKind.Resolved, AlertSound.AllClear), (change.Kind, change.Sound));
            Assert.Empty (store.Active);
            var resolved = Assert.Single (store.History);
            Assert.Equal ((alarm.Id, AlertStatus.Resolved, AlertLevel.Alarm, 50.6), (resolved.Id, resolved.Status, resolved.Level, resolved.Temperature));
        }

        [Fact]
        public void TheAllClear_ResolvesOnlyItsOwnSource_WhicheverWasRaisedFirst ()
        {
            var store = new AlertStore ();
            Live (store, Msg ("Music studio", AlertLevel.Warning));
            Live (store, Msg ("Workshop", AlertLevel.Warning));

            // The all clear is for the SECOND source raised. An implementation that resolved "the first open alert" would pick the
            // studio instead and leave the workshop open.
            var change = Live (store, Msg ("Workshop", AlertLevel.Calm));

            Assert.Equal ("Workshop", change.Alert!.Source);
            Assert.Equal ("Music studio", Assert.Single (store.Active).Source);
        }

        [Fact]
        public void ACalmMessage_ForASourceWithNothingOpen_IsIgnored ()
        {
            var store = new AlertStore ();

            var change = Live (store, Msg ("Workshop", AlertLevel.Calm));

            Assert.Equal ((AlertChangeKind.Ignored, AlertSound.None), (change.Kind, change.Sound));
            Assert.Empty (store.History);
        }

        [Fact]
        public void AfterAnAllClear_ANewWarning_StartsAFreshEpisode ()
        {
            var store = new AlertStore ();
            var first = Live (store, Msg ("Workshop", AlertLevel.Alarm)).Alert!;
            Live (store, Msg ("Workshop", AlertLevel.Calm));

            var second = Live (store, Msg ("Workshop", AlertLevel.Warning));

            Assert.Equal (AlertChangeKind.Raised, second.Kind);
            Assert.NotEqual (first.Id, second.Alert!.Id);
            Assert.Equal (2, store.History.Count);
        }

        // ---- tests, duplicates ----

        [Fact]
        public void ATestMessage_CreatesNoAlert_AndCheers ()
        {
            var store = new AlertStore ();
            var test = new InterpretedMessage ("t1", T0, InterpretedKind.Test, AlertLevel.Alarm, "Test", "Test", "", null);

            var change = Live (store, test);

            Assert.Equal ((AlertChangeKind.Test, AlertSound.TestCheer, (Alert?)null), (change.Kind, change.Sound, change.Alert));
            Assert.Empty (store.History);
        }

        [Fact]
        public void TheSameMessageTwice_IsProcessedOnce ()
        {
            var store = new AlertStore ();
            var m = Msg ("Workshop", AlertLevel.Warning, id: "same");
            Live (store, m);

            var again = Live (store, m);

            Assert.Equal ((AlertChangeKind.Duplicate, AlertSound.None), (again.Kind, again.Sound));
            Assert.Single (store.History);
        }

        [Fact]
        public void TheSeenIds_AreBounded ()
        {
            var store = new AlertStore (new AlertStoreOptions { MaxSeenIds = 3 });
            for (var i = 0; i < 10; i++)
                Live (store, Msg ("S" + i, AlertLevel.Warning, id: "id" + i));

            var seenIds = store.Export ().SeenIds;

            Assert.Equal (["id7", "id8", "id9"], seenIds);
        }

        // ---- replay never makes a sound ----

        [Theory]
        [InlineData (AlertLevel.Warning)]
        [InlineData (AlertLevel.Alarm)]
        public void ABacklogMessage_BuildsState_ButNeverSounds (AlertLevel level)
        {
            var store = new AlertStore ();

            var change = store.Apply (Msg ("Workshop", level), MessageOrigin.Backlog, T0);

            Assert.Equal ((AlertChangeKind.Raised, AlertSound.None, MessageOrigin.Backlog), (change.Kind, change.Sound, change.Origin));
            Assert.Single (store.Active);
        }

        [Fact]
        public void ReplayingAWholeHistory_LeavesTheRightAlertsActive_WithNoSound ()
        {
            // Workshop warns, alarms and clears; Music studio warns and is still warm. Replayed in order, silently.
            var store = new AlertStore ();
            var replay = new[] {
                Msg ("Workshop", AlertLevel.Warning, TimeSpan.FromMinutes (60)),
                Msg ("Music studio", AlertLevel.Warning, TimeSpan.FromMinutes (50)),
                Msg ("Workshop", AlertLevel.Alarm, TimeSpan.FromMinutes (40)),
                Msg ("Workshop", AlertLevel.Calm, TimeSpan.FromMinutes (20)),
            };

            var sounds = replay.Select (m => store.Apply (m, MessageOrigin.Backlog, T0).Sound).ToList ();

            Assert.All (sounds, s => Assert.Equal (AlertSound.None, s));
            Assert.Equal ("Music studio", Assert.Single (store.Active).Source);
            Assert.Equal (2, store.History.Count);
        }

        [Fact]
        public void ALiveMessageThatIsStale_ChangesStateSilently ()
        {
            var store = new AlertStore ();

            // An alarm from 16 minutes ago, delivered live after an outage: the state matters, a siren for it does not.
            var change = Live (store, Msg ("Workshop", AlertLevel.Alarm, TimeSpan.FromMinutes (16)));

            Assert.Equal ((AlertChangeKind.Raised, AlertSound.None), (change.Kind, change.Sound));
            Assert.Single (store.Active);
        }

        [Fact]
        public void ALiveMessageJustInsideTheFreshnessWindow_Sounds ()
        {
            var store = new AlertStore ();

            Assert.Equal (AlertSound.Alarm, Live (store, Msg ("Workshop", AlertLevel.Alarm, TimeSpan.FromMinutes (14))).Sound);
        }

        // ---- bounds and persistence ----

        [Fact]
        public void TheAlertBook_IsBounded_DroppingTheOldestResolvedFirst ()
        {
            var store = new AlertStore (new AlertStoreOptions { MaxAlerts = 3 });
            for (var i = 0; i < 5; i++) {
                Live (store, Msg ("S" + i, AlertLevel.Warning, TimeSpan.FromMinutes (100 - i * 10)));
                Live (store, Msg ("S" + i, AlertLevel.Calm, TimeSpan.FromMinutes (95 - i * 10)));
            }

            Assert.Equal (3, store.History.Count);
            Assert.Equal (["S4", "S3", "S2"], store.History.Select (a => a.Source));
        }

        [Fact]
        public void AnOpenAlert_IsNeverDroppedToMakeRoom ()
        {
            var store = new AlertStore (new AlertStoreOptions { MaxAlerts = 2 });
            for (var i = 0; i < 4; i++)
                Live (store, Msg ("S" + i, AlertLevel.Alarm));   // four sources all still alarming

            Assert.Equal (4, store.Active.Count);
        }

        [Fact]
        public void ClearHistory_RemovesResolvedAlerts_ButNotWhatIsStillHappening ()
        {
            var store = new AlertStore ();
            Live (store, Msg ("Workshop", AlertLevel.Alarm));
            Live (store, Msg ("Workshop", AlertLevel.Calm));
            Live (store, Msg ("Music studio", AlertLevel.Warning));

            store.ClearHistory ();

            Assert.Equal ("Music studio", Assert.Single (store.History).Source);
        }

        [Fact]
        public void ExportThenRestore_RoundTripsTheWholeState ()
        {
            var store = new AlertStore ();
            var alarm = Live (store, Msg ("Workshop", AlertLevel.Alarm, id: "a1", temperature: 50.6)).Alert!;
            store.Acknowledge (alarm.Id, T0);
            Live (store, Msg ("Music studio", AlertLevel.Warning, id: "a2"));
            Live (store, Msg ("Music studio", AlertLevel.Calm, id: "a3"));

            var restored = new AlertStore ();
            restored.Restore (store.Export ());

            Assert.Equal (store.History, restored.History);
            Assert.Equal ("a3", restored.LastMessageId);
            // and it still remembers what it has seen, so a replay of those messages does nothing
            Assert.Equal (AlertChangeKind.Duplicate, restored.Apply (Msg ("Workshop", AlertLevel.Alarm, id: "a1"), MessageOrigin.Backlog, T0).Kind);
        }

        [Fact]
        public void Restore_KeepsOneOpenCardPerSource_EvenFromAHandEditedFile ()
        {
            var state = new AlertStoreState ([
                new Alert ("a", "Workshop", AlertLevel.Warning, "t", "b", T0, T0, null, AlertStatus.Active),
                new Alert ("b", "Workshop", AlertLevel.Alarm, "t", "b", T0 + TimeSpan.FromMinutes (1), T0, null, AlertStatus.Active),
            ], [], null);

            var store = new AlertStore ();
            store.Restore (state);

            Assert.Equal ("b", Assert.Single (store.Active).Id);   // the newer one is the open one
        }

        [Fact]
        public void ConcurrentApplies_DoNotCorruptTheStore ()
        {
            var store = new AlertStore (new AlertStoreOptions { MaxAlerts = 1000, MaxSeenIds = 10000 });

            Parallel.For (0, 2000, i => {
                var source = "S" + (i % 20);
                store.Apply (new InterpretedMessage ("id" + i, T0, InterpretedKind.Alert, AlertLevel.Warning, source, source, "", null), MessageOrigin.Live, T0);
                _ = store.Active;
            });

            Assert.Equal (20, store.Active.Count);    // exactly one open card per source, however the threads interleaved
            Assert.Equal (2000, store.Export ().SeenIds.Count);
        }
    }
}
