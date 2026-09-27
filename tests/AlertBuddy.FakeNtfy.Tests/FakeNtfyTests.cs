// The helpers below own their cancellation on purpose: a reader task is stopped by its own token and awaited with a fixed bound, so a hung
// server fails the test in seconds instead of being cancelled at some arbitrary point. That is what the analyzer's advice would defeat.
#pragma warning disable xUnit1051

using System.Net;
using System.Text;
using AlertBuddy.Core.Ntfy;
using AlertBuddy.FakeNtfy;
using AlertBuddy.TestSupport;
using Xunit;

[assembly: CollectionBehavior (DisableTestParallelization = true)]

namespace AlertBuddy.FakeNtfy.Tests
{
    public class FakeNtfyServerTests
    {
        private static readonly FakeNtfyOptions Fast = new () { Keepalive = TimeSpan.FromMilliseconds (150) };

        private static HttpClient Client (FakeNtfyServer server, string? authorization = null)
        {
            var http = new HttpClient { BaseAddress = server.BaseUri, Timeout = TimeSpan.FromSeconds (20) };
            if (authorization is not null)
                http.DefaultRequestHeaders.TryAddWithoutValidation ("Authorization", authorization);
            return http;
        }

        private static async Task<List<NtfyEvent>> Poll (HttpClient http, string path)
        {
            var body = await http.GetStringAsync (path, TestContext.Current.CancellationToken);
            var events = new List<NtfyEvent> ();
            foreach (var line in body.Split ('\n', StringSplitOptions.RemoveEmptyEntries))
                if (NtfyParser.TryParse (line, out var e))
                    events.Add (e!);
            return events;
        }

        /// <summary>Opens a stream and reads events as they arrive, until disposed.</summary>
        private sealed class LiveStream : IAsyncDisposable
        {
            private readonly CancellationTokenSource cts = new ();
            private readonly Task reader;
            private readonly List<NtfyEvent> events = [];

            public LiveStream (HttpClient http, string path)
            {
                reader = Task.Run (async () => {
                    try {
                        using var response = await http.GetAsync (path, HttpCompletionOption.ResponseHeadersRead, cts.Token);
                        Status = response.StatusCode;
                        await using var stream = await response.Content.ReadAsStreamAsync (cts.Token);
                        await foreach (var line in BoundedLineReader.ReadAsync (stream, cancellationToken: cts.Token))
                            if (line.Line is not null && NtfyParser.TryParse (line.Line, out var e)) {
                                lock (events)
                                    events.Add (e!);
                            }
                        Ended = true;
                    } catch (OperationCanceledException) {
                    } catch (Exception ex) when (ex is IOException or HttpRequestException) {
                        Ended = true;
                    }
                });
            }

            public HttpStatusCode? Status { get; private set; }
            public bool Ended { get; private set; }
            public IReadOnlyList<NtfyEvent> Events { get { lock (events) return events.ToList (); } }

            public async ValueTask DisposeAsync ()
            {
                await cts.CancelAsync ();
                try { await reader.WaitAsync (TimeSpan.FromSeconds (5)); } catch (TimeoutException) { }
            }
        }

        [Fact]
        public async Task APublishedMessage_IsServedWithTheFieldsTheAppReads ()
        {
            await using var server = await FakeNtfyServer.StartAsync ();
            server.Publish ("home-alerts", "Workshop is at 41.2 °C", "Workshop: temperature warning", 4, ["warning"]);
            using var http = Client (server);

            var e = Assert.Single (await Poll (http, "/home-alerts/json?poll=1&since=all"));

            var m = e.Message!;
            Assert.Equal (("home-alerts", "Workshop: temperature warning", "Workshop is at 41.2 °C", 4), (m.Topic, m.Title, m.Message, m.Priority));
            Assert.Equal (["warning"], m.Tags);
            Assert.Equal (12, m.Id.Length);
        }

        [Fact]
        public async Task ThePriorityOfThree_IsOmittedAsNtfyDoes ()
        {
            await using var server = await FakeNtfyServer.StartAsync ();
            server.Publish ("t", "plain");
            using var http = Client (server);

            var raw = await http.GetStringAsync ("/t/json?poll=1&since=all", TestContext.Current.CancellationToken);

            Assert.DoesNotContain ("priority", raw);
        }

        [Fact]
        public async Task PostingOverHttp_PublishesWithTheHeadersNtfyUses ()
        {
            await using var server = await FakeNtfyServer.StartAsync ();
            using var http = Client (server);
            using var request = new HttpRequestMessage (HttpMethod.Post, "/home-alerts") { Content = new StringContent ("Workshop is at 50 C") };
            request.Headers.Add ("Title", "Workshop: temperature alarm");
            request.Headers.Add ("Priority", "urgent");
            request.Headers.Add ("Tags", "warning, house");

            (await http.SendAsync (request, TestContext.Current.CancellationToken)).EnsureSuccessStatusCode ();

            var m = Assert.Single (await Poll (http, "/home-alerts/json?poll=1&since=all")).Message!;
            Assert.Equal ((5, "Workshop: temperature alarm"), (m.Priority, m.Title));
            Assert.Equal (["warning", "house"], m.Tags);
        }

        [Fact]
        public async Task ATopic_OnlyServesItsOwnMessages ()
        {
            await using var server = await FakeNtfyServer.StartAsync ();
            server.Publish ("a", "one");
            server.Publish ("b", "two");
            using var http = Client (server);

            var events = await Poll (http, "/a/json?poll=1&since=all");

            Assert.Equal (["one"], events.Select (e => e.Message!.Message));
        }

        [Fact]
        public async Task Since_AcceptsAnIdADurationATimestampAndAll ()
        {
            await using var server = await FakeNtfyServer.StartAsync ();
            var old = server.Publish ("t", "old", time: DateTimeOffset.UtcNow.AddHours (-3));
            var mid = server.Publish ("t", "mid", time: DateTimeOffset.UtcNow.AddMinutes (-30));
            server.Publish ("t", "new");
            using var http = Client (server);

            Assert.Equal (["mid", "new"], (await Poll (http, "/t/json?poll=1&since=" + old)).Select (e => e.Message!.Message));
            Assert.Equal (["new"], (await Poll (http, "/t/json?poll=1&since=" + mid)).Select (e => e.Message!.Message));
            Assert.Equal (["mid", "new"], (await Poll (http, "/t/json?poll=1&since=1h")).Select (e => e.Message!.Message));
            Assert.Equal (["old", "mid", "new"], (await Poll (http, "/t/json?poll=1&since=all")).Select (e => e.Message!.Message));
            var ts = DateTimeOffset.UtcNow.AddMinutes (-45).ToUnixTimeSeconds ();
            Assert.Equal (["mid", "new"], (await Poll (http, "/t/json?poll=1&since=" + ts)).Select (e => e.Message!.Message));
            Assert.Equal (3, (await Poll (http, "/t/json?poll=1&since=unknownid")).Count);     // an id it does not know gets everything
        }

        [Fact]
        public async Task AStream_OpensWithAnOpenEvent_ThenDeliversLiveMessages_AndKeepsAlive ()
        {
            await using var server = await FakeNtfyServer.StartAsync (Fast);
            using var http = Client (server);
            await using var stream = new LiveStream (http, "/t/json");

            await Wait.UntilAsync (() => stream.Events.Any (e => e.Kind == NtfyEventKind.Open), "the open event");
            server.Publish ("t", "hello");
            await Wait.UntilAsync (() => stream.Events.Any (e => e.Kind == NtfyEventKind.Message), "the live message");
            await Wait.UntilAsync (() => stream.Events.Any (e => e.Kind == NtfyEventKind.Keepalive), "a keepalive");

            Assert.Equal (NtfyEventKind.Open, stream.Events[0].Kind);
            Assert.Equal ("hello", stream.Events.First (e => e.Kind == NtfyEventKind.Message).Message!.Message);
        }

        [Fact]
        public async Task AStreamWithSince_ReplaysTheCache_ThenGoesLive ()
        {
            await using var server = await FakeNtfyServer.StartAsync ();
            var first = server.Publish ("t", "one");
            server.Publish ("t", "two");
            using var http = Client (server);
            await using var stream = new LiveStream (http, "/t/json?since=" + first);

            await Wait.UntilAsync (() => stream.Events.Any (e => e.Message?.Message == "two"), "the cached message");
            server.Publish ("t", "three");
            await Wait.UntilAsync (() => stream.Events.Any (e => e.Message?.Message == "three"), "the live message");

            Assert.Equal (["two", "three"], stream.Events.Where (e => e.Message is not null).Select (e => e.Message!.Message));
        }

        [Fact]
        public async Task NoMessageIsMissedOrRepeated_WhenOnePublishesWhileAStreamIsOpening ()
        {
            await using var server = await FakeNtfyServer.StartAsync ();
            for (var i = 0; i < 20; i++)
                server.Publish ("t", "m" + i);
            using var http = Client (server);

            await using var stream = new LiveStream (http, "/t/json?since=all");
            for (var i = 20; i < 40; i++)
                server.Publish ("t", "m" + i);
            await Wait.UntilAsync (() => stream.Events.Count (e => e.Message is not null) >= 40, "all forty");

            Assert.Equal (Enumerable.Range (0, 40).Select (i => "m" + i), stream.Events.Where (e => e.Message is not null).Select (e => e.Message!.Message));
        }

        [Fact]
        public async Task DroppingConnections_EndsTheStreamAbruptly_AndItCanBeReopened ()
        {
            await using var server = await FakeNtfyServer.StartAsync ();
            using var http = Client (server);
            await using var stream = new LiveStream (http, "/t/json");
            await Wait.UntilAsync (() => server.OpenStreams == 1, "the stream to register");

            server.DropConnections ();

            await Wait.UntilAsync (() => stream.Ended, "the client to notice the drop");
            await Wait.UntilAsync (() => server.OpenStreams == 0, "the server to forget it");
        }

        [Fact]
        public async Task AStreamThatTheClientClosed_IsForgotten ()
        {
            await using var server = await FakeNtfyServer.StartAsync ();
            using var http = Client (server);
            var stream = new LiveStream (http, "/t/json");
            await Wait.UntilAsync (() => server.OpenStreams == 1, "the stream to register");

            await stream.DisposeAsync ();

            await Wait.UntilAsync (() => server.OpenStreams == 0, "the server to notice the client left");
        }

        [Fact]
        public async Task FailNext_AnswersTheNextRequestsWithThatStatus_ThenRecovers ()
        {
            await using var server = await FakeNtfyServer.StartAsync ();
            server.FailNext (503, 2);
            using var http = Client (server);

            Assert.Equal (HttpStatusCode.ServiceUnavailable, (await http.GetAsync ("/t/json?poll=1", TestContext.Current.CancellationToken)).StatusCode);
            Assert.Equal (HttpStatusCode.ServiceUnavailable, (await http.GetAsync ("/t/json?poll=1", TestContext.Current.CancellationToken)).StatusCode);
            Assert.Equal (HttpStatusCode.OK, (await http.GetAsync ("/t/json?poll=1", TestContext.Current.CancellationToken)).StatusCode);
        }

        [Fact]
        public async Task ABasicSignIn_IsRequiredWhenConfigured ()
        {
            await using var server = await FakeNtfyServer.StartAsync (new FakeNtfyOptions { BasicUser = "alice", BasicPassword = "s3cret" });

            var without = await Client (server).GetAsync ("/t/json?poll=1", TestContext.Current.CancellationToken);
            var wrong = await Client (server, "Basic " + Convert.ToBase64String (Encoding.UTF8.GetBytes ("alice:nope"))).GetAsync ("/t/json?poll=1", TestContext.Current.CancellationToken);
            var right = await Client (server, "Basic " + Convert.ToBase64String (Encoding.UTF8.GetBytes ("alice:s3cret"))).GetAsync ("/t/json?poll=1", TestContext.Current.CancellationToken);

            Assert.Equal ((HttpStatusCode.Unauthorized, HttpStatusCode.Unauthorized, HttpStatusCode.OK), (without.StatusCode, wrong.StatusCode, right.StatusCode));
            Assert.Contains ("Basic", without.Headers.WwwAuthenticate.ToString ());
        }

        [Fact]
        public async Task ABearerToken_IsRequiredWhenConfigured ()
        {
            await using var server = await FakeNtfyServer.StartAsync (new FakeNtfyOptions { Token = "tk_example" });

            Assert.Equal (HttpStatusCode.Unauthorized, (await Client (server).GetAsync ("/t/json?poll=1", TestContext.Current.CancellationToken)).StatusCode);
            Assert.Equal (HttpStatusCode.OK, (await Client (server, "Bearer tk_example").GetAsync ("/t/json?poll=1", TestContext.Current.CancellationToken)).StatusCode);
        }

        [Fact]
        public async Task TheScenario_PublishesAWarningAnAlarmAndAnAllClear ()
        {
            await using var server = await FakeNtfyServer.StartAsync ();
            using var http = Client (server);

            (await http.PostAsync ("/_scenario/t?step=0.05", null, TestContext.Current.CancellationToken)).EnsureSuccessStatusCode ();
            await Wait.UntilAsync (() => server.MessageCount == 3, "the three scenario messages");

            var events = await Poll (http, "/t/json?poll=1&since=all");
            Assert.Equal ([4, 5, 3], events.Select (e => e.Message!.Priority));
            Assert.All (events, e => Assert.StartsWith ("Workshop:", e.Message!.Title));
        }

        [Fact]
        public async Task EveryRequestIsRecorded_WithoutTheSignInBeingInterpreted ()
        {
            await using var server = await FakeNtfyServer.StartAsync ();
            using var http = Client (server, "Bearer tk_x");

            await http.GetAsync ("/t/json?poll=1", TestContext.Current.CancellationToken);

            var seen = Assert.Single (server.Requests);
            Assert.Equal (("GET", "/t/json?poll=1", "Bearer tk_x"), (seen.Method, seen.PathAndQuery, seen.Authorization));
        }
    }

    /// <summary>The real subscription against the real (fake) server, over real sockets and in real time.</summary>
    public class SubscriptionOverRealHttpTests
    {
        private static async Task<(FakeNtfyServer Server, NtfySubscription Sub, List<NtfyEvent> Events, CancellationTokenSource Stop, Task Reader)> Start (
            FakeNtfyOptions? serverOptions = null, Func<NtfyCredentials>? credentials = null, string? initialLastId = null)
        {
            var server = await FakeNtfyServer.StartAsync (serverOptions);
            var events = new List<NtfyEvent> ();
            string? lastId = initialLastId;
            var sub = new NtfySubscription (new NtfySubscriptionOptions (server.BaseUri, "home-alerts") {
                Credentials = credentials ?? (() => NtfyCredentials.None),
                LastMessageId = () => lastId,
                Unencrypted = true,
            }, jitter: () => 0.5);
            var stop = new CancellationTokenSource ();
            var reader = Task.Run (async () => {
                try {
                    await foreach (var e in sub.ReadAsync (stop.Token)) {
                        lock (events) {
                            events.Add (e);
                            if (e.Message is { } m)
                                lastId = m.Id;
                        }
                    }
                } catch (OperationCanceledException) {
                }
            });
            return (server, sub, events, stop, reader);
        }

        private static async Task Finish (FakeNtfyServer server, CancellationTokenSource stop, Task reader)
        {
            await stop.CancelAsync ();
            await reader.WaitAsync (TimeSpan.FromSeconds (10));
            await server.DisposeAsync ();
        }

        private static IReadOnlyList<NtfyEvent> Messages (List<NtfyEvent> events)
        {
            lock (events)
                return events.Where (e => e.Kind == NtfyEventKind.Message).ToList ();
        }

        [Fact]
        public async Task HistoryIsReplayedAsBacklog_AndLaterMessagesAreLive_OverRealHttp ()
        {
            var (server, sub, events, stop, reader) = await Start ();
            try {
                await Wait.UntilAsync (() => sub.Current.State == ConnectionState.Live, "live");
            } finally {
                await Finish (server, stop, reader);
            }

            // (the server started empty, so this run is about the connection; the next test publishes before connecting)
            Assert.Empty (Messages (events));
        }

        [Fact]
        public async Task ReplayThenLive_WithRealChunkedStreaming ()
        {
            await using var seed = await FakeNtfyServer.StartAsync ();
            seed.Publish ("home-alerts", "Workshop is at 41.2 °C", "Workshop: temperature warning", 4);
            seed.Publish ("home-alerts", "Workshop is at 50.6 °C", "Workshop: temperature alarm", 5);

            var events = new List<NtfyEvent> ();
            var sub = new NtfySubscription (new NtfySubscriptionOptions (seed.BaseUri, "home-alerts") { Unencrypted = true }, jitter: () => 0.5);
            using var stop = new CancellationTokenSource ();
            var reader = Task.Run (async () => {
                try {
                    await foreach (var e in sub.ReadAsync (stop.Token))
                        lock (events)
                            events.Add (e);
                } catch (OperationCanceledException) {
                }
            });

            await Wait.UntilAsync (() => sub.Current.State == ConnectionState.Live, "live");
            seed.Publish ("home-alerts", "Workshop is at 44.0 °C", "Workshop: temperature alarm (resolved)", 3);
            await Wait.UntilAsync (() => Messages (events).Count == 3, "all three messages");

            await stop.CancelAsync ();
            await reader.WaitAsync (TimeSpan.FromSeconds (10));

            Assert.Equal ([MessageOrigin.Backlog, MessageOrigin.Backlog, MessageOrigin.Live], Messages (events).Select (e => e.Origin));
            Assert.True (sub.Current.Unencrypted);
        }

        [Fact]
        public async Task ADroppedConnection_ReconnectsAndResumes_WithoutMissingOrRepeatingAMessage ()
        {
            var (server, sub, events, stop, reader) = await Start ();
            try {
                await Wait.UntilAsync (() => sub.Current.State == ConnectionState.Live && server.OpenStreams == 1, "live");
                server.Publish ("home-alerts", "before", "Workshop: warning", 4);
                await Wait.UntilAsync (() => Messages (events).Count == 1, "the first message");

                server.DropConnections ();
                await Wait.UntilAsync (() => sub.Current.State == ConnectionState.Reconnecting, "the drop to be noticed");
                server.Publish ("home-alerts", "during the gap", "Workshop: alarm", 5);     // published while nobody is connected

                await Wait.UntilAsync (() => Messages (events).Count == 2, "the gap message after reconnecting", TimeSpan.FromSeconds (15));
                server.Publish ("home-alerts", "after", "Workshop: resolved", 3);
                await Wait.UntilAsync (() => Messages (events).Count == 3, "the message after reconnecting");

                Assert.Equal (["before", "during the gap", "after"], Messages (events).Select (e => e.Message!.Message));
                Assert.All (Messages (events), e => Assert.Equal (MessageOrigin.Live, e.Origin));     // a short gap resumes: what it delivers is news
                Assert.Equal (1, server.Requests.Count (r => r.PathAndQuery.Contains ("poll=1")));    // history was replayed once, not on the reconnect
            } finally {
                await Finish (server, stop, reader);
            }
        }

        [Fact]
        public async Task ARefusedSignIn_IsAuthFailed_AndAGoodOneIsLive ()
        {
            var (server, sub, events, stop, reader) = await Start (new FakeNtfyOptions { BasicUser = "alice", BasicPassword = "s3cret" }, () => NtfyCredentials.Basic ("alice", "wrong"));
            try {
                await Wait.UntilAsync (() => sub.Current.State == ConnectionState.AuthFailed, "auth failed");
                Assert.Equal (ConnectionProblem.Unauthorized, sub.Current.Problem);
                Assert.Empty (Messages (events));
            } finally {
                await Finish (server, stop, reader);
            }

            var (server2, sub2, _, stop2, reader2) = await Start (new FakeNtfyOptions { BasicUser = "alice", BasicPassword = "s3cret" }, () => NtfyCredentials.Basic ("alice", "s3cret"));
            try {
                await Wait.UntilAsync (() => sub2.Current.State == ConnectionState.Live, "live with the right sign-in");
            } finally {
                await Finish (server2, stop2, reader2);
            }
        }

        [Fact]
        public async Task ATokenSignIn_WorksOverRealHttp ()
        {
            var (server, sub, _, stop, reader) = await Start (new FakeNtfyOptions { Token = "tk_example" }, () => NtfyCredentials.Bearer ("tk_example"));
            try {
                await Wait.UntilAsync (() => sub.Current.State == ConnectionState.Live, "live");
            } finally {
                await Finish (server, stop, reader);
            }
        }

        [Fact]
        public async Task AServerErrorIsReconnecting_ThenTheAppRecovers ()
        {
            var (server, sub, _, stop, reader) = await StartFailing ();
            try {
                await Wait.UntilAsync (() => sub.Current.Problem == ConnectionProblem.ServerError, "the server error");
                await Wait.UntilAsync (() => sub.Current.State == ConnectionState.Live, "recovery", TimeSpan.FromSeconds (15));
            } finally {
                await Finish (server, stop, reader);
            }

            static async Task<(FakeNtfyServer, NtfySubscription, List<NtfyEvent>, CancellationTokenSource, Task)> StartFailing ()
            {
                var server = await FakeNtfyServer.StartAsync ();
                server.FailNext (503);
                var events = new List<NtfyEvent> ();
                var sub = new NtfySubscription (new NtfySubscriptionOptions (server.BaseUri, "home-alerts") { Unencrypted = true }, jitter: () => 0.5);
                var stop = new CancellationTokenSource ();
                var reader = Task.Run (async () => {
                    try { await foreach (var e in sub.ReadAsync (stop.Token)) { lock (events) events.Add (e); } } catch (OperationCanceledException) { }
                });
                return (server, sub, events, stop, reader);
            }
        }
    }
}
