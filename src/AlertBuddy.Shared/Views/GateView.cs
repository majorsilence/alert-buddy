using System.Drawing;
using AlertBuddy.Shared.Controls;
using AlertBuddy.Shared.Theme;
using AlertBuddy.ViewModels.Screens;
using Majorsilence.Forms;
using Majorsilence.Forms.Mvvm;

namespace AlertBuddy.Shared.Views
{
    /// <summary>The grown-up gate (PLAN.md section 4.3): hold, then a PIN pad. A gate against a curious child, not security.</summary>
    public sealed class GateView : UserControl
    {
        private readonly GateViewModel vm;
        private readonly BindingScope scope = new ();
        private readonly Label message;
        private readonly Label dots;
        private readonly HoldButton hold;
        private readonly ChunkyButton cancel;
        private readonly List<ChunkyButton> pad = [];
        private readonly ChunkyButton backspace;

        /// <summary>Builds the gate for <paramref name="vm"/>.</summary>
        public GateView (GateViewModel vm)
        {
            this.vm = vm ?? throw new ArgumentNullException (nameof (vm));
            Dock = DockStyle.Fill;
            BackColor = AlertPalette.Ground;

            message = new Label { AutoSize = true, ForeColor = AlertPalette.OnGround };
            dots = new Label { AutoSize = true, ForeColor = AlertPalette.OnGround };
            Controls.Add (message);
            Controls.Add (dots);

            hold = new HoldButton { Text = "Hold", Size = new Size (140, 72), HoldDuration = TimeSpan.FromSeconds (2) };
            hold.Held += (_, _) => vm.HoldCompletedCommand.Execute (null);
            Controls.Add (hold);

            for (var d = 0; d <= 9; d++) {
                var digit = d.ToString ();
                var key = new ChunkyButton { Text = digit, Size = new Size (72, 64) };
                key.Click += (_, _) => vm.PressDigitCommand.Execute (digit);
                pad.Add (key);
                Controls.Add (key);
            }

            backspace = new ChunkyButton { Text = "⌫", Size = new Size (72, 64) };
            scope.Add (backspace.BindCommand (vm.BackspaceCommand));
            Controls.Add (backspace);

            cancel = new ChunkyButton { Text = "Cancel", Size = new Size (140, 56) };
            scope.Add (cancel.BindCommand (vm.CancelCommand));
            Controls.Add (cancel);

            scope.Add (vm.Observe (nameof (GateViewModel.Message), v => v.Message, m => { message.Text = m; PerformCustomLayout (); }));
            scope.Add (vm.Observe (nameof (GateViewModel.EnteredCount), v => v.EnteredCount, n => { dots.Text = new string ('●', n) + new string ('○', 4 - n); PerformCustomLayout (); }));
            scope.Add (vm.Observe (nameof (GateViewModel.Phase), v => v.Phase, phase => {
                hold.Visible = phase == GatePhase.Hold;
                dots.Visible = phase == GatePhase.Pin;
                backspace.Visible = phase == GatePhase.Pin;
                foreach (var key in pad)
                    key.Visible = phase == GatePhase.Pin;
            }));

            Resize += (_, _) => PerformCustomLayout ();
            PerformCustomLayout ();
        }

        private void PerformCustomLayout ()
        {
            var cx = Width / 2;
            message.Location = new Point (cx - message.Width / 2, 40);
            dots.Location = new Point (cx - dots.Width / 2, message.Bottom + 16);
            hold.Location = new Point (cx - hold.Width / 2, message.Bottom + 48);

            // 1-9 in a 3x3 grid, then backspace, 0 and nothing under them.
            const int cell = 80;
            var left = cx - (cell * 3) / 2;
            var top = dots.Bottom + 24;
            for (var i = 1; i <= 9; i++)
                pad[i].Location = new Point (left + ((i - 1) % 3) * cell, top + ((i - 1) / 3) * cell);

            backspace.Location = new Point (left, top + 3 * cell);
            pad[0].Location = new Point (left + cell, top + 3 * cell);

            cancel.Location = new Point (cx - cancel.Width / 2, Height - cancel.Height - 24);
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
