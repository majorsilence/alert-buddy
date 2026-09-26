using AlertBuddy.Core.Ntfy;
using AlertBuddy.TestSupport;

namespace AlertBuddy.Core.Tests.Ntfy
{
    /// <summary>Runs an <see cref="NtfySubscription"/> against the scripted fake server in fake time, and records what came out.</summary>
    internal sealed class SubscriptionRig : IAsyncDisposable
    {
        private readonly object gate = new ();
        private readonly List<NtfyEvent> events = [];
        private readonly List<ConnectionInfo> states = [];
        private readonly CancellationTokenSource cancellation = new ();
        private Task? reader;

        public FakeNtfyServer Server { get; } = new ();
        public TestClock Clock { get; } = new ();
        public NtfySubscription Subscription { get; }
        public Exception? ReaderFailure { get; private set; }

        /// <summary>The newest message id consumed, mirroring what the engine persists.</summary>
        public string? LastId { get; private set; }

        public NtfyCredentials Credentials { get; set; } = NtfyCredentials.None;

        public SubscriptionRig (Func<NtfySubscriptionOptions, NtfySubscriptionOptions>? configure = null, string baseAddress = "https://ntfy.example.com")
        {
            var options = new NtfySubscriptionOptions (new Uri (baseAddress), "home-alerts") {
                Credentials = () => Credentials,
                LastMessageId = () => LastId,
            };

            Subscription = new NtfySubscription (configure?.Invoke (options) ?? options, Server, Clock.Provider, jitter: () => 0.5);
            Subscription.StateChanged += info => {
                lock (gate)
                    states.Add (info);
            };
        }

        public IReadOnlyList<NtfyEvent> Events { get { lock (gate) return events.ToList (); } }

        public IReadOnlyList<NtfyEvent> Messages => Events.Where (e => e.Kind == NtfyEventKind.Message).ToList ();

        public IReadOnlyList<ConnectionInfo> States { get { lock (gate) return states.ToList (); } }

        public ConnectionInfo Current => Subscription.Current;

        public void Start ()
        {
            reader = Task.Run (async () => {
                try {
                    await foreach (var e in Subscription.ReadAsync (cancellation.Token)) {
                        lock (gate) {
                            events.Add (e);
                            if (e.Message is { } m)
                                LastId = m.Id;
                        }
                    }
                } catch (OperationCanceledException) {
                } catch (Exception ex) {
                    ReaderFailure = ex;
                }
            });
        }

        public Task RequestsAsync (int count) => Wait.UntilAsync (() => Server.Requests.Count >= count, $"{count} requests (saw {Server.Requests.Count})");

        public Task MessagesAsync (int count) => Wait.UntilAsync (() => Messages.Count >= count, $"{count} messages (saw {Messages.Count})");

        public Task StateAsync (ConnectionState state, string what) => Wait.UntilAsync (() => Current.State == state, what);

        public async Task StopAsync ()
        {
            await cancellation.CancelAsync ();
            if (reader is not null)
                await reader.WaitAsync (TimeSpan.FromSeconds (10));
        }

        public async ValueTask DisposeAsync ()
        {
            try { await StopAsync (); } catch (TimeoutException) { }
            cancellation.Dispose ();
        }

        // ---- json helpers ----

        public static string Message (string id, string title = "Workshop: temperature alarm", int priority = 5, long time = 1790000500, string body = "Workshop is at 52 °C")
            => $$"""{"id":"{{id}}","time":{{time}},"event":"message","topic":"home-alerts","priority":{{priority}},"title":"{{title}}","message":"{{body}}"}""";

        /// <summary>A stream connection that has been scripted, started, and reached.</summary>
        public async Task<StreamHandle> ConnectedStreamAsync (params string[] history)
        {
            Server.EnqueuePoll (history);
            var stream = Server.EnqueueStream ();
            Start ();
            await stream.Connected;
            return stream;
        }
    }
}
