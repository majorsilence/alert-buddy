using System.Drawing;
using AlertBuddy.Shared.Theme;
using Majorsilence.Forms;
using Majorsilence.Forms.Drawing;

namespace AlertBuddy.Shared.Controls
{
    /// <summary>
    /// Press and hold; a ring fills, then it fires. The grown-up gate and the alarm takeover's "Got it" (PLAN.md section 8.5). Cancels
    /// cleanly on release, so an accidental brush of the button does nothing.
    /// </summary>
    public class HoldButton : PaperSurface
    {
        // Majorsilence.Forms.Timer is ambiguous with System.Threading.Timer under ImplicitUsings; qualify it.
        private readonly Majorsilence.Forms.Timer timer = new () { Interval = 16 };
        private DateTime? startedAt;
        private double progress;

        /// <summary>Creates a hold button.</summary>
        public HoldButton ()
        {
            Height = 56;
            FillColor = AlertPalette.Paper;
            OutlineColor = AlertPalette.GrapeInk;
            timer.Tick += (_, _) => Advance ();
        }

        /// <summary>How long the button must be held before <see cref="Held"/> fires.</summary>
        public TimeSpan HoldDuration { get; set; } = TimeSpan.FromSeconds (2);

        /// <inheritdoc/>
        protected override void OnTextChanged (EventArgs e)
        {
            base.OnTextChanged (e);
            Invalidate ();
        }

        /// <summary>Raised once the button has been held for <see cref="HoldDuration"/>.</summary>
        public event EventHandler? Held;

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

            if (!Enabled)
                return;

            Pressed = true;
            startedAt = DateTime.UtcNow;
            progress = 0;
            timer.Enabled = true;
        }

        /// <inheritdoc/>
        protected override void OnMouseUp (MouseEventArgs e)
        {
            base.OnMouseUp (e);
            Cancel ();
        }

        /// <inheritdoc/>
        protected override void OnMouseLeave (EventArgs e)
        {
            base.OnMouseLeave (e);
            Cancel ();
        }

        private void Cancel ()
        {
            if (startedAt is null)
                return;

            Pressed = false;
            startedAt = null;
            progress = 0;
            timer.Enabled = false;
            Invalidate ();
        }

        private void Advance ()
        {
            if (startedAt is not { } started)
                return;

            progress = Math.Min (1.0, (DateTime.UtcNow - started).TotalMilliseconds / HoldDuration.TotalMilliseconds);
            Invalidate ();

            if (progress >= 1.0) {
                timer.Enabled = false;
                startedAt = null;
                progress = 0;
                Pressed = false;
                Held?.Invoke (this, EventArgs.Empty);
            }
        }

        /// <inheritdoc/>
        protected override void OnPaint (PaintEventArgs e)
        {
            base.OnPaint (e);

            var g = e.Graphics;
            g.ResetTransform ();

            var w = ClientSize.Width;
            var h = ClientSize.Height;
            var bounds = new RectangleF (0, Pressed ? ShadowOffset : 0, w - ShadowOffset, h - ShadowOffset);

            if (progress > 0) {
                using var ringPen = new Pen (AlertPalette.Butter, 4f);
                var ring = new RectangleF (bounds.X + 3, bounds.Y + 3, bounds.Width - 6, bounds.Height - 6);
                g.DrawArc (ringPen, ring, -90, (float)(progress * 360));
            }

            if (string.IsNullOrEmpty (Text))
                return;

            using var font = AlertFonts.Body (16, bold: true);
            using var brush = new SolidBrush (AlertPalette.GrapeInk);
            var lines = TextLayout.Wrap (g, Text, font, bounds.Width - 12);
            TextLayout.DrawCentered (g, lines, font, brush, bounds);
        }

        /// <inheritdoc/>
        protected override void Dispose (bool disposing)
        {
            if (disposing)
                timer.Dispose ();

            base.Dispose (disposing);
        }
    }
}
