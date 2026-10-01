using System.Drawing;
using AlertBuddy.Shared.Theme;
using Majorsilence.Forms;
using Majorsilence.Forms.Drawing;

namespace AlertBuddy.Shared.Controls
{
    /// <summary>
    /// The cut-paper-and-stickers base every custom control derives from (PLAN.md section 8.5): a 3px outline, a hard offset shadow (no
    /// blur) that collapses on press, and corner radii that differ by corner so a shape never looks machine-drawn. The corner radii are
    /// picked once per instance from a seed stored with the control, so a shape never changes between repaints.
    /// </summary>
    public class PaperSurface : Control
    {
        private static int nextSeed = 1;

        private readonly int seed = nextSeed++;
        private (float TopLeft, float TopRight, float BottomRight, float BottomLeft)? corners;
        private bool pressed;

        /// <summary>The surface's fill colour.</summary>
        public Color FillColor { get; set; } = AlertPalette.Paper;

        /// <summary>The outline and shadow colour. Grape ink on a light surface, per PLAN.md section 8.2.</summary>
        public Color OutlineColor { get; set; } = AlertPalette.GrapeInk;

        /// <summary>The outline stroke width, in logical pixels.</summary>
        public float OutlineWidth { get; set; } = 3f;

        /// <summary>How far the hard shadow sits down and to the right, in logical pixels. Zero draws no shadow.</summary>
        public float ShadowOffset { get; set; } = 4f;

        /// <summary>
        /// Whether the surface is pressed: the hard shadow collapses (the shape sits flush) as a raised-then-pushed-in cue. Derived
        /// controls set this from their own pointer state.
        /// </summary>
        public bool Pressed {
            get => pressed;
            set {
                if (pressed == value)
                    return;

                pressed = value;
                Invalidate ();
            }
        }

        /// <summary>
        /// The per-corner radii, in logical pixels, seeded so they are stable across repaints (10 to 18, PLAN.md section 8.2). A pill
        /// shape (<see cref="ChunkyButton"/>, <see cref="HoldButton"/>) overrides this instead of using the seeded jitter.
        /// </summary>
        protected virtual (float TopLeft, float TopRight, float BottomRight, float BottomLeft) CornerRadii {
            get {
                if (corners is { } cached)
                    return cached;

                var random = new Random (seed);
                var computed = (
                    TopLeft: 10f + (float)random.NextDouble () * 8f,
                    TopRight: 10f + (float)random.NextDouble () * 8f,
                    BottomRight: 10f + (float)random.NextDouble () * 8f,
                    BottomLeft: 10f + (float)random.NextDouble () * 8f);
                corners = computed;
                return computed;
            }
        }

        /// <summary>Fills and outlines the paper shape into <paramref name="bounds"/>, in logical pixels, with the shadow behind it.</summary>
        protected void PaintSurface (Graphics g, RectangleF bounds)
        {
            ArgumentNullException.ThrowIfNull (g);

            var (tl, tr, br, bl) = CornerRadii;

            if (!pressed && ShadowOffset > 0) {
                var shadow = new RectangleF (bounds.X + ShadowOffset, bounds.Y + ShadowOffset, bounds.Width, bounds.Height);
                using var shadowBrush = new SolidBrush (OutlineColor);
                g.FillRoundedRectangle (shadowBrush, shadow, tl, tr, br, bl);
            }

            var shape = pressed
                ? new RectangleF (bounds.X + ShadowOffset, bounds.Y + ShadowOffset, bounds.Width, bounds.Height)
                : bounds;

            using var fillBrush = new SolidBrush (FillColor);
            g.FillRoundedRectangle (fillBrush, shape, tl, tr, br, bl);

            using var outlinePen = new Pen (OutlineColor, OutlineWidth);
            g.DrawRoundedRectangle (outlinePen, shape, tl, tr, br, bl);
        }

        /// <summary>
        /// The logical-pixel rectangle the paper shape fills, given the control's own logical size. The default is the whole control
        /// less room for the shadow; <see cref="SpeechBubble"/> reserves space below it for its tail instead.
        /// </summary>
        protected virtual RectangleF GetSurfaceBounds (float logicalWidth, float logicalHeight)
            => new (0, 0, logicalWidth - ShadowOffset, logicalHeight - ShadowOffset);

        /// <inheritdoc/>
        protected override void OnPaint (PaintEventArgs e)
        {
            base.OnPaint (e);

            var g = e.Graphics;

            var w = ClientSize.Width;
            var h = ClientSize.Height;
            PaintSurface (g, GetSurfaceBounds (w, h));
        }
    }
}
