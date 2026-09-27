using AlertBuddy.Core.Alerts;
using AlertBuddy.Core.Interpretation;
using AlertBuddy.Core.Ntfy;
using AlertBuddy.Core.Store;
using AlertBuddy.TestSupport;
using Xunit;

namespace AlertBuddy.Core.Tests.Store
{
    public class SoundPolicyTests
    {
        private static readonly NightPolicy Default = new ();
        private static readonly DateTimeOffset Noon = new (2026, 9, 26, 12, 0, 0, TimeSpan.Zero);

        private static DateTimeOffset At (int hour, int minute) => new (2026, 9, 26, hour, minute, 0, TimeSpan.Zero);

        [Theory]
        [InlineData (19, 59, false)]
        [InlineData (20, 0, true)]        // quiet hours start on the dot
        [InlineData (23, 59, true)]
        [InlineData (0, 0, true)]         // past midnight
        [InlineData (6, 59, true)]
        [InlineData (7, 0, false)]        // and end on the dot
        [InlineData (12, 0, false)]
        public void QuietHours_RunPastMidnight (int hour, int minute, bool quiet)
            => Assert.Equal (quiet, Default.Covers (new TimeOnly (hour, minute)));

        [Fact]
        public void QuietHours_CanAlsoBeWithinOneDay ()
        {
            var lunch = new NightPolicy { Start = new TimeOnly (13, 0), End = new TimeOnly (15, 0) };

            Assert.False (lunch.Covers (new TimeOnly (12, 59)));
            Assert.True (lunch.Covers (new TimeOnly (13, 0)));
            Assert.True (lunch.Covers (new TimeOnly (14, 59)));
            Assert.False (lunch.Covers (new TimeOnly (15, 0)));
        }

        [Theory]
        [InlineData (AlertSound.Warning, 22, AlertSound.None)]
        [InlineData (AlertSound.AllClear, 3, AlertSound.None)]
        [InlineData (AlertSound.Alarm, 22, AlertSound.Alarm)]          // an alarm is never muted by quiet hours
        [InlineData (AlertSound.Alarm, 3, AlertSound.Alarm)]
        [InlineData (AlertSound.TestCheer, 22, AlertSound.TestCheer)]  // a test is someone deliberately checking it works
        [InlineData (AlertSound.Warning, 12, AlertSound.Warning)]
        [InlineData (AlertSound.AllClear, 12, AlertSound.AllClear)]
        [InlineData (AlertSound.None, 22, AlertSound.None)]
        public void AtNight_OnlyWarningsAndAllClearsAreSilenced (AlertSound sound, int hour, AlertSound expected)
            => Assert.Equal (expected, SoundPolicy.Apply (sound, Default, At (hour, 0), TimeZoneInfo.Utc));

        [Fact]
        public void ADisabledPolicy_SilencesNothing ()
            => Assert.Equal (AlertSound.Warning, SoundPolicy.Apply (AlertSound.Warning, new NightPolicy { Enabled = false }, At (22, 0), TimeZoneInfo.Utc));

        [Fact]
        public void LocalTime_NotUtc_DecidesWhatIsNight ()
        {
            var minusFive = TimeZoneInfo.CreateCustomTimeZone ("test-5", TimeSpan.FromHours (-5), "test", "test");

            // 03:00 UTC is 22:00 the evening before at UTC-5: night. 12:00 UTC is 07:00 there: the morning has begun.
            Assert.Equal (AlertSound.None, SoundPolicy.Apply (AlertSound.Warning, Default, At (3, 0), minusFive));
            Assert.Equal (AlertSound.Warning, SoundPolicy.Apply (AlertSound.Warning, Default, At (12, 0), minusFive));
            // and the reverse: 22:00 UTC is 17:00 at UTC-5, which is not night.
            Assert.Equal (AlertSound.Warning, SoundPolicy.Apply (AlertSound.Warning, Default, At (22, 0), minusFive));
        }
    }

    public class AlertHubTests
    {
        private static readonly AlertChange AnyChange = new (AlertChangeKind.Ignored, null, AlertSound.None, MessageOrigin.Live);

        [Fact]
        public void Publish_UpdatesTheSnapshot_AndTellsSubscribers ()
        {
            var hub = new AlertHub ();
            AlertChange? seen = null;
            hub.AlertChanged += c => seen = c;
            var alert = new Alert ("a", "Workshop", AlertLevel.Alarm, "t", "b", DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch, null, AlertStatus.Active);

            hub.Publish (AnyChange, [alert], [alert]);

            Assert.Same (AnyChange, seen);
            Assert.Equal (alert, Assert.Single (hub.Snapshot.Active));
        }

        [Fact]
        public void ALateSubscriber_CanReadWhatIsTrueNow ()
        {
            var hub = new AlertHub ();
            var alert = new Alert ("a", "Workshop", AlertLevel.Alarm, "t", "b", DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch, null, AlertStatus.Active);
            hub.Publish (AnyChange, [alert], [alert]);
            hub.SetConnection (new ConnectionInfo (ConnectionState.Live));

            var snapshot = hub.Snapshot;      // the UI attaching to a service that has been running for hours

            Assert.Single (snapshot.Active);
            Assert.Equal (ConnectionState.Live, snapshot.Connection.State);
        }

        [Fact]
        public void AThrowingSubscriber_DoesNotStopTheOthers_AndIsReported ()
        {
            var hub = new AlertHub ();
            var reached = new List<string> ();
            var failures = new List<Exception> ();
            hub.HandlerFailed += failures.Add;
            hub.AlertChanged += _ => reached.Add ("first");
            hub.AlertChanged += _ => throw new InvalidOperationException ("a broken screen");
            hub.AlertChanged += _ => reached.Add ("third");

            hub.Publish (AnyChange, [], []);

            Assert.Equal (["first", "third"], reached);
            Assert.Equal ("a broken screen", Assert.Single (failures).Message);
        }

        [Fact]
        public void SetConnection_AnnouncesOnlyRealChanges ()
        {
            var hub = new AlertHub ();
            var announced = new List<ConnectionInfo> ();
            hub.ConnectionChanged += announced.Add;

            hub.SetConnection (new ConnectionInfo (ConnectionState.Live));
            hub.SetConnection (new ConnectionInfo (ConnectionState.Live));       // identical: nothing to say
            hub.SetConnection (new ConnectionInfo (ConnectionState.Reconnecting));

            Assert.Equal ([ConnectionState.Live, ConnectionState.Reconnecting], announced.Select (c => c.State));
        }

        [Fact]
        public void SetAlerts_UpdatesTheSnapshot_WithoutAnnouncingAChange ()
        {
            var hub = new AlertHub ();
            var announced = 0;
            hub.AlertChanged += _ => announced++;
            var alert = new Alert ("a", "Workshop", AlertLevel.Warning, "t", "b", DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch, null, AlertStatus.Active);

            hub.SetAlerts ([alert], [alert]);

            Assert.Equal (0, announced);
            Assert.Single (hub.Snapshot.Active);
        }
    }

    public class AlertEngineTests
    {
        private static NtfyEvent Line (string json)
        {
            Assert.True (NtfyParser.TryParse (json, out var e));
            return e!;
        }

        private sealed class Rig
        {
            public TestClock Clock { get; } = new (AppendixC.At (1790000100));
            public AlertHub Hub { get; } = new ();
            public InMemoryAlertStateStore State { get; } = new ();
            public List<AlertChange> Changes { get; } = [];
            public List<Exception> PersistFailures { get; } = [];
            public AlertEngine Engine { get; }

            public Rig (InMemoryAlertStateStore? state = null)
            {
                State = state ?? State;
                Hub.AlertChanged += Changes.Add;
                Engine = new AlertEngine (new AlertStore (), Hub, Clock, State, persistFailed: PersistFailures.Add);
            }
        }

        [Fact]
        public void TheWholeAppendixCLifecycle_ThroughTheEngine ()
        {
            var rig = new Rig ();

            rig.Engine.Handle (Line (AppendixC.Open));            // not a message: nothing
            rig.Engine.Handle (Line (AppendixC.Keepalive));
            rig.Engine.Handle (Line (AppendixC.Warning));
            rig.Clock.Advance (TimeSpan.FromSeconds (300));
            rig.Engine.Handle (Line (AppendixC.Alarm));
            rig.Clock.Advance (TimeSpan.FromSeconds (500));
            rig.Engine.Handle (Line (AppendixC.Resolved));

            Assert.Equal ([AlertChangeKind.Raised, AlertChangeKind.Upgraded, AlertChangeKind.Resolved], rig.Changes.Select (c => c.Kind));
            Assert.Equal ([AlertSound.Warning, AlertSound.Alarm, AlertSound.AllClear], rig.Changes.Select (c => c.Sound));
            Assert.Empty (rig.Hub.Snapshot.Active);
            var episode = Assert.Single (rig.Hub.Snapshot.History);
            Assert.Equal (("Workshop", AlertStatus.Resolved, AlertLevel.Alarm, 50.6), (episode.Source, episode.Status, episode.Level, episode.Temperature));
        }

        [Fact]
        public void OpenAndKeepalive_ChangeNothing_AndReturnNull ()
        {
            var rig = new Rig ();

            Assert.Null (rig.Engine.Handle (Line (AppendixC.Open)));
            Assert.Null (rig.Engine.Handle (Line (AppendixC.Keepalive)));
            Assert.Empty (rig.Changes);
            Assert.Equal (0, rig.State.SaveCount);
        }

        [Fact]
        public void ADuplicate_IsNeitherAnnouncedNorSavedAgain ()
        {
            var rig = new Rig ();
            rig.Engine.Handle (Line (AppendixC.Warning));
            var saves = rig.State.SaveCount;

            var again = rig.Engine.Handle (Line (AppendixC.Warning));

            Assert.Equal (AlertChangeKind.Duplicate, again!.Kind);
            Assert.Single (rig.Changes);
            Assert.Equal (saves, rig.State.SaveCount);
        }

        [Fact]
        public void ARestart_RestoresWhatWasActive_AndWhereToResumeFrom ()
        {
            var first = new Rig ();
            first.Engine.Handle (Line (AppendixC.Warning));
            first.Engine.Handle (Line (AppendixC.Alarm));

            // A new process, the same saved state.
            var second = new Rig (first.State);

            var alarm = Assert.Single (second.Hub.Snapshot.Active);
            Assert.Equal (("Workshop", AlertLevel.Alarm), (alarm.Source, alarm.Level));
            Assert.Equal ("aB3dEi", second.Engine.LastMessageId);
            Assert.Empty (second.Changes);   // restoring is not news: nothing is announced, so nothing sounds
        }

        [Fact]
        public void AfterARestart_AReplayOfWhatWasAlreadySeen_DoesNothing ()
        {
            var first = new Rig ();
            first.Engine.Handle (Line (AppendixC.Warning));
            first.Engine.Handle (Line (AppendixC.Alarm));
            var second = new Rig (first.State);

            // The 12 hour replay on the next connect re-sends both, tagged as backlog.
            var warning = second.Engine.Handle (Line (AppendixC.Warning) with { Origin = MessageOrigin.Backlog });
            var alarm = second.Engine.Handle (Line (AppendixC.Alarm) with { Origin = MessageOrigin.Backlog });

            Assert.Equal ([AlertChangeKind.Duplicate, AlertChangeKind.Duplicate], [warning!.Kind, alarm!.Kind]);
            Assert.Empty (second.Changes);
        }

        [Fact]
        public void Acknowledging_IsAnnounced_SavedAndSilent ()
        {
            var rig = new Rig ();
            var alarm = rig.Engine.Handle (Line (AppendixC.Alarm))!.Alert!;
            rig.Changes.Clear ();

            var acknowledged = rig.Engine.Acknowledge (alarm.Id);

            Assert.Equal (AlertStatus.Acknowledged, acknowledged!.Status);
            var change = Assert.Single (rig.Changes);
            Assert.Equal ((AlertChangeKind.Acknowledged, AlertSound.None), (change.Kind, change.Sound));
            Assert.Equal (AlertStatus.Acknowledged, rig.Hub.Snapshot.Active[0].Status);
            Assert.Equal (AlertStatus.Acknowledged, rig.State.Saved!.Alerts[0].Status);   // and it survives a restart
        }

        [Fact]
        public void HandledAndUnknownIds ()
        {
            var rig = new Rig ();
            var alarm = rig.Engine.Handle (Line (AppendixC.Alarm))!.Alert!;

            Assert.Equal (AlertStatus.Handled, rig.Engine.MarkHandled (alarm.Id)!.Status);
            Assert.Null (rig.Engine.Acknowledge ("nope"));
            Assert.Null (rig.Engine.MarkHandled ("nope"));
        }

        [Fact]
        public void ASaveThatFails_IsReported_ButTheAlertIsStillShown ()
        {
            var rig = new Rig ();
            rig.State.ThrowOnSave = new IOException ("disk full");

            rig.Engine.Handle (Line (AppendixC.Alarm));

            Assert.Equal ("disk full", Assert.Single (rig.PersistFailures).Message);
            Assert.Single (rig.Changes);                      // the child still sees the alarm
            Assert.Single (rig.Hub.Snapshot.Active);
        }

        [Fact]
        public void ClearHistory_RemovesResolvedAlerts_AndSaves ()
        {
            var rig = new Rig ();
            rig.Engine.Handle (Line (AppendixC.Alarm));
            rig.Engine.Handle (Line (AppendixC.Resolved));
            Assert.Single (rig.Hub.Snapshot.History);

            rig.Changes.Clear ();
            rig.Engine.ClearHistory ();

            Assert.Empty (rig.Hub.Snapshot.History);
            Assert.Empty (rig.State.Saved!.Alerts);
            Assert.Equal ((AlertChangeKind.HistoryCleared, AlertSound.None), (Assert.Single (rig.Changes).Kind, rig.Changes[0].Sound));   // a screen showing the book must hear it
        }

        [Fact]
        public void NewInterpretationSettings_ApplyToTheMessagesThatFollow ()
        {
            var rig = new Rig ();
            rig.Engine.SetInterpreter (new AlertInterpreter (new InterpretationSettings { SourceSeparator = " is " }));

            var change = rig.Engine.Handle (Line ("""{"id":"z1","time":1790000100,"event":"message","topic":"t","priority":4,"title":"Cellar is warm","message":"m"}"""));

            Assert.Equal ("Cellar", change!.Alert!.Source);
        }

        [Fact]
        public void AnEngineWithNoPersistence_KeepsItsStateInMemoryOnly ()
        {
            var hub = new AlertHub ();
            var engine = new AlertEngine (new AlertStore (), hub, new TestClock (AppendixC.At (1790000100)));

            engine.Handle (Line (AppendixC.Alarm));

            Assert.Single (hub.Snapshot.Active);        // this is how Practice mode runs: nothing is written anywhere
        }
    }
}
