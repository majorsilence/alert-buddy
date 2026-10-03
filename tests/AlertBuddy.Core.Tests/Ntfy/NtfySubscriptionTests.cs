using System.Net;
using System.Net.Sockets;
using System.Security.Authentication;
using AlertBuddy.Core.Ntfy;
using AlertBuddy.TestSupport;
using Xunit;

namespace AlertBuddy.Core.Tests.Ntfy
{
    public class NtfySubscriptionTests
    {
        private static HttpRequestException Refused () => new (HttpRequestError.ConnectionError, "refused", new SocketException ((int)SocketError.ConnectionRefused));

        // ---- replay first, silently; then live ----

        [Fact]
        public async Task TheFirstConnection_ReplaysHistoryAsBacklog_ThenStreamsNewMessagesAsLive ()
        {
            await using var rig = new SubscriptionRig ();
            var stream = await rig.ConnectedStreamAsync (AppendixC.Warning, AppendixC.Alarm);

            stream.Send (SubscriptionRig.Message ("live1"));
            await rig.MessagesAsync (3);

            Assert.Equal ([MessageOrigin.Backlog, MessageOrigin.Backlog, MessageOrigin.Live], rig.Messages.Select (m => m.Origin));
            Assert.Equal (["aB3dEh", "aB3dEi", "live1"], rig.Messages.Select (m => m.Message!.Id));
            Assert.Equal (ConnectionState.Live, rig.Current.State);
        }

        [Fact]
        public async Task HistoryIsAskedForAsAFiniteRequest_AndTheLiveStreamResumesAfterTheNewestReplayedMessage ()
        {
            await using var rig = new SubscriptionRig ();
            await rig.ConnectedStreamAsync (AppendixC.Warning, AppendixC.Alarm);

            var replay = rig.Server.Requests[0];
            var live = rig.Server.Requests[1];

            Assert.True (replay.IsPoll);
            Assert.Equal ("720m", replay.Since);                                  // the 12 hour replay window
            Assert.Equal ("/home-alerts/json", replay.Uri.AbsolutePath);
            Assert.False (live.IsPoll);
            Assert.Equal ("aB3dEi", live.Since);                                  // after the newest replayed message, so nothing is missed or repeated
        }

        [Fact]
        public async Task WithNoHistory_TheLiveStreamStartsFromWhenTheHistoryRequestBegan ()
        {
            await using var rig = new SubscriptionRig ();
            var startedAt = rig.Clock.Now;
            await rig.ConnectedStreamAsync ();

            // A message published between the two requests would be lost with no anchor, so the stream is anchored to the moment the
            // history request began, less 30 seconds for clock differences; anything that arrives twice is dropped as a duplicate.
            Assert.Equal (startedAt.AddSeconds (-30).ToUnixTimeSeconds ().ToString (), rig.Server.Requests[1].Since);
        }

        [Fact]
        public async Task OnlyMessagesAreBacklog_TheReplayIgnoresOpenKeepaliveAndStrayLines ()
        {
            await using var rig = new SubscriptionRig ();
            await rig.ConnectedStreamAsync (AppendixC.Open, AppendixC.Keepalive, "garbage", AppendixC.Warning);
            await rig.MessagesAsync (1);     // the stream is read on another thread: wait for the message, never assume it has arrived

            Assert.Equal (["aB3dEh"], rig.Messages.Select (m => m.Message!.Id));
            Assert.DoesNotContain (rig.Events, e => e.Kind is NtfyEventKind.Open or NtfyEventKind.Keepalive && e.Origin == MessageOrigin.Backlog);
        }

        [Fact]
        public async Task AShortGap_ResumesFromTheLastMessage_AndWhatItDeliversIsNews ()
        {
            await using var rig = new SubscriptionRig ();
            var first = await rig.ConnectedStreamAsync (AppendixC.Warning, AppendixC.Alarm);
            await rig.MessagesAsync (2);
            var second = rig.Server.EnqueueStream ();

            first.End ();
            await rig.Clock.AdvanceToNextAsync ();          // the one second backoff
            await second.Connected;
            second.Send (SubscriptionRig.Message ("missed1"));
            await rig.MessagesAsync (3);

            var resume = rig.Server.Requests[2];
            Assert.False (resume.IsPoll);                    // no second replay for a short gap
            Assert.Equal ("aB3dEi", resume.Since);
            Assert.Equal (MessageOrigin.Live, rig.Messages[2].Origin);
        }

        [Fact]
        public async Task ALongGap_ReplaysHistoryAgain_Silently ()
        {
            await using var rig = new SubscriptionRig (o => o with { LongGap = TimeSpan.FromMinutes (2) });
            var first = await rig.ConnectedStreamAsync (AppendixC.Warning);
            await rig.MessagesAsync (1);
            rig.Server.WhenEmpty = (_, _) => throw Refused ();

            first.End ();
            var lostAt = rig.Clock.Now;
            while (rig.Clock.Now - lostAt <= TimeSpan.FromMinutes (2.5))
                await rig.Clock.AdvanceToNextAsync ();       // the server stays unreachable long enough to count as a long gap

            rig.Server.WhenEmpty = null;
            rig.Server.EnqueuePoll (AppendixC.Alarm);
            var second = rig.Server.EnqueueStream ();
            await rig.Clock.AdvanceToNextAsync ();
            await second.Connected;
            await rig.MessagesAsync (2);

            var requests = rig.Server.Requests;
            Assert.True (requests[^2].IsPoll);
            Assert.Equal ("720m", requests[^2].Since);
            Assert.Equal (MessageOrigin.Backlog, rig.Messages[1].Origin);   // what it brings back is history, so it never sounds
        }

        [Fact]
        public async Task AShortGap_WithNoMessageEverSeen_AsksForWhatArrivedSinceItWasLastHeard ()
        {
            await using var rig = new SubscriptionRig ();
            var first = await rig.ConnectedStreamAsync ();
            var second = rig.Server.EnqueueStream ();

            first.End ();
            await rig.Clock.AdvanceToNextAsync ();
            await second.Connected;

            // No id to resume from, so the request covers the gap: one second plus a minute's margin, rounded up to whole minutes.
            Assert.Equal ("2m", rig.Server.Requests[2].Since);
        }

        // ---- authentication and identity ----

        [Fact]
        public async Task TheSignIn_IsSentOnEveryRequest ()
        {
            await using var rig = new SubscriptionRig ();
            rig.Credentials = NtfyCredentials.Basic ("alice", "s3cret");
            await rig.ConnectedStreamAsync ();

            Assert.All (rig.Server.Requests, r => Assert.Equal ("Basic YWxpY2U6czNjcmV0", r.Authorization));
            Assert.All (rig.Server.Requests, r => Assert.Equal ("AlertBuddy/1", r.UserAgent));
        }

        [Fact]
        public async Task WithNoSignIn_NoAuthorizationHeaderIsSent ()
        {
            await using var rig = new SubscriptionRig ();
            await rig.ConnectedStreamAsync ();

            Assert.All (rig.Server.Requests, r => Assert.Null (r.Authorization));
        }

        [Fact]
        public async Task AChangedPassword_AppliesOnTheNextReconnect_WithoutRestarting ()
        {
            await using var rig = new SubscriptionRig ();
            rig.Credentials = NtfyCredentials.Bearer ("old-token");
            var first = await rig.ConnectedStreamAsync ();
            var second = rig.Server.EnqueueStream ();

            rig.Credentials = NtfyCredentials.Bearer ("new-token");
            first.End ();
            await rig.Clock.AdvanceToNextAsync ();
            await second.Connected;

            Assert.Equal ("Bearer new-token", rig.Server.Requests[^1].Authorization);
        }

        [Theory]
        [InlineData ("https://ntfy.example.com", "/home-alerts/json")]
        [InlineData ("https://ntfy.example.com/ntfy", "/ntfy/home-alerts/json")]
        [InlineData ("https://ntfy.example.com:8443/a/b", "/a/b/home-alerts/json")]
        public async Task TheUrl_IsTheBaseThenTheTopicThenJson (string baseAddress, string path)
        {
            await using var rig = new SubscriptionRig (baseAddress: baseAddress);
            await rig.ConnectedStreamAsync ();

            Assert.All (rig.Server.Requests, r => Assert.Equal (path, r.Uri.AbsolutePath));
            Assert.All (rig.Server.Requests, r => Assert.Equal (new Uri (baseAddress).Authority, r.Uri.Authority));
        }

        // ---- the backoff ----

        [Fact]
        public async Task ReconnectDelays_AreOneTwoFourEightSixteenThirtyTwoThenSixtySeconds ()
        {
            await using var rig = new SubscriptionRig ();
            rig.Server.WhenEmpty = (_, _) => throw Refused ();
            rig.Start ();

            var delays = new List<double> ();
            for (var i = 0; i < 9; i++) {
                delays.Add ((await rig.Clock.AdvanceToNextAsync ()).TotalSeconds);
                await rig.RequestsAsync (i + 2);
            }

            Assert.Equal ([1, 2, 4, 8, 16, 32, 60, 60, 60], delays);
        }

        [Fact]
        public async Task TheBackoff_StartsAgainAfterAConnectionThatStayedHealthy ()
        {
            await using var rig = new SubscriptionRig ();
            rig.Server.EnqueueException (Refused ());
            rig.Server.EnqueueException (Refused ());
            rig.Server.EnqueueException (Refused ());
            rig.Server.EnqueuePoll ();
            var stream = rig.Server.EnqueueStream ();
            rig.Start ();

            await rig.Clock.AdvanceToNextAsync ();     // 1 s
            await rig.RequestsAsync (2);
            await rig.Clock.AdvanceToNextAsync ();     // 2 s
            await rig.RequestsAsync (3);
            await rig.Clock.AdvanceToNextAsync ();     // 4 s
            await stream.Connected;
            await rig.Clock.SettleAsync ();

            rig.Clock.Advance (TimeSpan.FromSeconds (31));      // healthy for 30 seconds or more
            rig.Server.WhenEmpty = (_, _) => throw Refused ();
            stream.End ();

            // Three failures had built the delay up to 8 seconds; a healthy connection earns a fresh start at 1.
            Assert.Equal (TimeSpan.FromSeconds (1), await rig.Clock.NextDelayAsync ());
        }

        [Fact]
        public async Task TheBackoff_KeepsGrowing_WhenAConnectionDropsBeforeItWasHealthy ()
        {
            await using var rig = new SubscriptionRig ();
            rig.Server.EnqueueException (Refused ());
            rig.Server.EnqueueException (Refused ());
            rig.Server.EnqueueException (Refused ());
            rig.Server.EnqueuePoll ();
            var stream = rig.Server.EnqueueStream ();
            rig.Start ();

            await rig.Clock.AdvanceToNextAsync ();
            await rig.RequestsAsync (2);
            await rig.Clock.AdvanceToNextAsync ();
            await rig.RequestsAsync (3);
            await rig.Clock.AdvanceToNextAsync ();
            await stream.Connected;
            await rig.Clock.SettleAsync ();

            rig.Clock.Advance (TimeSpan.FromSeconds (29));      // one second short of healthy
            rig.Server.WhenEmpty = (_, _) => throw Refused ();
            stream.End ();

            Assert.Equal (TimeSpan.FromSeconds (8), await rig.Clock.NextDelayAsync ());
        }

        // ---- the watchdog ----

        [Fact]
        public async Task ASilentConnection_IsDroppedAfter110Seconds_AndKeepalivesKeepItAlive ()
        {
            await using var rig = new SubscriptionRig ();
            var stream = await rig.ConnectedStreamAsync ();
            await rig.Clock.SettleAsync ();

            // The only timer waiting is the watchdog, and it is exactly the plan's 110 seconds.
            Assert.Equal (TimeSpan.FromSeconds (110), await rig.Clock.NextDelayAsync ());

            rig.Clock.Advance (TimeSpan.FromSeconds (100));
            stream.Send (AppendixC.Keepalive);                                     // a keepalive restarts the clock
            await Wait.UntilAsync (() => rig.Events.Any (e => e.Kind == NtfyEventKind.Keepalive), "the keepalive to arrive");
            Assert.Equal (TimeSpan.FromSeconds (110), await rig.Clock.NextDelayAsync ());
            Assert.Equal (ConnectionState.Live, rig.Current.State);

            rig.Clock.Advance (TimeSpan.FromSeconds (109));
            await rig.Clock.SettleAsync ();
            Assert.Equal (ConnectionState.Live, rig.Current.State);                // one second before the deadline: still up

            rig.Clock.Advance (TimeSpan.FromSeconds (1));
            await rig.StateAsync (ConnectionState.Reconnecting, "the watchdog to drop the connection");
            Assert.Equal (ConnectionProblem.Silent, rig.Current.Problem);
        }

        [Fact]
        public async Task AfterTheWatchdogFires_TheAppReconnects ()
        {
            await using var rig = new SubscriptionRig ();
            await rig.ConnectedStreamAsync ();
            var next = rig.Server.EnqueueStream ();
            await rig.Clock.SettleAsync ();

            rig.Clock.Advance (TimeSpan.FromSeconds (110));
            await rig.StateAsync (ConnectionState.Reconnecting, "the drop");
            await rig.Clock.AdvanceToNextAsync ();
            await next.Connected;

            await rig.StateAsync (ConnectionState.Live, "the app to be live again");
        }

        // ---- refused, not found, throttled, broken ----

        [Theory]
        [InlineData (HttpStatusCode.Unauthorized)]
        [InlineData (HttpStatusCode.Forbidden)]
        public async Task ARefusedSignIn_IsAuthFailed_RetriedEveryFiveMinutes_WithoutFlickering (HttpStatusCode status)
        {
            await using var rig = new SubscriptionRig ();
            rig.Credentials = NtfyCredentials.Basic ("alice", "wrong");
            rig.Server.EnqueueStatus (status);
            rig.Server.EnqueueStatus (status);
            rig.Server.EnqueueStatus (status);
            rig.Start ();

            await rig.StateAsync (ConnectionState.AuthFailed, "auth failed");
            Assert.Equal (ConnectionProblem.Unauthorized, rig.Current.Problem);
            Assert.Equal (TimeSpan.FromMinutes (5), await rig.Clock.NextDelayAsync ());

            rig.Clock.Advance (TimeSpan.FromMinutes (5) - TimeSpan.FromSeconds (1));
            await rig.Clock.SettleAsync ();
            Assert.Single (rig.Server.Requests);                                  // not a fast loop

            rig.Clock.Advance (TimeSpan.FromSeconds (1));
            await rig.RequestsAsync (2);
            await rig.Clock.AdvanceToNextAsync ();
            await rig.RequestsAsync (3);

            // Through three attempts the banner said AuthFailed and never anything else.
            Assert.Equal ([ConnectionState.AuthFailed], rig.States.Select (s => s.State).Distinct ());
        }

        [Fact]
        public async Task SlowRetries_DoNotInflateTheBackoff_ForTheNextNetworkBlip ()
        {
            await using var rig = new SubscriptionRig ();
            rig.Server.EnqueueStatus (HttpStatusCode.Unauthorized);
            rig.Server.EnqueueStatus (HttpStatusCode.Unauthorized);
            rig.Server.EnqueueStatus (HttpStatusCode.Unauthorized);
            rig.Server.EnqueueException (Refused ());
            rig.Start ();

            for (var i = 2; i <= 4; i++) {
                await rig.Clock.AdvanceToNextAsync ();      // five minutes each time
                await rig.RequestsAsync (i);
            }

            // Three refused sign-ins over fifteen minutes, then one refused connection. The blip is the FIRST failure of its kind, so it
            // retries in one second; counting the slow retries as failures would make it wait four.
            await rig.StateAsync (ConnectionState.Reconnecting, "the blip");
            Assert.Equal (TimeSpan.FromSeconds (1), await rig.Clock.NextDelayAsync ());
        }

        [Fact]
        public async Task AfterASignInIsFixed_TheAppBecomesLive ()
        {
            await using var rig = new SubscriptionRig ();
            rig.Server.EnqueueStatus (HttpStatusCode.Unauthorized);
            rig.Server.EnqueuePoll ();
            var stream = rig.Server.EnqueueStream ();
            rig.Start ();

            await rig.StateAsync (ConnectionState.AuthFailed, "auth failed");
            await rig.Clock.AdvanceToNextAsync ();
            await stream.Connected;

            await rig.StateAsync (ConnectionState.Live, "live once the sign-in works");
        }

        [Fact]
        public async Task ATopicThatDoesNotExist_IsMisconfigured_RetriedSlowly ()
        {
            await using var rig = new SubscriptionRig ();
            rig.Server.EnqueueStatus (HttpStatusCode.NotFound);
            rig.Start ();

            await rig.StateAsync (ConnectionState.Misconfigured, "misconfigured");

            Assert.Equal (ConnectionProblem.TopicNotFound, rig.Current.Problem);
            Assert.Equal (TimeSpan.FromMinutes (5), await rig.Clock.NextDelayAsync ());
        }

        [Fact]
        public async Task ARedirect_IsRefusedNotFollowed_BecauseTheSignInMustNotTravel ()
        {
            await using var rig = new SubscriptionRig ();
            rig.Server.EnqueueStatus (HttpStatusCode.Redirect);
            rig.Start ();

            await rig.StateAsync (ConnectionState.Misconfigured, "misconfigured");

            Assert.Equal (ConnectionProblem.InvalidAddress, rig.Current.Problem);
            Assert.Single (rig.Server.Requests);
        }

        [Fact]
        public async Task TooManyRequests_HonoursRetryAfter ()
        {
            await using var rig = new SubscriptionRig ();
            rig.Server.EnqueueStatus (HttpStatusCode.TooManyRequests, TimeSpan.FromSeconds (90));
            rig.Start ();

            await rig.StateAsync (ConnectionState.Reconnecting, "reconnecting");

            Assert.Equal (ConnectionProblem.RateLimited, rig.Current.Problem);
            Assert.Equal (TimeSpan.FromSeconds (90), await rig.Clock.NextDelayAsync ());
        }

        [Fact]
        public async Task TooManyRequests_WithNoRetryAfter_BacksOffAnyway ()
        {
            await using var rig = new SubscriptionRig ();
            rig.Server.EnqueueStatus (HttpStatusCode.TooManyRequests);
            rig.Start ();

            await rig.StateAsync (ConnectionState.Reconnecting, "reconnecting");

            Assert.Equal (TimeSpan.FromSeconds (8), await rig.Clock.NextDelayAsync ());
        }

        [Fact]
        public async Task AServerError_IsReconnecting_NamingTheStatus ()
        {
            await using var rig = new SubscriptionRig ();
            rig.Server.EnqueueStatus (HttpStatusCode.ServiceUnavailable);
            rig.Start ();

            await rig.StateAsync (ConnectionState.Reconnecting, "reconnecting");

            Assert.Equal ((ConnectionProblem.ServerError, "HTTP 503"), (rig.Current.Problem, rig.Current.Detail));
            Assert.Equal (TimeSpan.FromSeconds (1), await rig.Clock.NextDelayAsync ());
        }

        [Fact]
        public async Task ABadCertificate_IsMisconfigured_SaysWhatFailed_AndIsRetriedSlowly ()
        {
            await using var rig = new SubscriptionRig ();
            rig.Server.EnqueueException (new HttpRequestException (HttpRequestError.SecureConnectionError, "ssl",
                new AuthenticationException ("The remote certificate is invalid because it has expired.")));
            rig.Start ();

            await rig.StateAsync (ConnectionState.Misconfigured, "misconfigured");

            Assert.Equal (ConnectionProblem.Certificate, rig.Current.Problem);
            Assert.Contains ("expired", rig.Current.Detail);
            Assert.Equal (TimeSpan.FromMinutes (5), await rig.Clock.NextDelayAsync ());
        }

        [Theory]
        [InlineData (SocketError.NetworkUnreachable, ConnectionState.Offline)]
        [InlineData (SocketError.HostUnreachable, ConnectionState.Offline)]
        [InlineData (SocketError.HostNotFound, ConnectionState.Offline)]
        [InlineData (SocketError.ConnectionRefused, ConnectionState.Reconnecting)]     // the server is down or restarting
        [InlineData (SocketError.ConnectionReset, ConnectionState.Reconnecting)]
        public async Task NoRouteIsOffline_ButADownServerIsReconnecting (SocketError error, ConnectionState expected)
        {
            await using var rig = new SubscriptionRig ();
            rig.Server.EnqueueException (new HttpRequestException (HttpRequestError.ConnectionError, "fail", new SocketException ((int)error)));
            rig.Start ();

            await rig.StateAsync (expected, $"{expected} for {error}");

            Assert.Equal (ConnectionProblem.Unreachable, rig.Current.Problem);
            Assert.Equal (TimeSpan.FromSeconds (1), await rig.Clock.NextDelayAsync ());
        }

        [Fact]
        public async Task AServerThatAcceptsButNeverAnswers_IsGivenUpOnAtTheDeadline ()
        {
            await using var rig = new SubscriptionRig ();
            rig.Server.EnqueueSilence ();
            rig.Start ();
            await rig.RequestsAsync (1);
            await rig.Clock.SettleAsync ();

            Assert.Equal (TimeSpan.FromSeconds (110), await rig.Clock.NextDelayAsync ());
            rig.Clock.Advance (TimeSpan.FromSeconds (110));
            await rig.StateAsync (ConnectionState.Reconnecting, "the deadline to fire");

            Assert.Equal (ConnectionProblem.Silent, rig.Current.Problem);
        }

        [Fact]
        public async Task AFailedHistoryRequest_IsAnAttemptThatFailed_AndIsRepeated ()
        {
            await using var rig = new SubscriptionRig ();
            rig.Server.EnqueueStatus (HttpStatusCode.InternalServerError);
            rig.Server.EnqueuePoll (AppendixC.Warning);
            var stream = rig.Server.EnqueueStream ();
            rig.Start ();

            await rig.StateAsync (ConnectionState.Reconnecting, "the failed replay");
            await rig.Clock.AdvanceToNextAsync ();
            await stream.Connected;
            await rig.MessagesAsync (1);

            Assert.True (rig.Server.Requests[0].IsPoll);
            Assert.True (rig.Server.Requests[1].IsPoll);       // the replay is retried, not skipped
            Assert.Equal (MessageOrigin.Backlog, rig.Messages[0].Origin);
        }

        [Fact]
        public async Task AnUnexpectedException_DoesNotEndTheListener_ItIsReportedAndRetried ()
        {
            await using var rig = new SubscriptionRig ();
            rig.Server.Enqueue ((_, _) => throw new InvalidOperationException ("a bug in something"));
            rig.Server.EnqueuePoll ();
            var stream = rig.Server.EnqueueStream ();
            rig.Start ();

            await rig.StateAsync (ConnectionState.Reconnecting, "the surprise");
            Assert.Equal ((ConnectionProblem.ServerError, "InvalidOperationException"), (rig.Current.Problem, rig.Current.Detail));

            await rig.Clock.AdvanceToNextAsync ();
            await stream.Connected;
            await rig.StateAsync (ConnectionState.Live, "the listener to carry on");
            Assert.Null (rig.ReaderFailure);
        }

        // ---- reading the stream ----

        [Fact]
        public async Task StrayAndOverLongLines_NeverEndAHealthyConnection ()
        {
            await using var rig = new SubscriptionRig (o => o with { MaxLineBytes = 200 });
            var stream = await rig.ConnectedStreamAsync ();

            stream.Send ("this is not json");
            stream.Send (new string ('x', 5000));
            stream.Send ("""{"event":"message_delete","id":"d1","time":1}""");
            stream.Send (SubscriptionRig.Message ("after"));
            await rig.MessagesAsync (1);

            Assert.Equal ("after", rig.Messages[0].Message!.Id);
            Assert.Equal (ConnectionState.Live, rig.Current.State);
            Assert.Equal (2, rig.Server.Requests.Count);            // no reconnect happened
        }

        [Fact]
        public async Task AMessageSplitAcrossReads_ArrivesWhole ()
        {
            await using var rig = new SubscriptionRig ();
            var stream = await rig.ConnectedStreamAsync ();
            var bytes = System.Text.Encoding.UTF8.GetBytes (SubscriptionRig.Message ("split", body: "Workshop is at 52 °C ✅") + "\n");

            for (var i = 0; i < bytes.Length; i += 7)
                stream.SendRaw (bytes[i..Math.Min (i + 7, bytes.Length)]);
            await rig.MessagesAsync (1);

            Assert.Equal ("Workshop is at 52 °C ✅", rig.Messages[0].Message!.Message);
        }

        [Fact]
        public async Task OpenAndKeepalive_AreYielded_ButUnknownEventsAreNot ()
        {
            await using var rig = new SubscriptionRig ();
            var stream = await rig.ConnectedStreamAsync ();

            stream.Send (AppendixC.Open);
            stream.Send ("""{"id":"x","time":1,"event":"poll_request","topic":"t"}""");
            stream.Send (AppendixC.Keepalive);
            await Wait.UntilAsync (() => rig.Events.Count >= 2, "two events");
            await rig.Clock.SettleAsync ();

            Assert.Equal ([NtfyEventKind.Open, NtfyEventKind.Keepalive], rig.Events.Select (e => e.Kind));
        }

        [Fact]
        public async Task LastHeard_MovesOnEveryLine_AndIsShownInTheState ()
        {
            await using var rig = new SubscriptionRig ();
            var stream = await rig.ConnectedStreamAsync ();
            var connectedAt = rig.Current.LastHeard;

            rig.Clock.Advance (TimeSpan.FromSeconds (45));
            stream.Send (AppendixC.Keepalive);
            await Wait.UntilAsync (() => rig.Current.LastHeard != connectedAt, "the keepalive to be noticed");

            Assert.Equal (connectedAt + TimeSpan.FromSeconds (45), rig.Current.LastHeard);
        }

        [Fact]
        public async Task TheUnencryptedBadge_IsCarriedInEveryState ()
        {
            await using var rig = new SubscriptionRig (o => o with { Unencrypted = true }, baseAddress: "http://192.168.1.10:2586");
            await rig.ConnectedStreamAsync ();

            Assert.True (rig.Current.Unencrypted);
            Assert.All (rig.States, s => Assert.True (s.Unencrypted));
        }

        // ---- lifetime ----

        [Fact]
        public async Task Cancelling_EndsTheReadCleanly_AndNothingIsRequestedAfterwards ()
        {
            await using var rig = new SubscriptionRig ();
            await rig.ConnectedStreamAsync ();
            var requests = rig.Server.Requests.Count;

            await rig.StopAsync ();
            rig.Clock.Advance (TimeSpan.FromHours (1));

            Assert.Null (rig.ReaderFailure);
            Assert.Equal (requests, rig.Server.Requests.Count);
        }

        [Fact]
        public async Task Cancelling_DuringABackoffWait_EndsPromptly ()
        {
            await using var rig = new SubscriptionRig ();
            rig.Server.WhenEmpty = (_, _) => throw Refused ();
            rig.Start ();
            await rig.RequestsAsync (1);
            await rig.Clock.SettleAsync ();

            await rig.StopAsync ();

            Assert.Null (rig.ReaderFailure);
            Assert.Single (rig.Server.Requests);
        }

        [Fact]
        public async Task OnlyOneReadAtATime ()
        {
            await using var rig = new SubscriptionRig ();
            await rig.ConnectedStreamAsync ();

            await Assert.ThrowsAsync<InvalidOperationException> (async () => {
                await foreach (var _ in rig.Subscription.ReadAsync (TestContext.Current.CancellationToken)) {
                }
            });
        }

        // ---- secrets ----

        [Fact]
        public async Task NoSecretEverAppearsInAStateOrAUrl ()
        {
            await using var rig = new SubscriptionRig ();
            rig.Credentials = NtfyCredentials.Basic ("alice", "hunter2-secret");
            rig.Server.EnqueueStatus (HttpStatusCode.Unauthorized);
            rig.Server.EnqueueException (new HttpRequestException (HttpRequestError.ConnectionError, "fail"));
            rig.Start ();

            await rig.StateAsync (ConnectionState.AuthFailed, "auth failed");
            await rig.Clock.AdvanceToNextAsync ();
            await rig.RequestsAsync (2);
            await rig.Clock.SettleAsync ();

            foreach (var state in rig.States) {
                var text = state.ToString ();
                Assert.DoesNotContain ("hunter2", text);
                Assert.DoesNotContain ("alice", text);
                Assert.DoesNotContain ("aHVudGVy", text);        // base64 of the start of the secret
            }

            Assert.All (rig.Server.Requests, r => Assert.DoesNotContain ("hunter2", r.Uri.ToString ()));
        }

        // ---- the soak test of milestone 1 ----

        [Fact]
        public async Task ASoakOf100ForcedDisconnects_DeliversEveryMessageOnceAndInOrder ()
        {
            await using var rig = new SubscriptionRig ();
            rig.Server.EnqueuePoll ();

            var streams = new List<StreamHandle> ();
            for (var i = 0; i < 100; i++)
                streams.Add (rig.Server.EnqueueStream ());
            var finalStream = rig.Server.EnqueueStream ();
            rig.Start ();

            for (var i = 0; i < 100; i++) {
                await streams[i].Connected;
                streams[i].Send (SubscriptionRig.Message ($"m{i}", time: 1790000000 + i));
                await rig.MessagesAsync (i + 1);

                // Half the drops are a clean end (a proxy timeout), half a broken connection (a network dropping).
                if (i % 2 == 0)
                    streams[i].End ();
                else
                    streams[i].Fail (new IOException ("connection reset by peer"));

                await rig.StateAsync (ConnectionState.Reconnecting, $"drop #{i}");
                await rig.Clock.AdvanceToNextAsync ();
            }

            await finalStream.Connected;
            await rig.StateAsync (ConnectionState.Live, "live after 100 drops");

            Assert.Equal (Enumerable.Range (0, 100).Select (i => $"m{i}"), rig.Messages.Select (m => m.Message!.Id));
            Assert.All (rig.Messages, m => Assert.Equal (MessageOrigin.Live, m.Origin));
            Assert.Equal (1, rig.Server.Requests.Count (r => r.IsPoll));                     // history was replayed once, at the start, and never again
            for (var i = 1; i < 100; i++)
                Assert.Equal ($"m{i - 1}", rig.Server.Requests.Where (r => !r.IsPoll).ElementAt (i).Since);   // each reconnect resumed after the last message
            Assert.Null (rig.ReaderFailure);
        }
    }
}
