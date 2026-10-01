using System.Drawing;
using AlertBuddy.Shared.Controls;
using AlertBuddy.Shared.Theme;
using AlertBuddy.ViewModels.Screens;
using Majorsilence.Forms;
using Majorsilence.Forms.Mvvm;

namespace AlertBuddy.Shared.Views
{
    /// <summary>
    /// The alarm takeover (PLAN.md sections 4.3, 8.3 and 9): replaces Home while an alarm is unanswered. One big button for the child,
    /// a small hold button for a grown-up.
    /// </summary>
    public sealed class AlarmView : UserControl
    {
        private readonly AlarmViewModel vm;
        private readonly BindingScope scope = new ();

        private readonly BeaconBuddy beacon;
        private readonly Label heading;
        private readonly Label detail;
        private readonly ChunkyButton toldButton;
        private readonly Label thankYou;
        private readonly HoldButton gotItButton;

        /// <summary>Builds the takeover for <paramref name="vm"/>.</summary>
        public AlarmView (AlarmViewModel vm)
        {
            this.vm = vm ?? throw new ArgumentNullException (nameof (vm));
            Dock = DockStyle.Fill;
            // A neutral ground, not a Cherry wash: state colour is the beacon's job (PLAN.md section 8.2's "colour is never
            // decoration"), and a Cherry background would also swallow the beacon's own Cherry lamp and Paper ring whole.
            BackColor = AlertPalette.Ground;

            beacon = new BeaconBuddy { Size = new Size (200, 200), Level = BeaconMood.Alarm };
            Controls.Add (beacon);

            heading = new Label { AutoSize = true, Text = vm.Heading, ForeColor = AlertPalette.OnGround };
            Controls.Add (heading);

            detail = new Label { AutoSize = true, ForeColor = AlertPalette.OnGround };
            Controls.Add (detail);

            toldButton = new ChunkyButton { Text = vm.ToldButtonText, Height = 72, FillColor = AlertPalette.Cherry, OutlineColor = AlertPalette.GrapeInk, TextColor = AlertPalette.Paper };
            toldButton.Click += (_, _) => vm.ToldAGrownUpCommand.Execute (null);
            Controls.Add (toldButton);

            thankYou = new Label { AutoSize = true, ForeColor = AlertPalette.OnGround, Visible = false, Text = vm.ThankYouText };
            Controls.Add (thankYou);

            gotItButton = new HoldButton { Text = vm.GotItButtonText, Size = new Size (140, 48) };
            gotItButton.Held += (_, _) => vm.GotItCommand.Execute (null);
            Controls.Add (gotItButton);

            scope.Add (vm.Observe (nameof (AlarmViewModel.Detail), v => v.Detail, text => detail.Text = text));
            scope.Add (vm.Observe (nameof (AlarmViewModel.IsAcknowledged), v => v.IsAcknowledged, acknowledged => {
                beacon.Level = acknowledged ? BeaconMood.Reassured : BeaconMood.Alarm;
                toldButton.Visible = !acknowledged;
                thankYou.Visible = acknowledged;
                beacon.Invalidate ();
                PerformCustomLayout ();
            }));

            Resize += (_, _) => PerformCustomLayout ();
            PerformCustomLayout ();
        }

        private void PerformCustomLayout ()
        {
            var w = Width;
            var centerX = w / 2;

            beacon.Location = new Point (centerX - beacon.Width / 2, 32);
            heading.Location = new Point (centerX - heading.Width / 2, beacon.Bottom + 16);
            detail.Location = new Point (centerX - detail.Width / 2, heading.Bottom + 8);

            if (!vm.IsAcknowledged) {
                toldButton.Size = new Size (Math.Min (420, w - 48), 72);
                toldButton.Location = new Point (centerX - toldButton.Width / 2, detail.Bottom + 40);
            } else {
                thankYou.Location = new Point (centerX - thankYou.Width / 2, detail.Bottom + 40);
            }

            gotItButton.Location = new Point (centerX - gotItButton.Width / 2, Height - gotItButton.Height - 24);
        }

        /// <inheritdoc/>
        protected override void Dispose (bool disposing)
        {
            if (disposing)
                scope.Dispose ();

            base.Dispose (disposing);
        }
    }
}
