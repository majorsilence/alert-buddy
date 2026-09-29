using System.Drawing;
using AlertBuddy.Shared.Theme;
using Majorsilence.Forms;
using Majorsilence.Forms.Drawing;

namespace AlertBuddy.Shared.Controls
{
    /// <summary>
    /// The big pill button of PLAN.md section 8.5: the hard shadow collapses on press, 64 logical pixels high minimum, the primary way a
    /// child acts on a screen.
    /// </summary>
    public class ChunkyButton : PaperSurface
    {
        /// <summary>Creates a chunky button.</summary>
        public ChunkyButton ()
        {
            Height = 72;
            FillColor = AlertPalette.Paper;
            OutlineColor = AlertPalette.GrapeInk;
        }

        /// <summary>The label's colour. Grape ink by default; the alarm takeover's Cherry-filled button sets Paper instead (PLAN.md section 8.3's wireframe: "Paper on Cherry").</summary>
        public Color TextColor { get; set; } = AlertPalette.GrapeInk;

        /// <inheritdoc/>
        protected override void OnTextChanged (EventArgs e)
        {
            base.OnTextChanged (e);
            Invalidate ();
        }

        /// <inheritdoc/>
        protected override (float TopLeft, float TopRight, float BottomRight, float BottomLeft) CornerRadii {
            get {
                var radius = (ClientSize.Height - ShadowOffset) / 2f;
                return (radius, radius, radius, radius);
            }
        }

        /// <inheritdoc/>
        protected override void OnMouseDown (MouseEventArgs e)
        {
            base.OnMouseDown (e);
            Pressed = true;
        }

        /// <inheritdoc/>
        protected override void OnMouseUp (MouseEventArgs e)
        {
            base.OnMouseUp (e);
            Pressed = false;
        }

        /// <inheritdoc/>
        protected override void OnMouseLeave (EventArgs e)
        {
            base.OnMouseLeave (e);
            Pressed = false;
        }

        /// <inheritdoc/>
        protected override void OnPaint (PaintEventArgs e)
        {
            base.OnPaint (e);

            if (string.IsNullOrEmpty (Text))
                return;

            var g = e.Graphics;
            g.ResetTransform ();
            g.ScaleTransform ((float)e.Scaling, (float)e.Scaling);

            var w = ClientSize.Width / (float)e.Scaling;
            var h = ClientSize.Height / (float)e.Scaling;
            var bounds = new RectangleF (0, Pressed ? ShadowOffset : 0, w - ShadowOffset, h - ShadowOffset);

            using var font = AlertFonts.Display (20);
            using var brush = new SolidBrush (Enabled ? TextColor : Color.FromArgb (0x80, TextColor));
            var lines = TextLayout.Wrap (g, Text, font, bounds.Width - 16);
            TextLayout.DrawCentered (g, lines, font, brush, bounds);
        }
    }
}
