using System.Net;
using System.Text;
using System.Threading.Channels;

namespace AlertBuddy.TestSupport
{
    /// <summary>One request the client made, for the test to inspect.</summary>
    public sealed record RecordedRequest (Uri Uri, string? Authorization, string? UserAgent)
    {
        /// <summary>The value of a query parameter, or null.</summary>
        public string? Query (string name)
        {
            foreach (var pair in Uri.Query.TrimStart ('?').Split ('&', StringSplitOptions.RemoveEmptyEntries)) {
                var kv = pair.Split ('=', 2);
                if (kv[0] == name)
                    return kv.Length > 1 ? Uri.UnescapeDataString (kv[1]) : "";
            }

            return null;
        }

        /// <summary>The <c>since</c> parameter.</summary>
        public string? Since => Query ("since");

        /// <summary>Whether this is a one-shot history request (<c>poll=1</c>) rather than a held-open stream.</summary>
        public bool IsPoll => Query ("poll") == "1";
    }

    /// <summary>The test's end of one held-open stream: push lines, end it cleanly, or break it.</summary>
    public sealed class StreamHandle
    {
        private readonly Channel<byte[]> lines = Channel.CreateUnbounded<byte[]> ();
        private readonly TaskCompletionSource connected = new (TaskCreationOptions.RunContinuationsAsynchronously);
        private Exception? failure;

        internal Stream OpenBody () => new ChannelBackedStream (lines.Reader, () => failure);

        internal void MarkConnected () => connected.TrySetResult ();

        /// <summary>Completes when the client has connected to this stream. Bounded: if the client never connects the test fails after ten seconds instead of hanging the run.</summary>
        public Task Connected => connected.Task.WaitAsync (TimeSpan.FromSeconds (10));

        /// <summary>Sends one line, newline-terminated, as ntfy does.</summary>
        public void Send (string line) => SendRaw (Encoding.UTF8.GetBytes (line + "\n"));

        /// <summary>Sends bytes exactly as given, for splitting a line across reads or sending an endless one.</summary>
        public void SendRaw (byte[] bytes) => lines.Writer.TryWrite (bytes);

        /// <summary>Ends the stream cleanly, as a server restart or a proxy timeout does.</summary>
        public void End () => lines.Writer.TryComplete ();

        /// <summary>Breaks the stream with an exception, as a dropped network does.</summary>
        public void Fail (Exception exception)
        {
            failure = exception;
            lines.Writer.TryComplete ();
        }
    }

    internal sealed class ChannelBackedStream (ChannelReader<byte[]> reader, Func<Exception?> failure) : Stream
    {
        private byte[] current = [];
        private int offset;

        public override async ValueTask<int> ReadAsync (Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            while (offset >= current.Length) {
                if (!await reader.WaitToReadAsync (cancellationToken).ConfigureAwait (false)) {
                    if (failure () is { } ex)
                        throw ex;
                    return 0;
                }

                if (reader.TryRead (out var next)) {
                    current = next;
                    offset = 0;
                }
            }

            var n = Math.Min (buffer.Length, current.Length - offset);
            current.AsMemory (offset, n).CopyTo (buffer);
            offset += n;
            return n;
        }

        public override int Read (byte[] buffer, int offset, int count) => ReadAsync (buffer.AsMemory (offset, count)).AsTask ().GetAwaiter ().GetResult ();
        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException ();
        public override long Position { get => throw new NotSupportedException (); set => throw new NotSupportedException (); }
        public override void Flush () { }
        public override long Seek (long o, SeekOrigin origin) => throw new NotSupportedException ();
        public override void SetLength (long value) => throw new NotSupportedException ();
        public override void Write (byte[] buffer, int o, int count) => throw new NotSupportedException ();
    }

    /// <summary>
    /// A scripted ntfy server in process, as an <see cref="HttpMessageHandler"/>: no sockets, no ports, and every outcome (a held-open
    /// stream, a status code, a network failure) queued by the test in the order the client will hit them. Nothing here is a real host.
    /// </summary>
    public sealed class FakeNtfyServer : HttpMessageHandler
    {
        private readonly object gate = new ();
        private readonly Queue<Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>>> script = new ();
        private readonly List<RecordedRequest> requests = [];

        /// <summary>What happens when a request arrives and nothing is queued. By default the test fails loudly, so an unexpected extra attempt is seen.</summary>
        public Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>>? WhenEmpty { get; set; }

        /// <summary>Every request so far, oldest first.</summary>
        public IReadOnlyList<RecordedRequest> Requests {
            get {
                lock (gate)
                    return requests.ToList ();
            }
        }

        /// <summary>The next connection is a held-open stream the test controls.</summary>
        public StreamHandle EnqueueStream ()
        {
            var handle = new StreamHandle ();
            Enqueue ((_, _) => {
                handle.MarkConnected ();
                return Task.FromResult (new HttpResponseMessage (HttpStatusCode.OK) { Content = new StreamContent (handle.OpenBody ()) });
            });
            return handle;
        }

        /// <summary>The next connection is a one-shot response with these lines, then the end of the body (a <c>poll=1</c> history request).</summary>
        public void EnqueuePoll (params string[] lines)
            => Enqueue ((_, _) => Task.FromResult (new HttpResponseMessage (HttpStatusCode.OK) {
                Content = new StringContent (string.Concat (lines.Select (l => l + "\n")), Encoding.UTF8),
            }));

        /// <summary>The next connection is answered with a status code and, optionally, a <c>Retry-After</c>.</summary>
        public void EnqueueStatus (HttpStatusCode status, TimeSpan? retryAfter = null)
            => Enqueue ((_, _) => {
                var response = new HttpResponseMessage (status) { Content = new StringContent ("") };
                if (retryAfter is { } wait)
                    response.Headers.RetryAfter = new System.Net.Http.Headers.RetryConditionHeaderValue (wait);
                return Task.FromResult (response);
            });

        /// <summary>The next connection fails before any response, as an unreachable network or a bad certificate does.</summary>
        public void EnqueueException (Exception exception) => Enqueue ((_, _) => throw exception);

        /// <summary>The next connection is accepted but never answered, as a black-holed route is.</summary>
        public void EnqueueSilence () => Enqueue (async (_, ct) => {
            await Task.Delay (Timeout.Infinite, ct).ConfigureAwait (false);
            throw new InvalidOperationException ("unreachable");
        });

        /// <summary>Queues a custom behaviour.</summary>
        public void Enqueue (Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> behaviour)
        {
            lock (gate)
                script.Enqueue (behaviour);
        }

        /// <inheritdoc />
        protected override Task<HttpResponseMessage> SendAsync (HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>>? behaviour;
            lock (gate) {
                requests.Add (new RecordedRequest (request.RequestUri!, request.Headers.Authorization?.ToString (), request.Headers.UserAgent.ToString ()));
                behaviour = script.Count > 0 ? script.Dequeue () : WhenEmpty;
            }

            if (behaviour is null)
                throw new InvalidOperationException ($"The client made request #{Requests.Count} ({request.RequestUri}) but the test scripted nothing for it.");

            return behaviour (request, cancellationToken);
        }
    }

    /// <summary>Waits for a condition that another thread will make true, without sleeping for a fixed time.</summary>
    public static class Wait
    {
        /// <summary>Polls until <paramref name="condition"/> holds, or fails with <paramref name="what"/> after the timeout.</summary>
        public static async Task UntilAsync (Func<bool> condition, string what, TimeSpan? timeout = null)
        {
            var deadline = DateTime.UtcNow + (timeout ?? TimeSpan.FromSeconds (10));
            while (!condition ()) {
                if (DateTime.UtcNow > deadline)
                    throw new TimeoutException ($"Timed out waiting for: {what}");
                await Task.Delay (2).ConfigureAwait (false);
            }
        }
    }
}
