using System.Drawing;
using AlertBuddy.Shared.Theme;
using AlertBuddy.ViewModels.Screens;
using Majorsilence.Forms;
using Majorsilence.Forms.Drawing;

namespace AlertBuddy.Shared.Controls
{
    /// <summary>
    /// The hero: dome, lamp, rotating beam, base and face (PLAN.md section 8.6). The single memorable thing in the app, so everything
    /// else in section 8.5 stays quiet and lets this be the star.
    /// </summary>
    /// <remarks>
    /// A first, real pass at the brief rather than every micro-behaviour in the table: the eyes blink and the mouth and lamp change with
    /// <see cref="Level"/>, the beam sweeps and speeds up for <see cref="BeaconMood.Alarm"/>, and <see cref="ReduceMotion"/> stops the
    /// rotation and shake. Eyes following a touch, the sweat drop and the long-press wiggle are not built yet -- worth adding once this
    /// is on screen and looked at (PLAN.md section 8.5's "look at it, do not ship the first render").
    /// </remarks>
    public class BeaconBuddy : Control
    {
        // PLAN.md 8.6: "try 16 ms, measure, fall back to 33 ms". No frame-time measurement has been done on this platform yet, so this
        // starts at the documented fallback rather than guessing 16 ms is safe.
        // Majorsilence.Forms.Timer is ambiguous with System.Threading.Timer under ImplicitUsings; qualify it.
        private readonly Majorsilence.Forms.Timer timer = new () { Interval = 33, Enabled = true };
        private double beamAngle;
        private double phase;
        private bool blinkClosed;
        private double blinkAt = 3;

        /// <summary>Creates the beacon.</summary>
        public BeaconBuddy ()
        {
            Width = 220;
            Height = 220;
            timer.Tick += (_, _) => Advance ();
        }

        /// <summary>What the beacon shows.</summary>
        public BeaconMood Level { get => level; set { if (level == value) return; level = value; Invalidate (); } }
        private BeaconMood level = BeaconMood.Asleep;

        /// <summary>No rotation, no shake; the lamp pulses colour slowly instead. Set from the system setting or the grown-up's override.</summary>
        public bool ReduceMotion { get; set; }

        private void Advance ()
        {
            phase += 0.06;

            if (!ReduceMotion) {
                var degreesPerTick = level switch {
                    BeaconMood.Alarm => 12.0,   // about one turn per second at a 33 ms tick
                    BeaconMood.Warning => 3.0,  // one sweep roughly every 3 s
                    _ => 0.0,
                };
                if (degreesPerTick > 0) {
                    beamAngle = (beamAngle + degreesPerTick) % 360;
                    Invalidate ();
                }
            }

            // A blink every few seconds while watching; other moods hold their own fixed eye shape.
            if (level is BeaconMood.Watching or BeaconMood.Reassured) {
                blinkAt -= 0.033;
                if (blinkAt <= 0) {
                    blinkClosed = !blinkClosed;
                    blinkAt = blinkClosed ? 0.15 : 3 + phase % 4;
                    Invalidate ();
                }
            } else if (blinkClosed) {
                blinkClosed = false;
            }

            if (level is BeaconMood.Asleep or BeaconMood.AllClear)
                Invalidate (); // these moods breathe/pulse continuously via `phase`
        }

        /// <inheritdoc/>
        protected override void OnPaint (PaintEventArgs e)
        {
            base.OnPaint (e);

            var g = e.Graphics;

            var w = ClientSize.Width;
            var h = ClientSize.Height;
            var domeRadius = Math.Min (w, h) * 0.30f;
            var domeCenter = new PointF (w / 2f, h * 0.55f);
            var lampRadius = domeRadius * 0.34f;
            var lampCenter = new PointF (domeCenter.X, domeCenter.Y - domeRadius * 0.95f);

            DrawBase (g, domeCenter, domeRadius);
            DrawBeam (g, lampCenter, lampRadius);
            DrawDome (g, domeCenter, domeRadius);
            DrawLamp (g, lampCenter, lampRadius);
            DrawFace (g, domeCenter, domeRadius);
        }

        private void DrawBase (Graphics g, PointF domeCenter, float domeRadius)
        {
            var baseRect = new RectangleF (domeCenter.X - domeRadius * 0.7f, domeCenter.Y + domeRadius * 0.55f, domeRadius * 1.4f, domeRadius * 0.45f);
            using var brush = new SolidBrush (AlertPalette.GrapeInk);
            g.FillRoundedRectangle (brush, baseRect, domeRadius * 0.12f);
        }

        private void DrawBeam (Graphics g, PointF lampCenter, float lampRadius)
        {
            if (level is not (BeaconMood.Warning or BeaconMood.Alarm))
                return;

            var reach = lampRadius * 6f;
            var wedge = level == BeaconMood.Alarm ? 26f : 34f;
            var angle = ReduceMotion ? 0.0 : beamAngle;
            var color = AlertPalette.For (level);

            var state = g.Save ();
            g.TranslateTransform (lampCenter.X, lampCenter.Y);
            g.RotateTransform ((float)angle);

            using (var beamBrush = new SolidBrush (Color.FromArgb (70, color)))
                g.FillPie (beamBrush, -reach, -reach, reach * 2, reach * 2, -wedge / 2, wedge);

            using (var beamBrush = new SolidBrush (Color.FromArgb (70, color)))
                g.FillPie (beamBrush, -reach, -reach, reach * 2, reach * 2, 180 - wedge / 2, wedge);

            g.Restore (state);
        }

        private void DrawDome (Graphics g, PointF center, float radius)
        {
            // Asleep breathes: a slow radius pulse. All clear gets one soft pulse when it lands (PLAN.md section 8.6); a continuous
            // gentle pulse stands in for that single pulse until the takeover-to-Home transition is wired to trigger it once.
            var breath = level == BeaconMood.Asleep ? (float)Math.Sin (phase) * radius * 0.02f
                : level == BeaconMood.AllClear ? (float)Math.Sin (phase * 1.5) * radius * 0.015f
                : 0f;
            var r = radius + breath;

            var rect = new RectangleF (center.X - r, center.Y - r, r * 2, r * 2);
            using (var brush = new SolidBrush (AlertPalette.Paper))
                g.FillEllipse (brush, rect);
            using (var pen = new Pen (AlertPalette.GrapeInk, 3f))
                g.DrawEllipse (pen, rect);
        }

        private void DrawLamp (Graphics g, PointF center, float radius)
        {
            var color = AlertPalette.For (level);

            // Alarm shapes always carry a Paper ring: Cherry must never sit directly on a dark ground (PLAN.md section 8.2).
            if (level == BeaconMood.Alarm) {
                var ring = new RectangleF (center.X - radius - 3, center.Y - radius - 3, (radius + 3) * 2, (radius + 3) * 2);
                using var ringBrush = new SolidBrush (AlertPalette.Paper);
                g.FillEllipse (ringBrush, ring);
            }

            var pulse = ReduceMotion && level != BeaconMood.Asleep ? 0.75f + (float)Math.Sin (phase) * 0.25f : 1f;
            var lit = Color.FromArgb ((int)(color.A * pulse), color.R, color.G, color.B);
            var rect = new RectangleF (center.X - radius, center.Y - radius, radius * 2, radius * 2);
            using (var brush = new SolidBrush (lit))
                g.FillEllipse (brush, rect);
            using (var pen = new Pen (AlertPalette.GrapeInk, 2.5f))
                g.DrawEllipse (pen, rect);
        }

        private void DrawFace (Graphics g, PointF center, float radius)
        {
            var eyeY = center.Y - radius * 0.12f;
            var eyeSpacing = radius * 0.42f;
            var eyeRadius = level == BeaconMood.Alarm ? radius * 0.16f : radius * 0.11f;
            var closed = blinkClosed || level == BeaconMood.Asleep;

            using var pen = new Pen (AlertPalette.GrapeInk, 3f);
            using var brush = new SolidBrush (AlertPalette.GrapeInk);

            foreach (var dx in new[] { -eyeSpacing, eyeSpacing }) {
                var eyeCenter = new PointF (center.X + dx, eyeY);
                if (closed)
                    g.DrawLine (pen, eyeCenter.X - eyeRadius, eyeCenter.Y, eyeCenter.X + eyeRadius, eyeCenter.Y);
                else
                    g.FillEllipse (brush, eyeCenter.X - eyeRadius, eyeCenter.Y - eyeRadius, eyeRadius * 2, eyeRadius * 2);
            }

            var mouthY = center.Y + radius * 0.28f;
            var mouthWidth = radius * 0.5f;

            switch (level) {
                case BeaconMood.Alarm:
                    // A small "o": tell a grown-up now.
                    var o = mouthWidth * 0.35f;
                    g.DrawEllipse (pen, center.X - o / 2, mouthY - o / 2, o, o);
                    break;
                case BeaconMood.AllClear:
                case BeaconMood.Watching:
                case BeaconMood.Reassured:
                    // A gentle smile: an arc along the bottom of an invisible circle.
                    var smileRect = new RectangleF (center.X - mouthWidth / 2, mouthY - mouthWidth / 2, mouthWidth, mouthWidth);
                    g.DrawArc (pen, smileRect, 20, 140);
                    break;
                case BeaconMood.Warning:
                    // A flatter, concerned line.
                    g.DrawLine (pen, center.X - mouthWidth / 2, mouthY, center.X + mouthWidth / 2, mouthY);
                    break;
                case BeaconMood.Asleep:
                default:
                    // Relaxed: a short, soft line.
                    g.DrawLine (pen, center.X - mouthWidth / 3, mouthY, center.X + mouthWidth / 3, mouthY);
                    break;
            }
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
