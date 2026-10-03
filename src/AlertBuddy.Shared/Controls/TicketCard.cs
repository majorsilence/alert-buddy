using System.Drawing;
using AlertBuddy.Core.Alerts;
using AlertBuddy.Shared.Theme;
using Majorsilence.Forms;
using Majorsilence.Forms.Drawing;

namespace AlertBuddy.Shared.Controls
{
    /// <summary>
    /// One alert: source, time, one sentence, a state-coloured left edge, seeded corners (PLAN.md section 8.5). Draws its own thermometer
    /// glyph inline rather than as the separate <c>ThermoGlyph</c> control the plan lists, to keep one control doing one alert's whole
    /// ticket; split it out if another control ever needs the glyph on its own.
    /// </summary>
    public class TicketCard : PaperSurface
    {
        private const float EdgeWidth = 8f;

        /// <summary>Creates a ticket card.</summary>
        public TicketCard ()
        {
            Height = 88;
            FillColor = AlertPalette.Paper;
            OutlineColor = AlertPalette.GrapeInk;
        }

        // A screen reader reads this, not the painted card: where, what, and how long ago.
        private void Describe ()
        {
            AccessibleName = string.Join (" ", new[] { source, sentence, timeAgo }.Where (part => part.Length > 0));
            AccessibleRole = AccessibleRole.StaticText;
        }

        /// <summary>The room or device the alert came from.</summary>
        public string Source { get => source; set { if (source == value) return; source = value; Describe (); Invalidate (); } }
        private string source = "";

        /// <summary>How long ago, "22 min".</summary>
        public string TimeAgo { get => timeAgo; set { if (timeAgo == value) return; timeAgo = value; Describe (); Invalidate (); } }
        private string timeAgo = "";

        /// <summary>"41 degrees. Keep an eye on it."</summary>
        public string Sentence { get => sentence; set { if (sentence == value) return; sentence = value; Describe (); Invalidate (); } }
        private string sentence = "";

        /// <summary>The parsed temperature, when the alert carried one. Draws the thermometer glyph when set.</summary>
        public double? Temperature { get => temperature; set { if (temperature == value) return; temperature = value; Invalidate (); } }
        private double? temperature;

        /// <summary>What the left edge and glyph are coloured by.</summary>
        public AlertLevel Level { get => level; set { if (level == value) return; level = value; Invalidate (); } }
        private AlertLevel level;

        /// <summary>Resolved alerts show the calm colour regardless of the level they were raised at.</summary>
        public AlertStatus Status { get => status; set { if (status == value) return; status = value; Invalidate (); } }
        private AlertStatus status;

        private Color EdgeColor => Status == AlertStatus.Resolved ? AlertPalette.Mint
            : Level == AlertLevel.Alarm ? AlertPalette.Cherry
            : Level == AlertLevel.Warning ? AlertPalette.Tangerine
            : AlertPalette.Butter;

        /// <inheritdoc/>
        protected override void OnPaint (PaintEventArgs e)
        {
            base.OnPaint (e);

            var g = e.Graphics;
            g.ResetTransform ();

            var w = ClientSize.Width;
            var h = ClientSize.Height;
            var bounds = GetSurfaceBounds (w, h);

            // The state-coloured left edge, inset from the outline so it reads as a stripe, not an overpaint of it.
            var (tl, _, _, bl) = CornerRadii;
            var edge = new RectangleF (bounds.X + OutlineWidth, bounds.Y + OutlineWidth, EdgeWidth, bounds.Height - OutlineWidth * 2);
            using (var edgeBrush = new SolidBrush (EdgeColor))
                g.FillRoundedRectangle (edgeBrush, edge, tl, 0, 0, bl);

            var textLeft = edge.Right + 14;
            using var sourceFont = AlertFonts.Display (18);
            using var bodyFont = AlertFonts.Body (16);
            using var textBrush = new SolidBrush (AlertPalette.GrapeInk);
            using var mutedBrush = new SolidBrush (Color.FromArgb (0xB0, AlertPalette.GrapeInk));

            g.DrawString (source, sourceFont, textBrush, new PointF (textLeft, bounds.Y + 10));

            using (var format = new StringFormat { Alignment = StringAlignment.Far })
                g.DrawString (timeAgo, bodyFont, mutedBrush, new RectangleF (textLeft, bounds.Y + 12, bounds.Right - textLeft - 8, 28), format);

            var glyphWidth = temperature is not null ? DrawThermoGlyph (g, bounds, textLeft) : 0f;
            g.DrawString (sentence, bodyFont, textBrush, new RectangleF (textLeft + glyphWidth, bounds.Y + 42, bounds.Width - (textLeft - bounds.X) - glyphWidth - 8, bounds.Height - 50));
        }

        // A chunky thermometer, filled to the parsed temperature between the two warning/alarm levels, tick marks at each.
        private float DrawThermoGlyph (Graphics g, RectangleF bounds, float x)
        {
            const float glyphWidth = 14f, glyphHeight = 40f;
            var top = bounds.Y + 40;
            var tube = new RectangleF (x, top, glyphWidth, glyphHeight);

            using (var outline = new Pen (AlertPalette.GrapeInk, 2f))
                g.DrawRoundedRectangle (outline, tube, glyphWidth / 2);

            // Warning starts the fill at a third full, alarm-hot at two-thirds -- there is no fixed scale here, only a felt sense of "rising".
            var fraction = Level == AlertLevel.Alarm ? 0.85f : Level == AlertLevel.Warning ? 0.55f : 0.3f;
            var fillHeight = glyphHeight * fraction;
            var fill = new RectangleF (x + 3, top + glyphHeight - fillHeight + 3, glyphWidth - 6, fillHeight - 6);
            using (var fillBrush = new SolidBrush (EdgeColor))
                g.FillRoundedRectangle (fillBrush, fill, (glyphWidth - 6) / 2);

            return glyphWidth + 12;
        }
    }
}
