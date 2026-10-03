using System.Drawing;
using AlertBuddy.Shared.Controls;
using AlertBuddy.Shared.Theme;
using AlertBuddy.ViewModels.Screens;
using Majorsilence.Forms;
using Majorsilence.Forms.Mvvm;

namespace AlertBuddy.Shared.Views
{
    /// <summary>Practice (PLAN.md sections 4.4 and 9): a banner that says it is pretend, the buddy, the numbered step, and the child's real button.</summary>
    public sealed class PracticeView : UserControl
    {
        private readonly PracticeViewModel vm;
        private readonly BindingScope scope = new ();
        private readonly Label banner;
        private readonly ChunkyButton backButton;
        private readonly BeaconBuddy beacon;
        private readonly SpeechBubble bubble;
        private readonly Label stepLine;
        private readonly ChunkyButton startButton;
        private readonly ChunkyButton toldButton;

        /// <summary>Builds Practice for <paramref name="vm"/>.</summary>
        public PracticeView (PracticeViewModel vm)
        {
            this.vm = vm ?? throw new ArgumentNullException (nameof (vm));
            Dock = DockStyle.Fill;
            BackColor = AlertPalette.Ground;

            banner = new Label { AutoSize = true, Text = vm.Banner, ForeColor = AlertPalette.Notice };
            Controls.Add (banner);

            backButton = new ChunkyButton { Text = "Back", Size = new Size (120, 56), Location = new Point (16, 16) }.Named ("practice.back");
            scope.Add (backButton.BindCommand (vm.BackCommand));
            Controls.Add (backButton);

            beacon = new BeaconBuddy { Size = new Size (200, 200) }.Named ("practice.buddy");
            Controls.Add (beacon);

            bubble = new SpeechBubble { Size = new Size (340, 120) }.Named ("practice.status");
            Controls.Add (bubble);

            stepLine = new Label { AutoSize = true, ForeColor = AlertPalette.OnGround };
            Controls.Add (stepLine);

            startButton = new ChunkyButton { Height = 64 }.Named ("practice.start");
            startButton.Click += (_, _) => {
                if (vm.IsRunning)
                    vm.StopCommand.Execute (null);
                else
                    vm.StartCommand.Execute (null);
            };
            Controls.Add (startButton);

            toldButton = new ChunkyButton { Text = "I told a grown-up", Height = 64, FillColor = AlertPalette.Cherry, OutlineColor = AlertPalette.GrapeInk, TextColor = AlertPalette.Paper }.Named ("practice.told");
            scope.Add (toldButton.BindCommand (vm.ToldAGrownUpCommand));
            Controls.Add (toldButton);

            scope.Add (vm.Observe (nameof (PracticeViewModel.Mood), v => v.Mood, m => beacon.Level = m));
            scope.Add (vm.Observe (nameof (PracticeViewModel.StatusText), v => v.StatusText, t => bubble.Text = t));
            scope.Add (vm.Observe (nameof (PracticeViewModel.Step), v => v.Step, _ => UpdateStepLine ()));
            scope.Add (vm.Observe (nameof (PracticeViewModel.Caption), v => v.Caption, _ => UpdateStepLine ()));
            scope.Add (vm.Observe (nameof (PracticeViewModel.IsRunning), v => v.IsRunning, running => startButton.Text = running ? "Stop" : "Start practice"));

            Resize += (_, _) => PerformCustomLayout ();
            PerformCustomLayout ();
        }

        // Numbered because the steps are a sequence (PLAN.md section 9).
        private void UpdateStepLine ()
        {
            stepLine.Text = vm.Step > 0 ? $"Step {vm.Step} of 3. {vm.Caption}" : "";
            PerformCustomLayout ();
        }

        private void PerformCustomLayout ()
        {
            var w = Width;
            var cx = w / 2;
            banner.Location = new Point (w - banner.Width - 16, 32);
            beacon.Location = new Point (cx - beacon.Width / 2, 88);
            bubble.Location = new Point (cx - bubble.Width / 2, beacon.Bottom + 8);
            stepLine.Location = new Point (cx - stepLine.Width / 2, bubble.Bottom + 12);

            var bw = Math.Min (420, w - 48);
            toldButton.Size = new Size (bw, 64);
            startButton.Size = new Size (bw, 64);
            toldButton.Location = new Point (cx - bw / 2, Height - 88 - 80);
            startButton.Location = new Point (cx - bw / 2, Height - 88);
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
