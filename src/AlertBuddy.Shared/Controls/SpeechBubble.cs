using System.Drawing;
using AlertBuddy.Shared.Theme;
using Majorsilence.Forms;
using Majorsilence.Forms.Drawing;

namespace AlertBuddy.Shared.Controls
{
    /// <summary>The status sentence, with a tail toward the buddy (PLAN.md section 8.5).</summary>
    public class SpeechBubble : PaperSurface
    {
        /// <summary>Creates a speech bubble.</summary>
        public SpeechBubble ()
        {
            FillColor = AlertPalette.Paper;
            OutlineColor = AlertPalette.GrapeInk;
        }

        /// <inheritdoc/>
        protected override void OnTextChanged (EventArgs e)
        {
            base.OnTextChanged (e);
            Invalidate ();
        }

        /// <inheritdoc/>
        protected override RectangleF GetSurfaceBounds (float logicalWidth, float logicalHeight)
            => new (0, 0, logicalWidth - ShadowOffset, logicalHeight - ShadowOffset - TailHeight);

        /// <inheritdoc/>
        protected override void OnPaint (PaintEventArgs e)
        {
            base.OnPaint (e);

            var g = e.Graphics;
            g.ResetTransform ();
            g.ScaleTransform ((float)e.Scaling, (float)e.Scaling);

            var w = ClientSize.Width / (float)e.Scaling;
            var h = ClientSize.Height / (float)e.Scaling;
            var body = GetSurfaceBounds (w, h);

            // The tail: a small triangle under the bubble, pointing up at the buddy above it.
            var tailCenter = body.Width / 2f;
            var tail = new PointF[] {
                new (tailCenter - 14, body.Bottom - 1),
                new (tailCenter + 14, body.Bottom - 1),
                new (tailCenter, body.Bottom + TailHeight),
            };
            using (var tailBrush = new SolidBrush (FillColor))
                g.FillPolygon (tailBrush, tail);
            using (var tailPen = new Pen (OutlineColor, OutlineWidth))
                g.DrawLines (tailPen, [tail[0], tail[2], tail[1]]);

            if (string.IsNullOrEmpty (Text))
                return;

            using var font = AlertFonts.Display (18);
            using var brush = new SolidBrush (AlertPalette.GrapeInk);
            var textBounds = new RectangleF (body.X + 16, body.Y + 8, body.Width - 32, body.Height - 16);
            var lines = TextLayout.Wrap (g, Text, font, textBounds.Width);
            TextLayout.DrawCentered (g, lines, font, brush, textBounds);
        }

        private const float TailHeight = 14f;
    }
}
