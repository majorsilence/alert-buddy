using System.Globalization;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Channels;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace AlertBuddy.FakeNtfy
{
    /// <summary>How the fake server behaves.</summary>
    public sealed record FakeNtfyOptions
    {
        /// <summary>The port to listen on. 0 picks a free one.</summary>
        public int Port { get; init; }

        /// <summary>Listen on every interface, so a phone on the same network can reach it. Off by default: loopback only.</summary>
        public bool Lan { get; init; }

        /// <summary>How often a held-open stream gets a keepalive line. ntfy's is about 45 seconds.</summary>
        public TimeSpan Keepalive { get; init; } = TimeSpan.FromSeconds (45);

        /// <summary>When set, a request must carry this user name and password (HTTP basic) or it is refused with 401.</summary>
        public string? BasicUser { get; init; }

        /// <summary>The password for <see cref="BasicUser"/>.</summary>
        public string? BasicPassword { get; init; }

        /// <summary>When set, a request must carry this bearer token or it is refused with 401.</summary>
        public string? Token { get; init; }
    }

    /// <summary>One request the server saw.</summary>
    public sealed record SeenRequest (string Method, string PathAndQuery, string? Authorization);

    /// <summary>
    /// An ntfy-compatible server for development and tests (PLAN.md section 4.4). It serves <c>GET /{topic}/json</c> (a held-open NDJSON
    /// stream, or a finite one with <c>poll=1</c>, resumable with <c>since=</c>) and accepts <c>POST /{topic}</c>. Extra endpoints under
    /// <c>/_control</c> and <c>/_scenario</c> break it on purpose. All data is invented.
    /// </summary>
    public sealed class FakeNtfyServer : IAsyncDisposable
    {
        private readonly WebApplication app;
        private readonly FakeNtfyOptions options;
        private readonly object gate = new ();
        private readonly List<StoredMessage> messages = [];
        private readonly Dictionary<string, List<Subscriber>> subscribers = new ();
        private readonly List<SeenRequest> requests = [];
        private readonly Queue<int> failures = new ();

        private sealed record StoredMessage (string Id, long Time, string Topic, int Priority, string? Title, string Body, string[] Tags);

        private sealed class Subscriber (HttpContext context)
        {
            public Channel<string> Lines { get; } = Channel.CreateUnbounded<string> ();
            public HttpContext Context { get; } = context;
        }

        private FakeNtfyServer (WebApplication app, FakeNtfyOptions options, Uri baseUri)
        {
            this.app = app;
            this.options = options;
            BaseUri = baseUri;
        }

        /// <summary>Where the server is listening, for example <c>http://127.0.0.1:51234</c>.</summary>
        public Uri BaseUri { get; }

        /// <summary>How many held-open streams there are right now.</summary>
        public int OpenStreams {
            get {
                lock (gate)
                    return subscribers.Values.Sum (s => s.Count);
            }
        }

        /// <summary>Every request seen, oldest first.</summary>
        public IReadOnlyList<SeenRequest> Requests {
            get {
                lock (gate)
                    return requests.ToList ();
            }
        }

        /// <summary>Starts a server.</summary>
        public static async Task<FakeNtfyServer> StartAsync (FakeNtfyOptions? options = null)
        {
            options ??= new FakeNtfyOptions ();

            var builder = WebApplication.CreateSlimBuilder ();
            builder.Logging.ClearProviders ();
            builder.WebHost.UseUrls ($"http://{(options.Lan ? "0.0.0.0" : "127.0.0.1")}:{options.Port}");
            var app = builder.Build ();

            var holder = new FakeNtfyServer[1];
            app.MapGet ("/{topic}/json", (HttpContext ctx, string topic) => holder[0].SubscribeAsync (ctx, topic));
            app.MapPost ("/{topic}", (HttpContext ctx, string topic) => holder[0].PublishAsync (ctx, topic));
            app.MapPost ("/_scenario/{topic}", (HttpContext ctx, string topic) => holder[0].ScenarioAsync (ctx, topic));
            app.MapPost ("/_control/drop", () => { holder[0].DropConnections (); return Results.Ok (); });
            app.MapPost ("/_control/fail/{code:int}/{times:int?}", (int code, int? times) => { holder[0].FailNext (code, times ?? 1); return Results.Ok (); });
            app.MapGet ("/_control/state", () => Results.Json (new { openStreams = holder[0].OpenStreams, messages = holder[0].MessageCount }));

            await app.StartAsync ().ConfigureAwait (false);

            var address = app.Services.GetRequiredService<IServer> ().Features.Get<IServerAddressesFeature> ()!.Addresses.First ();
            var uri = new Uri (address.Replace ("0.0.0.0", "127.0.0.1").Replace ("[::]", "127.0.0.1"));
            var server = new FakeNtfyServer (app, options, uri);
            holder[0] = server;
            return server;
        }

        /// <summary>How many messages are stored.</summary>
        public int MessageCount {
            get {
                lock (gate)
                    return messages.Count;
            }
        }

        /// <summary>Publishes a message, as a client POSTing would, and returns its id.</summary>
        public string Publish (string topic, string message, string? title = null, int priority = 3, string[]? tags = null, DateTimeOffset? time = null)
        {
            var stored = new StoredMessage (NewId (), (time ?? DateTimeOffset.UtcNow).ToUnixTimeSeconds (), topic, Math.Clamp (priority, 1, 5), title, message, tags ?? []);
            List<Subscriber> live;
            lock (gate) {
                messages.Add (stored);
                live = subscribers.TryGetValue (topic, out var list) ? list.ToList () : [];
            }

            var line = ToJson (stored, "message");
            foreach (var subscriber in live)
                subscriber.Lines.Writer.TryWrite (line);

            return stored.Id;
        }

        /// <summary>Ends every held-open stream abruptly, as a dropped network does.</summary>
        public void DropConnections ()
        {
            List<Subscriber> all;
            lock (gate)
                all = subscribers.Values.SelectMany (s => s).ToList ();

            foreach (var subscriber in all)
                subscriber.Context.Abort ();
        }

        /// <summary>The next <paramref name="times"/> requests are answered with this status code.</summary>
        public void FailNext (int statusCode, int times = 1)
        {
            lock (gate)
                for (var i = 0; i < times; i++)
                    failures.Enqueue (statusCode);
        }

        /// <summary>
        /// Publishes a warning, then an alarm, then an all clear for an invented room called Workshop, <paramref name="step"/> apart. The
        /// same lifecycle as the plan's fixture, for a demo on an emulator or a phone.
        /// </summary>
        public async Task RunLifecycleAsync (string topic, TimeSpan step, CancellationToken cancellationToken = default)
        {
            Publish (topic, "Workshop is at 41.2 °C", "Workshop: temperature warning", 4);
            await Task.Delay (step, cancellationToken).ConfigureAwait (false);
            Publish (topic, "Workshop is at 50.6 °C", "Workshop: temperature alarm", 5);
            await Task.Delay (step, cancellationToken).ConfigureAwait (false);
            Publish (topic, "Workshop is at 44.0 °C", "Workshop: temperature alarm (resolved)", 3);
        }

        /// <inheritdoc />
        public async ValueTask DisposeAsync ()
        {
            DropConnections ();
            await app.StopAsync ().ConfigureAwait (false);
            await app.DisposeAsync ().ConfigureAwait (false);
        }

        // ---- endpoints ----

        private async Task SubscribeAsync (HttpContext context, string topic)
        {
            if (!Admit (context))
                return;

            var since = context.Request.Query["since"].ToString ();
            var poll = context.Request.Query["poll"] == "1";

            context.Response.ContentType = "application/x-ndjson";
            context.Response.Headers.CacheControl = "no-cache";

            Subscriber? subscriber = null;
            List<StoredMessage> cached;
            lock (gate) {
                cached = string.IsNullOrEmpty (since) && !poll ? [] : Since (topic, since);

                // Registered in the same critical section that read the cache, so a message published in between is neither missed nor sent twice.
                if (!poll) {
                    subscriber = new Subscriber (context);
                    if (!subscribers.TryGetValue (topic, out var list))
                        subscribers[topic] = list = [];
                    list.Add (subscriber);
                }
            }

            try {
                if (!poll)
                    await WriteAsync (context, $$"""{"id":"{{NewId ()}}","time":{{DateTimeOffset.UtcNow.ToUnixTimeSeconds ()}},"event":"open","topic":"{{topic}}"}""").ConfigureAwait (false);

                foreach (var message in cached)
                    await WriteAsync (context, ToJson (message, "message")).ConfigureAwait (false);

                if (poll || subscriber is null)
                    return;

                var token = context.RequestAborted;
                while (!token.IsCancellationRequested) {
                    using var wait = CancellationTokenSource.CreateLinkedTokenSource (token);
                    wait.CancelAfter (options.Keepalive);
                    try {
                        if (await subscriber.Lines.Reader.WaitToReadAsync (wait.Token).ConfigureAwait (false)) {
                            while (subscriber.Lines.Reader.TryRead (out var line))
                                await WriteAsync (context, line).ConfigureAwait (false);
                        }
                    } catch (OperationCanceledException) when (!token.IsCancellationRequested) {
                        // Nothing to say for a while: a keepalive, so the client can tell a quiet server from a dead connection.
                        await WriteAsync (context, $$"""{"id":"{{NewId ()}}","time":{{DateTimeOffset.UtcNow.ToUnixTimeSeconds ()}},"event":"keepalive","topic":"{{topic}}"}""").ConfigureAwait (false);
                    }
                }
            } catch (OperationCanceledException) {
            } catch (IOException) {
            } finally {
                if (subscriber is not null)
                    lock (gate)
                        subscribers[topic].Remove (subscriber);
            }
        }

        private async Task PublishAsync (HttpContext context, string topic)
        {
            if (!Admit (context))
                return;

            using var reader = new StreamReader (context.Request.Body, Encoding.UTF8);
            var body = await reader.ReadToEndAsync ().ConfigureAwait (false);
            var title = Header (context, "Title", "X-Title", "t");
            var priority = ParsePriority (Header (context, "Priority", "X-Priority", "p"));
            var tags = (Header (context, "Tags", "X-Tags", "ta") ?? "").Split (',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

            var id = Publish (topic, body, title, priority, tags);
            await context.Response.WriteAsync ($$"""{"id":"{{id}}","event":"message","topic":"{{topic}}"}""").ConfigureAwait (false);
        }

        private async Task ScenarioAsync (HttpContext context, string topic)
        {
            if (!Admit (context))
                return;

            var step = TimeSpan.FromSeconds (double.TryParse (context.Request.Query["step"], CultureInfo.InvariantCulture, out var seconds) ? seconds : 5);

            // Runs after the response, so the caller (curl, a script) is not held for the length of the scenario.
            _ = Task.Run (() => RunLifecycleAsync (topic, step));
            await context.Response.WriteAsync ($"Publishing a warning, an alarm and an all clear to {topic}, {step.TotalSeconds:0.#} seconds apart.").ConfigureAwait (false);
        }

        // ---- helpers ----

        // Records the request, applies a queued failure, and checks the sign-in. Returns false when it has already answered.
        private bool Admit (HttpContext context)
        {
            var authorization = context.Request.Headers.Authorization.ToString ();
            int? failure = null;
            lock (gate) {
                requests.Add (new SeenRequest (context.Request.Method, context.Request.Path + context.Request.QueryString, authorization.Length == 0 ? null : authorization));
                if (failures.Count > 0)
                    failure = failures.Dequeue ();
            }

            if (failure is { } code) {
                context.Response.StatusCode = code;
                return false;
            }

            if (options.BasicUser is not null || options.Token is not null) {
                var expected = options.Token is not null
                    ? "Bearer " + options.Token
                    : "Basic " + Convert.ToBase64String (Encoding.UTF8.GetBytes ($"{options.BasicUser}:{options.BasicPassword}"));

                if (authorization != expected) {
                    context.Response.StatusCode = (int)HttpStatusCode.Unauthorized;
                    context.Response.Headers.WWWAuthenticate = "Basic realm=\"fake-ntfy\"";
                    return false;
                }
            }

            return true;
        }

        private static string? Header (HttpContext context, params string[] names)
        {
            foreach (var name in names)
                if (context.Request.Headers.TryGetValue (name, out var value) && value.Count > 0)
                    return value.ToString ();
            return null;
        }

        private static int ParsePriority (string? text)
            => text?.ToLowerInvariant () switch {
                "5" or "max" or "urgent" => 5,
                "4" or "high" => 4,
                "2" or "low" => 2,
                "1" or "min" => 1,
                _ => 3,
            };

        // since= is a duration (10m, 12h), a unix timestamp, a message id, or "all". An id this server does not know gets everything.
        private List<StoredMessage> Since (string topic, string since)
        {
            var forTopic = messages.Where (m => m.Topic == topic).ToList ();
            if (string.IsNullOrEmpty (since) || since == "all")
                return forTopic;

            var duration = Regex.Match (since, @"^(\d+)([smhd])$");
            if (duration.Success) {
                var n = int.Parse (duration.Groups[1].Value, CultureInfo.InvariantCulture);
                var span = duration.Groups[2].Value switch { "s" => TimeSpan.FromSeconds (n), "m" => TimeSpan.FromMinutes (n), "h" => TimeSpan.FromHours (n), _ => TimeSpan.FromDays (n) };
                var cutoff = DateTimeOffset.UtcNow.Subtract (span).ToUnixTimeSeconds ();
                return forTopic.Where (m => m.Time >= cutoff).ToList ();
            }

            if (long.TryParse (since, CultureInfo.InvariantCulture, out var timestamp) && timestamp > 1_000_000_000)
                return forTopic.Where (m => m.Time >= timestamp).ToList ();

            var index = forTopic.FindIndex (m => m.Id == since);
            return index < 0 ? forTopic : forTopic.Skip (index + 1).ToList ();
        }

        private static async Task WriteAsync (HttpContext context, string line)
        {
            await context.Response.WriteAsync (line + "\n").ConfigureAwait (false);
            await context.Response.Body.FlushAsync ().ConfigureAwait (false);      // a stream that is not flushed is not a stream
        }

        private static string ToJson (StoredMessage m, string kind)
        {
            using var stream = new MemoryStream ();
            using (var w = new Utf8JsonWriter (stream)) {
                w.WriteStartObject ();
                w.WriteString ("id", m.Id);
                w.WriteNumber ("time", m.Time);
                w.WriteString ("event", kind);
                w.WriteString ("topic", m.Topic);
                if (m.Priority != 3)
                    w.WriteNumber ("priority", m.Priority);         // ntfy omits the default
                if (m.Tags.Length > 0) {
                    w.WriteStartArray ("tags");
                    foreach (var tag in m.Tags)
                        w.WriteStringValue (tag);
                    w.WriteEndArray ();
                }

                if (m.Title is not null)
                    w.WriteString ("title", m.Title);
                w.WriteString ("message", m.Body);
                w.WriteEndObject ();
            }

            return Encoding.UTF8.GetString (stream.ToArray ());
        }

        private static string NewId ()
        {
            const string alphabet = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
            return string.Create (12, 0, (span, _) => {
                for (var i = 0; i < span.Length; i++)
                    span[i] = alphabet[RandomNumberGenerator.GetInt32 (alphabet.Length)];
            });
        }
    }
}
