using System.Buffers;
using System.Runtime.CompilerServices;
using System.Text;

namespace AlertBuddy.Core.Ntfy
{
    /// <summary>One result from <see cref="BoundedLineReader"/>: a line, or a marker that a line was too long and was discarded.</summary>
    public readonly record struct LineReadResult (string? Line, bool TooLong);

    /// <summary>
    /// Reads newline-delimited text from a stream that never ends, holding at most <c>maxLineBytes</c> of a line in memory. A server
    /// that sent one endless line would otherwise grow a buffer until the app was killed (PLAN.md section 5.2: reject lines over 64 KB).
    /// </summary>
    public static class BoundedLineReader
    {
        /// <summary>The plan's limit for one line.</summary>
        public const int DefaultMaxLineBytes = 64 * 1024;

        /// <summary>
        /// Yields each line without its terminator. A line longer than the limit is skipped up to its newline and reported once as
        /// <see cref="LineReadResult.TooLong"/>, so the stream stays in step and the next line is read normally.
        /// </summary>
        public static async IAsyncEnumerable<LineReadResult> ReadAsync (
            Stream stream,
            int maxLineBytes = DefaultMaxLineBytes,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull (stream);
            ArgumentOutOfRangeException.ThrowIfLessThanOrEqual (maxLineBytes, 0);

            var chunk = ArrayPool<byte>.Shared.Rent (8192);
            var line = new ArrayBufferWriter<byte> ();
            var skipping = false;   // inside an over-long line, discarding until its newline

            try {
                while (true) {
                    var read = await stream.ReadAsync (chunk.AsMemory (), cancellationToken).ConfigureAwait (false);
                    if (read == 0) {
                        // The stream ended. A trailing fragment with no newline is a line the connection dropped in the middle of, which
                        // would not parse; hand it over anyway so the parser, not this reader, decides what it is.
                        if (!skipping && line.WrittenCount > 0)
                            yield return new LineReadResult (Decode (line.WrittenSpan), false);
                        yield break;
                    }

                    // An index, not a Span: a Span cannot live across the yield below.
                    var position = 0;
                    while (position < read) {
                        var newline = Array.IndexOf (chunk, (byte)'\n', position, read - position);
                        var end = newline < 0 ? read : newline;
                        var length = end - position;

                        if (skipping) {
                            // still discarding an over-long line
                        } else if (line.WrittenCount + length > maxLineBytes) {
                            skipping = true;
                            line.Clear ();
                        } else {
                            line.Write (chunk.AsSpan (position, length));
                        }

                        if (newline < 0)
                            break;

                        if (skipping) {
                            skipping = false;
                            yield return new LineReadResult (null, true);
                        } else {
                            var text = Decode (line.WrittenSpan);
                            line.Clear ();
                            yield return new LineReadResult (text, false);
                        }

                        position = newline + 1;
                    }
                }
            } finally {
                ArrayPool<byte>.Shared.Return (chunk);
            }
        }

        private static string Decode (ReadOnlySpan<byte> bytes)
        {
            // Tolerate CRLF: a proxy may add the carriage return that ntfy itself does not send.
            if (bytes.Length > 0 && bytes[^1] == (byte)'\r')
                bytes = bytes[..^1];
            return Encoding.UTF8.GetString (bytes);
        }
    }
}
