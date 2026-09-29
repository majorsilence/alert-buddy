using System.Drawing;
using Majorsilence.Forms.Drawing;

namespace AlertBuddy.Shared.Theme
{
    /// <summary>
    /// Word-wraps and centres a run of text by hand. The framework's own <c>Graphics.DrawString (..., RectangleF)</c> overload wraps
    /// correctly but only draws top-left; the <c>StringFormat</c> overload honours <c>Alignment</c>/<c>LineAlignment</c> but measures and
    /// clips the whole string as a single line (confirmed by rendering <see cref="Controls.SpeechBubble"/> headlessly and finding the
    /// status sentence clipped to a single centred, cropped line). Centred, multi-line text needs both, so this does the wrap itself.
    /// </summary>
    public static class TextLayout
    {
        /// <summary>Breaks <paramref name="text"/> into lines that each fit within <paramref name="maxWidth"/> logical pixels.</summary>
        public static IReadOnlyList<string> Wrap (Graphics g, string text, Font font, float maxWidth)
        {
            if (string.IsNullOrEmpty (text))
                return [];

            var words = text.Split (' ', StringSplitOptions.RemoveEmptyEntries);
            var lines = new List<string> ();
            var current = "";

            foreach (var word in words) {
                var candidate = current.Length == 0 ? word : current + " " + word;
                if (g.MeasureString (candidate, font).Width > maxWidth && current.Length > 0) {
                    lines.Add (current);
                    current = word;
                } else {
                    current = candidate;
                }
            }

            if (current.Length > 0)
                lines.Add (current);

            return lines;
        }

        /// <summary>Draws <paramref name="lines"/> centred both horizontally (per line) and vertically (as a block) within <paramref name="bounds"/>.</summary>
        public static void DrawCentered (Graphics g, IReadOnlyList<string> lines, Font font, Brush brush, RectangleF bounds)
        {
            if (lines.Count == 0)
                return;

            var lineHeight = g.MeasureString ("Ag", font).Height;
            var totalHeight = lineHeight * lines.Count;
            var y = bounds.Y + (bounds.Height - totalHeight) / 2f;

            foreach (var line in lines) {
                var width = g.MeasureString (line, font).Width;
                var x = bounds.X + (bounds.Width - width) / 2f;
                g.DrawString (line, font, brush, x, y);
                y += lineHeight;
            }
        }
    }
}
