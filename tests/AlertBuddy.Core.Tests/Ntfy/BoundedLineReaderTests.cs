using System.Text;
using AlertBuddy.Core.Ntfy;
using Xunit;

namespace AlertBuddy.Core.Tests.Ntfy
{
    public class BoundedLineReaderTests
    {
        /// <summary>A stream that hands back at most <c>chunk</c> bytes per read, the way a socket does.</summary>
        private sealed class ChunkedStream (byte[] data, int chunk) : Stream
        {
            private int position;

            public override int Read (byte[] buffer, int offset, int count) => Read (buffer.AsSpan (offset, count));

            public override int Read (Span<byte> buffer)
            {
                var n = Math.Min (Math.Min (buffer.Length, chunk), data.Length - position);
                data.AsSpan (position, n).CopyTo (buffer);
                position += n;
                return n;
            }

            public override ValueTask<int> ReadAsync (Memory<byte> buffer, CancellationToken cancellationToken = default) => new (Read (buffer.Span));

            public override bool CanRead => true;
            public override bool CanSeek => false;
            public override bool CanWrite => false;
            public override long Length => throw new NotSupportedException ();
            public override long Position { get => throw new NotSupportedException (); set => throw new NotSupportedException (); }
            public override void Flush () { }
            public override long Seek (long offset, SeekOrigin origin) => throw new NotSupportedException ();
            public override void SetLength (long value) => throw new NotSupportedException ();
            public override void Write (byte[] buffer, int offset, int count) => throw new NotSupportedException ();
        }

        private static async Task<List<LineReadResult>> ReadAll (string text, int chunk, int max = BoundedLineReader.DefaultMaxLineBytes)
        {
            var results = new List<LineReadResult> ();
            await foreach (var r in BoundedLineReader.ReadAsync (new ChunkedStream (Encoding.UTF8.GetBytes (text), chunk), max))
                results.Add (r);
            return results;
        }

        [Theory]
        [InlineData (1)]
        [InlineData (3)]
        [InlineData (7)]
        [InlineData (8192)]
        public async Task Lines_AreSplitTheSameWhereverTheReadsFall (int chunk)
        {
            var lines = await ReadAll ("one\ntwo\n\nfour\n", chunk);

            Assert.Equal (["one", "two", "", "four"], lines.Select (l => l.Line));
            Assert.All (lines, l => Assert.False (l.TooLong));
        }

        [Fact]
        public async Task CarriageReturns_AreTolerated ()
        {
            var lines = await ReadAll ("one\r\ntwo\r\n", 4);

            Assert.Equal (["one", "two"], lines.Select (l => l.Line));
        }

        [Fact]
        public async Task AMultiByteCharacter_SplitAcrossReads_IsDecodedWhole ()
        {
            // Decoding each chunk on its own would turn the halves of "°" into replacement characters. One byte per read forces the split.
            var lines = await ReadAll ("Workshop is at 41 °C ✅\n", 1);

            Assert.Equal (["Workshop is at 41 °C ✅"], lines.Select (l => l.Line));
        }

        [Fact]
        public async Task ALineExactlyAtTheLimit_IsKept_AndOneByteOverIsDiscardedOnce ()
        {
            var atLimit = new string ('a', 10);
            var overLimit = new string ('b', 11);

            var lines = await ReadAll ($"{atLimit}\n{overLimit}\nafter\n", 4, max: 10);

            Assert.Equal (atLimit, lines[0].Line);
            Assert.True (lines[1].TooLong);
            Assert.Null (lines[1].Line);
            Assert.Equal ("after", lines[2].Line);   // the stream stayed in step: the next line reads normally
            Assert.Equal (3, lines.Count);
        }

        [Fact]
        public async Task AnEndlessLine_IsNeverHeldInMemory_AndIsReportedOnceItsNewlineArrives ()
        {
            // 5 MB with no newline until the end, against a limit of 1 KB. The reader must skip it, not buffer it.
            var huge = new string ('x', 5_000_000);

            var lines = await ReadAll ($"{huge}\nok\n", 8192, max: 1024);

            Assert.Equal (2, lines.Count);
            Assert.True (lines[0].TooLong);
            Assert.Equal ("ok", lines[1].Line);
        }

        [Fact]
        public async Task ATrailingFragmentWithNoNewline_IsHandedOver_ForTheParserToReject ()
        {
            var lines = await ReadAll ("whole\n{\"id\":\"cut", 5);

            Assert.Equal (["whole", "{\"id\":\"cut"], lines.Select (l => l.Line));
        }

        [Fact]
        public async Task Cancellation_StopsTheRead ()
        {
            using var cts = new CancellationTokenSource ();
            await cts.CancelAsync ();

            await Assert.ThrowsAnyAsync<OperationCanceledException> (async () => {
                await foreach (var _ in BoundedLineReader.ReadAsync (new MemoryStream ([1, 2, 3]), cancellationToken: cts.Token)) {
                }
            });
        }
    }
}
