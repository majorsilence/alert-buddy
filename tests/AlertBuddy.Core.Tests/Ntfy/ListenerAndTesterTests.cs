using System.Net;
using System.Net.Sockets;
using System.Security.Authentication;
using AlertBuddy.Core.Ntfy;
using AlertBuddy.Core.Settings;
using AlertBuddy.Core.Store;
using AlertBuddy.TestSupport;
using Xunit;

namespace AlertBuddy.Core.Tests.Ntfy
{
    public class AlertListenerTests
    {
        private sealed class Rig : IAsyncDisposable
        {
            public FakeNtfyServer Server { get; } = new ();
            public TestClock Clock { get; } = new (AppendixC.At (1790000100));
            public AlertHub Hub { get; } = new ();
            public InMemorySettingsStore SettingsStore { get; } = new ();
            public InMemorySecretStore Secrets { get; } = new ();
            public SettingsService Settings { get; }
            public AlertEngine Engine { get; }
            public AlertListener Listener { get; }
            public List<Exception> Failures { get; } = [];

            public Rig (AppSettings? settings = null, InMemoryAlertStateStore? state = null)
            {
                SettingsStore.Current = settings ?? new AppSettings { ServerUrl = "https://ntfy.example.com", Topic = "home-alerts" };
                Settings = new SettingsService (SettingsStore);
                Engine = new AlertEngine (new AlertStore (), Hub, Clock, state);
                Listener = new AlertListener (Engine, Settings, Secrets, Server, Clock.Provider, Failures.Add);
            }

            public ValueTask DisposeAsync () => Listener.DisposeAsync ();
        }

        [Fact]
        public async Task NotSetUpYet_NothingIsContacted ()
        {
            await using var rig = new Rig (new AppSettings ());

            rig.Listener.Start ();

            Assert.False (rig.Listener.IsRunning);
            Assert.Empty (rig.Server.Requests);
        }

        [Theory]
        [InlineData ("not a url", "home-alerts")]
        [InlineData ("http://ntfy.example.com", "home-alerts")]       // plain http to a public host
        [InlineData ("https://ntfy.example.com", "has space")]
        public async Task InvalidSettings_AreReportedAsMisconfigured_AndNothingIsContacted (string url, string topic)
        {
            await using var rig = new Rig (new AppSettings { ServerUrl = url, Topic = topic });

            rig.Listener.Start ();

            Assert.False (rig.Listener.IsRunning);
            Assert.Equal ((ConnectionState.Misconfigured, ConnectionProblem.InvalidAddress), (rig.Hub.Snapshot.Connection.State, rig.Hub.Snapshot.Connection.Problem));
            Assert.Empty (rig.Server.Requests);
        }

        [Fact]
        public async Task HistoryAndLiveMessages_ReachTheEngine_AndTheConnectionReachesTheHub ()
        {
            await using var rig = new Rig ();
            rig.Server.EnqueuePoll (AppendixC.Warning);
            var stream = rig.Server.EnqueueStream ();

            rig.Listener.Start ();
            await stream.Connected;
            stream.Send (AppendixC.Alarm);
            await Wait.UntilAsync (() => rig.Hub.Snapshot.Active is [{ Level: Alerts.AlertLevel.Alarm }], "the alarm to arrive");

            Assert.True (rig.Listener.IsRunning);
            Assert.Equal (ConnectionState.Live, rig.Hub.Snapshot.Connection.State);
            var alert = Assert.Single (rig.Hub.Snapshot.Active);
            Assert.Equal ("Workshop", alert.Source);           // one card: the warning upgraded to the alarm
            Assert.Equal ("aB3dEi", rig.Engine.LastMessageId);
        }

        [Fact]
        public async Task ReplayedHistory_NeverMakesASound ()
        {
            await using var rig = new Rig ();
            // The snapshot is updated before the event is raised, so the events themselves are what to wait for.
            var sounds = new System.Collections.Concurrent.ConcurrentQueue<AlertSound> ();
            rig.Hub.AlertChanged += c => sounds.Enqueue (c.Sound);
            rig.Server.EnqueuePoll (AppendixC.Warning, AppendixC.Alarm);
            var stream = rig.Server.EnqueueStream ();

            rig.Listener.Start ();
            await stream.Connected;
            await Wait.UntilAsync (() => sounds.Count == 2, "both replayed messages to be announced");

            Assert.All (sounds, s => Assert.Equal (AlertSound.None, s));
        }

        [Fact]
        public async Task BasicSignIn_UsesTheUserNameFromSettings_AndThePasswordFromTheSecretStore ()
        {
            await using var rig = new Rig (new AppSettings { ServerUrl = "https://ntfy.example.com", Topic = "home-alerts", Auth = AuthMode.Basic, Username = "alice" });
            rig.Secrets.Set (SecretKeys.Password, "s3cret");
            rig.Server.EnqueuePoll ();
            var stream = rig.Server.EnqueueStream ();

            rig.Listener.Start ();
            await stream.Connected;

            Assert.All (rig.Server.Requests, r => Assert.Equal ("Basic YWxpY2U6czNjcmV0", r.Authorization));
        }

        [Fact]
        public async Task TokenSignIn_UsesTheTokenFromTheSecretStore ()
        {
            await using var rig = new Rig (new AppSettings { ServerUrl = "https://ntfy.example.com", Topic = "home-alerts", Auth = AuthMode.Token });
            rig.Secrets.Set (SecretKeys.Token, "tk_example");
            rig.Server.EnqueuePoll ();
            var stream = rig.Server.EnqueueStream ();

            rig.Listener.Start ();
            await stream.Connected;

            Assert.All (rig.Server.Requests, r => Assert.Equal ("Bearer tk_example", r.Authorization));
        }

        [Fact]
        public async Task AChangedPassword_AppliesAtTheNextReconnect_WithoutARestart ()
        {
            await using var rig = new Rig (new AppSettings { ServerUrl = "https://ntfy.example.com", Topic = "home-alerts", Auth = AuthMode.Basic, Username = "alice" });
            rig.Secrets.Set (SecretKeys.Password, "old");
            rig.Server.EnqueuePoll ();
            var first = rig.Server.EnqueueStream ();
            var second = rig.Server.EnqueueStream ();
            rig.Listener.Start ();
            await first.Connected;

            rig.Secrets.Set (SecretKeys.Password, "new");
            first.End ();
            await rig.Clock.AdvanceToNextAsync ();
            await second.Connected;

            Assert.Equal ("Basic " + Convert.ToBase64String ("alice:new"u8.ToArray ()), rig.Server.Requests[^1].Authorization);
        }

        [Fact]
        public async Task Restart_AppliesANewTopic ()
        {
            await using var rig = new Rig ();
            rig.Server.EnqueuePoll ();
            var first = rig.Server.EnqueueStream ();
            rig.Listener.Start ();
            await first.Connected;

            rig.Settings.Save (rig.Settings.Current with { Topic = "workshop-alerts" });
            rig.Server.EnqueuePoll ();
            var second = rig.Server.EnqueueStream ();
            await rig.Listener.RestartAsync ();
            await second.Connected;

            Assert.Equal ("/workshop-alerts/json", rig.Server.Requests[^1].Uri.AbsolutePath);
            Assert.True (rig.Listener.IsRunning);
        }

        [Fact]
        public async Task Stop_EndsTheListener_AndNothingMoreIsRequested ()
        {
            await using var rig = new Rig ();
            rig.Server.EnqueuePoll ();
            var stream = rig.Server.EnqueueStream ();
            rig.Listener.Start ();
            await stream.Connected;
            var requests = rig.Server.Requests.Count;

            await rig.Listener.StopAsync ();
            rig.Clock.Advance (TimeSpan.FromHours (1));

            Assert.False (rig.Listener.IsRunning);
            Assert.Equal (requests, rig.Server.Requests.Count);
        }

        [Fact]
        public async Task StartingTwice_DoesNotOpenTwoConnections ()
        {
            await using var rig = new Rig ();
            rig.Server.EnqueuePoll ();
            var stream = rig.Server.EnqueueStream ();

            rig.Listener.Start ();
            rig.Listener.Start ();
            await stream.Connected;

            Assert.Equal (2, rig.Server.Requests.Count);      // one history request and one stream, not four
        }

        [Fact]
        public async Task AnEngineThatThrowsOnOneMessage_DoesNotEndTheListener ()
        {
            var state = new InMemoryAlertStateStore { ThrowOnSave = new InvalidOperationException ("a bug while saving") };
            await using var rig = new Rig (state: state);
            rig.Server.EnqueuePoll ();
            var stream = rig.Server.EnqueueStream ();

            rig.Listener.Start ();
            await stream.Connected;
            stream.Send (AppendixC.Warning);
            await Wait.UntilAsync (() => rig.Failures.Count >= 1, "the failure to be reported");
            state.ThrowOnSave = null;
            stream.Send (AppendixC.Alarm);
            await Wait.UntilAsync (() => rig.Hub.Snapshot.Active.Count == 1, "the next message to be handled");

            Assert.Equal ("a bug while saving", rig.Failures[0].Message);
            Assert.True (rig.Listener.IsRunning);
        }
    }

    public class NtfyConnectionTesterTests
    {
        private static (NtfyConnectionTester Tester, FakeNtfyServer Server, TestClock Clock) Make ()
        {
            var server = new FakeNtfyServer ();
            var clock = new TestClock ();
            return (new NtfyConnectionTester (server, clock.Provider), server, clock);
        }

        private static Task<ConnectionTestResult> Test (NtfyConnectionTester tester, string url = "https://ntfy.example.com", string topic = "home-alerts", NtfyCredentials? creds = null, CancellationToken? ct = null)
            => tester.TestAsync (url, topic, creds ?? NtfyCredentials.None, ct ?? TestContext.Current.CancellationToken);

        [Fact]
        public async Task ASuccess_SaysSo_AndAsksForOnlyTheLastMinute ()
        {
            var (tester, server, _) = Make ();
            server.EnqueuePoll ();

            var result = await Test (tester, creds: NtfyCredentials.Basic ("alice", "s3cret"));

            Assert.True (result.Success);
            Assert.StartsWith ("Connected.", result.Message);
            var request = Assert.Single (server.Requests);
            Assert.True (request.IsPoll);
            Assert.Equal ("1m", request.Since);
            Assert.Equal ("Basic YWxpY2U6czNjcmV0", request.Authorization);
        }

        [Fact]
        public async Task PlainHttpOnAPrivateNetwork_SucceedsButSaysItIsNotEncrypted ()
        {
            var (tester, server, _) = Make ();
            server.EnqueuePoll ();

            var result = await Test (tester, "http://192.168.1.10:2586");

            Assert.True (result.Success);
            Assert.Contains ("not encrypted", result.Message);
        }

        [Theory]
        [InlineData (HttpStatusCode.Unauthorized, ConnectionProblem.Unauthorized, "sign-in")]
        [InlineData (HttpStatusCode.Forbidden, ConnectionProblem.Unauthorized, "sign-in")]
        [InlineData (HttpStatusCode.NotFound, ConnectionProblem.TopicNotFound, "topic")]
        [InlineData (HttpStatusCode.Redirect, ConnectionProblem.InvalidAddress, "redirect")]
        [InlineData (HttpStatusCode.TooManyRequests, ConnectionProblem.RateLimited, "slow down")]
        [InlineData (HttpStatusCode.ServiceUnavailable, ConnectionProblem.ServerError, "HTTP 503")]
        public async Task AFailureStatus_SaysWhatIsWrong_InWordsAGrownUpCanActOn (HttpStatusCode status, ConnectionProblem problem, string mentions)
        {
            var (tester, server, _) = Make ();
            server.EnqueueStatus (status);

            var result = await Test (tester);

            Assert.False (result.Success);
            Assert.Equal (problem, result.Problem);
            Assert.Contains (mentions, result.Message);
        }

        [Fact]
        public async Task ABadAddress_IsRefusedWithoutAnyRequest ()
        {
            var (tester, server, _) = Make ();

            var result = await Test (tester, "http://ntfy.example.com");

            Assert.False (result.Success);
            Assert.Equal (ConnectionProblem.InvalidAddress, result.Problem);
            Assert.Empty (server.Requests);
        }

        [Fact]
        public async Task ABadTopic_IsRefusedWithoutAnyRequest ()
        {
            var (tester, server, _) = Make ();

            var result = await Test (tester, topic: "has space");

            Assert.False (result.Success);
            Assert.Empty (server.Requests);
        }

        [Fact]
        public async Task ABadCertificate_SaysWhatFailed ()
        {
            var (tester, server, _) = Make ();
            server.EnqueueException (new HttpRequestException (HttpRequestError.SecureConnectionError, "ssl", new AuthenticationException ("The remote certificate has expired.")));

            var result = await Test (tester);

            Assert.Equal (ConnectionProblem.Certificate, result.Problem);
            Assert.Contains ("expired", result.Message);
        }

        [Fact]
        public async Task AServerThatIsDown_IsReportedAsUnreachable ()
        {
            var (tester, server, _) = Make ();
            server.EnqueueException (new HttpRequestException (HttpRequestError.ConnectionError, "refused", new SocketException ((int)SocketError.ConnectionRefused)));

            var result = await Test (tester);

            Assert.False (result.Success);
            Assert.Equal (ConnectionProblem.Unreachable, result.Problem);
            Assert.Contains ("Could not reach", result.Message);
        }

        [Fact]
        public async Task AServerThatNeverAnswers_IsGivenUpOnAfterFifteenSeconds ()
        {
            var (tester, server, clock) = Make ();
            server.EnqueueSilence ();

            var pending = Test (tester);
            await Wait.UntilAsync (() => server.Requests.Count == 1, "the request");
            await clock.SettleAsync ();
            Assert.Equal (TimeSpan.FromSeconds (15), await clock.NextDelayAsync ());
            clock.Advance (TimeSpan.FromSeconds (15));
            var result = await pending;

            Assert.Equal (ConnectionProblem.Silent, result.Problem);
            Assert.Contains ("did not answer", result.Message);
        }

        [Fact]
        public async Task CancellingTheTest_Cancels ()
        {
            var (tester, server, _) = Make ();
            server.EnqueueSilence ();
            using var cts = new CancellationTokenSource ();

            var pending = Test (tester, ct: cts.Token);
            await Wait.UntilAsync (() => server.Requests.Count == 1, "the request");
            await cts.CancelAsync ();

            await Assert.ThrowsAnyAsync<OperationCanceledException> (() => pending);
        }

        [Fact]
        public async Task NoSecret_AppearsInAnyMessage ()
        {
            foreach (var status in new[] { HttpStatusCode.Unauthorized, HttpStatusCode.ServiceUnavailable, HttpStatusCode.NotFound }) {
                var (tester, server, _) = Make ();
                server.EnqueueStatus (status);

                var result = await Test (tester, creds: NtfyCredentials.Basic ("alice", "hunter2-secret"));

                Assert.DoesNotContain ("hunter2", result.Message);
                Assert.DoesNotContain ("alice", result.Message);
            }
        }
    }
}
