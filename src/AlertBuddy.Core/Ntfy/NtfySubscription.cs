using AlertBuddy.Core.Localization;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Security;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Security.Authentication;
using System.Threading.Channels;

namespace AlertBuddy.Core.Ntfy
{
    /// <summary>Everything the listener needs to know about the server and how to resume.</summary>
    /// <param name="BaseUri">The server, already checked by <see cref="NtfyEndpoint.Check"/>.</param>
    /// <param name="Topic">The topic. It acts as a password, so it is never logged.</param>
    public sealed record NtfySubscriptionOptions (Uri BaseUri, string Topic)
    {
        /// <summary>Supplies the sign-in for each connection attempt, so a changed password applies on the next reconnect without restarting.</summary>
        public Func<NtfyCredentials> Credentials { get; init; } = () => NtfyCredentials.None;

        /// <summary>The id of the newest message already processed, to resume after. Persisted by the engine after every message.</summary>
        public Func<string?> LastMessageId { get; init; } = () => null;

        /// <summary>How far back a history replay asks for. ntfy caches 12 hours by default.</summary>
        public TimeSpan ReplayWindow { get; init; } = TimeSpan.FromHours (12);

        /// <summary>After being out of touch this long, the next connection replays history instead of resuming from the last id.</summary>
        public TimeSpan LongGap { get; init; } = TimeSpan.FromMinutes (30);

        /// <summary>Plain http on a private network: shown with an "unencrypted" badge.</summary>
        public bool Unencrypted { get; init; }

        /// <summary>Lines longer than this are discarded (PLAN.md section 5.2).</summary>
        public int MaxLineBytes { get; init; } = BoundedLineReader.DefaultMaxLineBytes;
    }

    /// <summary>
    /// A held-open subscription to one ntfy topic that reconnects on its own (PLAN.md section 5.2): a watchdog for a connection that
    /// went quiet, backoff with jitter, slow retries for a refused sign-in, and resuming from the last message. The state it is in is
    /// always available on <see cref="Current"/> and announced on <see cref="StateChanged"/>.
    /// </summary>
    public sealed class NtfySubscription
    {
        private const string UserAgent = "AlertBuddy/1";

        private readonly NtfySubscriptionOptions options;
        private readonly HttpMessageInvoker http;
        private readonly TimeProvider time;
        private readonly Func<double> jitter;
        private readonly object gate = new ();
        private ConnectionInfo current;
        private DateTimeOffset? lastHeard;
        private int running;

        /// <summary>Creates a subscription. Nothing is contacted until <see cref="ReadAsync"/> is enumerated.</summary>
        /// <param name="options">The server and how to resume.</param>
        /// <param name="handler">The HTTP handler. Null uses <see cref="CreateDefaultHandler"/>; tests pass a scripted one.</param>
        /// <param name="timeProvider">Drives every delay and deadline, so tests run in fake time.</param>
        /// <param name="jitter">Returns a number from 0 up to 1 for backoff jitter. Random by default.</param>
        public NtfySubscription (NtfySubscriptionOptions options, HttpMessageHandler? handler = null, TimeProvider? timeProvider = null, Func<double>? jitter = null)
        {
            this.options = options ?? throw new ArgumentNullException (nameof (options));
            http = new HttpMessageInvoker (handler ?? CreateDefaultHandler (), disposeHandler: handler is null);
            time = timeProvider ?? TimeProvider.System;
            this.jitter = jitter ?? (() => Random.Shared.NextDouble ());
            current = ConnectionInfo.Initial with { Unencrypted = options.Unencrypted };
        }

        /// <summary>The handler used in production. It never follows a redirect: a sign-in must not be forwarded to a host the grown-up did not configure.</summary>
        public static HttpMessageHandler CreateDefaultHandler ()
            => new SocketsHttpHandler {
                AllowAutoRedirect = false,
                ConnectTimeout = TimeSpan.FromSeconds (30),
                PooledConnectionLifetime = TimeSpan.FromMinutes (5),
                AutomaticDecompression = DecompressionMethods.None,   // a stream must be read as it arrives, not buffered for a decoder
            };

        /// <summary>The listener's state right now.</summary>
        public ConnectionInfo Current {
            get {
                lock (gate)
                    return current;
            }
        }

        /// <summary>The state changed, or something was heard (which moves <see cref="ConnectionInfo.LastHeard"/>).</summary>
        public event Action<ConnectionInfo>? StateChanged;

        /// <summary>
        /// Connects, and yields every event from the stream until cancelled, reconnecting through failures. Open and keepalive events are
        /// yielded too so a consumer can see the connection is alive. Only one enumeration may run at a time.
        /// </summary>
        public async IAsyncEnumerable<NtfyEvent> ReadAsync ([EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            if (Interlocked.Exchange (ref running, 1) == 1)
                throw new InvalidOperationException ("This subscription is already being read.");

            // The loop runs on its own task and hands events over a bounded channel: an async iterator cannot yield from inside the
            // try/catch a reconnect loop needs, and the bound stops a slow consumer being buried by a busy server.
            var channel = Channel.CreateBounded<NtfyEvent> (new BoundedChannelOptions (256) {
                FullMode = BoundedChannelFullMode.Wait,
                SingleReader = true,
                SingleWriter = true,
            });

            using var stop = CancellationTokenSource.CreateLinkedTokenSource (cancellationToken);
            var loop = Task.Run (() => RunAsync (channel.Writer, stop.Token), CancellationToken.None);

            try {
                await foreach (var evt in channel.Reader.ReadAllAsync (cancellationToken).ConfigureAwait (false))
                    yield return evt;
            } finally {
                await stop.CancelAsync ().ConfigureAwait (false);
                try {
                    await loop.ConfigureAwait (false);
                } catch (OperationCanceledException) {
                }

                Volatile.Write (ref running, 0);
            }
        }

        // ---- the reconnect loop ----

        private async Task RunAsync (ChannelWriter<NtfyEvent> output, CancellationToken ct)
        {
            var failures = 0;   // consecutive failed attempts, for backoff
            try {
                while (!ct.IsCancellationRequested) {
                    var attempt = await ConnectOnceAsync (output, failures, ct).ConfigureAwait (false);

                    // A connection that stayed healthy for a while earns a fresh start: the next drop retries in one second, not sixty. An
                    // attempt that came with its own wait (a refused sign-in, a Retry-After) is not counted, or one network blip after a
                    // week of slow retries would start the backoff at a minute.
                    failures = attempt.WasHealthy ? 0 : attempt.Wait is null ? failures + 1 : failures;

                    var wait = attempt.Wait ?? Backoff.Delay (Math.Max (failures - 1, 0), jitter ());
                    await Task.Delay (wait, time, ct).ConfigureAwait (false);
                }
            } catch (OperationCanceledException) when (ct.IsCancellationRequested) {
            } finally {
                output.TryComplete ();
            }
        }

        private readonly record struct Attempt (bool WasHealthy, TimeSpan? Wait);

        private async Task<Attempt> ConnectOnceAsync (ChannelWriter<NtfyEvent> output, int failures, CancellationToken ct)
        {
            // The state is deliberately not touched here. It is Connecting until the first attempt resolves and after that whatever the last
            // attempt left it as, so a sign-in that is refused stays "AuthFailed" through its slow retries instead of flickering.
            try {
                var resumeFrom = await ReplayIfNeededAsync (output, ct).ConfigureAwait (false);
                if (resumeFrom.Failure is { } replayFailure)
                    return replayFailure;

                return await StreamAsync (output, resumeFrom.Since, ct).ConfigureAwait (false);
            } catch (OperationCanceledException) when (ct.IsCancellationRequested) {
                throw;
            } catch (HttpRequestException ex) {
                return Classify (ex);
            } catch (IOException ex) {
                // The connection broke mid-response.
                Set (ConnectionState.Reconnecting, ConnectionProblem.Unreachable, Describe (ex));
                return new Attempt (false, null);
            } catch (OperationCanceledException) {
                // Not the caller's cancellation, so a deadline of ours: the server took too long to answer.
                Set (ConnectionState.Reconnecting, ConnectionProblem.Silent, Loc.T ("the server did not answer in time"));
                return new Attempt (false, null);
            } catch (Exception ex) {
                // Anything else is a bug or a surprise, and a listener that stops without a word is the worst way for an alert app to
                // fail. It is reported as a server problem naming the exception type, and the loop carries on with backoff.
                Set (ConnectionState.Reconnecting, ConnectionProblem.ServerError, ex.GetType ().Name);
                return new Attempt (false, null);
            }
        }

        // ---- replay: history first, silently, then the live stream ----

        private readonly record struct Resume (string? Since, Attempt? Failure);

        // A stream opened with since=12h mixes cached messages with live ones and gives no marker between them, and comparing a message's
        // time with the phone's clock breaks the moment the two clocks differ. So history is fetched as its own finite request (poll=1),
        // tagged Backlog, and the live stream is opened after it and tagged Live. The boundary is exact, whatever the clocks say.
        private async Task<Resume> ReplayIfNeededAsync (ChannelWriter<NtfyEvent> output, CancellationToken ct)
        {
            var lastId = options.LastMessageId ();
            var now = time.GetUtcNow ();

            if (lastHeard is { } heard && now - heard <= options.LongGap) {
                // A short gap: resume exactly after the last message, which are news, not history. With no message ever seen there is no id to
                // resume from, so ask for what arrived since the connection was last heard (plus a minute's margin; duplicates are dropped).
                return new Resume (lastId ?? DurationText (now - heard + TimeSpan.FromMinutes (1)), null);
            }

            // First connection, or a long gap: replay history so the Alert Book and "active right now" are right. Silently.
            var newest = lastId;
            var pollStarted = now;
            using var response = await SendAsync (Url ($"poll=1&since={DurationText (options.ReplayWindow)}"), ct).ConfigureAwait (false);
            if (StatusFailure (response) is { } failure)
                return new Resume (null, failure);

            Heard ();
            await using var body = await response.Content.ReadAsStreamAsync (ct).ConfigureAwait (false);
            await foreach (var line in BoundedLineReader.ReadAsync (body, options.MaxLineBytes, ct).ConfigureAwait (false)) {
                if (line.Line is null || !NtfyParser.TryParse (line.Line, out var evt) || evt is null || evt.Kind != NtfyEventKind.Message)
                    continue;

                newest = evt.Message!.Id;
                await output.WriteAsync (evt with { Origin = MessageOrigin.Backlog }, ct).ConfigureAwait (false);
            }

            // The live stream resumes after the newest message replayed. If there was none, it resumes from when the history request began
            // (less a margin for clock differences): a message published in the moment between the two requests would otherwise be lost,
            // and one replayed twice is simply dropped as a duplicate.
            return new Resume (newest ?? pollStarted.AddSeconds (-30).ToUnixTimeSeconds ().ToString (System.Globalization.CultureInfo.InvariantCulture), null);
        }

        // ---- the live stream ----

        private async Task<Attempt> StreamAsync (ChannelWriter<NtfyEvent> output, string? since, CancellationToken ct)
        {
            using var response = await SendAsync (Url (since is null ? "" : $"since={Uri.EscapeDataString (since)}"), ct).ConfigureAwait (false);
            if (StatusFailure (response) is { } failure)
                return failure;

            var connectedAt = time.GetTimestamp ();
            Heard ();
            Set (ConnectionState.Live);

            // A connection can go quiet without ever closing: a route that vanished, a NAT that forgot the socket. Silence for longer
            // than two missed keepalives is the tell. The timer is restarted by every line, so it only fires on real silence.
            using var watchdog = new CancellationTokenSource (Backoff.Watchdog, time);
            using var linked = CancellationTokenSource.CreateLinkedTokenSource (ct, watchdog.Token);

            try {
                await using var body = await response.Content.ReadAsStreamAsync (linked.Token).ConfigureAwait (false);
                await foreach (var line in BoundedLineReader.ReadAsync (body, options.MaxLineBytes, linked.Token).ConfigureAwait (false)) {
                    watchdog.CancelAfter (Backoff.Watchdog);
                    Heard ();

                    if (line.Line is null || !NtfyParser.TryParse (line.Line, out var evt) || evt is null || evt.Kind == NtfyEventKind.Other)
                        continue;   // a stray or over-long line never ends a healthy connection

                    if (evt.Kind == NtfyEventKind.Open)
                        Set (ConnectionState.Live);

                    await output.WriteAsync (evt, ct).ConfigureAwait (false);
                }

                // The server ended the stream cleanly: a restart, or a proxy's idle timeout. Reconnect, not an error.
                Set (ConnectionState.Reconnecting, ConnectionProblem.Unreachable, Loc.T ("the server closed the connection"));
            } catch (OperationCanceledException) when (!ct.IsCancellationRequested && watchdog.IsCancellationRequested) {
                Set (ConnectionState.Reconnecting, ConnectionProblem.Silent, Loc.T ("nothing was heard for too long"));
            } catch (IOException ex) {
                Set (ConnectionState.Reconnecting, ConnectionProblem.Unreachable, Describe (ex));
            }

            return new Attempt (time.GetElapsedTime (connectedAt) >= Backoff.HealthyAfter, null);
        }

        // ---- requests and their failures ----

        private Uri Url (string query)
        {
            var path = $"{options.BaseUri.GetLeftPart (UriPartial.Path).TrimEnd ('/')}/{Uri.EscapeDataString (options.Topic)}/json";
            return new Uri (query.Length == 0 ? path : $"{path}?{query}");
        }

        private async Task<HttpResponseMessage> SendAsync (Uri url, CancellationToken ct)
        {
            using var request = new HttpRequestMessage (HttpMethod.Get, url);
            request.Headers.UserAgent.ParseAdd (UserAgent);
            if (options.Credentials ().AuthorizationHeaderValue is { } header)
                request.Headers.TryAddWithoutValidation ("Authorization", header);

            // Connecting and receiving the headers has a deadline of its own; the watchdog only starts once the body is flowing.
            using var deadline = new CancellationTokenSource (Backoff.Watchdog, time);
            using var linked = CancellationTokenSource.CreateLinkedTokenSource (ct, deadline.Token);
            return await http.SendAsync (request, linked.Token).ConfigureAwait (false);
        }

        // Null when the response is a healthy 200. Otherwise sets the state and says how long to wait.
        private Attempt? StatusFailure (HttpResponseMessage response)
        {
            var code = (int)response.StatusCode;
            if (response.StatusCode == HttpStatusCode.OK)
                return null;

            if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden) {
                Set (ConnectionState.AuthFailed, ConnectionProblem.Unauthorized, $"HTTP {code}");
                return new Attempt (false, Backoff.SlowRetry);
            }

            if (response.StatusCode == HttpStatusCode.NotFound) {
                Set (ConnectionState.Misconfigured, ConnectionProblem.TopicNotFound, Loc.T ("the topic was not found"));
                return new Attempt (false, Backoff.SlowRetry);
            }

            if (code is >= 300 and < 400) {
                Set (ConnectionState.Misconfigured, ConnectionProblem.InvalidAddress, Loc.T ("the server redirected the request"));
                return new Attempt (false, Backoff.SlowRetry);
            }

            if (response.StatusCode == HttpStatusCode.TooManyRequests) {
                Set (ConnectionState.Reconnecting, ConnectionProblem.RateLimited, Loc.T ("the server asked the app to slow down"));
                return new Attempt (false, RetryAfter (response) ?? Backoff.Delay (3, 0.5));
            }

            Set (ConnectionState.Reconnecting, ConnectionProblem.ServerError, $"HTTP {code}");
            return new Attempt (false, null);
        }

        private TimeSpan? RetryAfter (HttpResponseMessage response)
        {
            RetryConditionHeaderValue? header = response.Headers.RetryAfter;
            if (header?.Delta is { } delta)
                return delta;

            if (header?.Date is { } date)
                return date - time.GetUtcNow ();

            return null;
        }

        private Attempt Classify (HttpRequestException ex)
        {
            // A bad certificate is a settings problem, not an outage: it will not fix itself until a grown-up acts, so it is retried slowly.
            if (ex.HttpRequestError == HttpRequestError.SecureConnectionError || ex.InnerException is AuthenticationException || ex.InnerException?.InnerException is AuthenticationException) {
                Set (ConnectionState.Misconfigured, ConnectionProblem.Certificate, CertificateDetail (ex));
                return new Attempt (false, Backoff.SlowRetry);
            }

            // No route at all is "offline"; a refused or reset connection is a server that is down or restarting.
            var offline = ex.InnerException is SocketException { SocketErrorCode: SocketError.NetworkUnreachable or SocketError.HostUnreachable or SocketError.HostNotFound or SocketError.TryAgain or SocketError.NetworkDown }
                || ex.HttpRequestError is HttpRequestError.NameResolutionError;

            Set (offline ? ConnectionState.Offline : ConnectionState.Reconnecting, ConnectionProblem.Unreachable, Describe (ex));
            return new Attempt (false, null);
        }

        private static string CertificateDetail (HttpRequestException ex)
        {
            // The message names what failed ("the remote certificate is invalid ... RemoteCertificateNameMismatch"). It never contains
            // the request headers, so it is safe to show.
            var inner = ex.InnerException?.Message ?? ex.Message;
            return Loc.F ("the secure connection failed: {0}", inner);
        }

        // A short factual description that is safe to show. Exception messages from the HTTP stack name the failure, not the request.
        private static string Describe (Exception ex) => ex is HttpRequestException { HttpRequestError: not HttpRequestError.Unknown } h ? h.HttpRequestError.ToString () : ex.GetType ().Name;

        // Rounded UP: asking for slightly more than the gap only re-delivers messages that are dropped as duplicates, while rounding down loses some.
        private static string DurationText (TimeSpan window) => $"{Math.Max (1, (int)Math.Ceiling (window.TotalMinutes))}m";

        // ---- state ----

        private void Heard ()
        {
            var now = time.GetUtcNow ();
            ConnectionInfo info;
            lock (gate) {
                lastHeard = now;
                info = current = current with { LastHeard = now };
            }

            StateChanged?.Invoke (info);
        }

        private void Set (ConnectionState state, ConnectionProblem problem = ConnectionProblem.None, string? detail = null)
        {
            ConnectionInfo info;
            lock (gate) {
                var unchanged = current.State == state && current.Problem == problem && current.Detail == detail;
                info = current = current with { State = state, Problem = problem, Detail = detail, Unencrypted = options.Unencrypted };
                if (unchanged)
                    return;
            }

            StateChanged?.Invoke (info);
        }
    }
}
