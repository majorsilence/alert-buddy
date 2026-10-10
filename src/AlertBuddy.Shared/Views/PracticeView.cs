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
        private readonly Label soundsHeading;
        private readonly Label soundNote;
        private readonly List<ChunkyButton> soundButtons = [];

        // The label on the button, and the full name for a screen reader. Short, because three sit side by side on a phone.
        private static readonly (PracticeSound Which, string Label, string Name)[] Sounds = [
            (PracticeSound.Gentle, "Gentle", "Hear the gentle sound"),
            (PracticeSound.Whoop, "Whoop", "Hear the whoop"),
            (PracticeSound.Code3, "Code 3", "Hear code 3"),
            (PracticeSound.MarchTime, "March", "Hear march time"),
            (PracticeSound.Continuous, "Steady", "Hear the continuous sound"),
            (PracticeSound.VoiceEvacuation, "Voice", "Hear the voice evacuation chime"),
        ];

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

            soundsHeading = new Label { AutoSize = true, Text = "Hear the sounds", ForeColor = AlertPalette.OnGround, Font = AlertFonts.Display (18) };
            Controls.Add (soundsHeading);
            foreach (var (which, label, name) in Sounds) {
                var button = new ChunkyButton { Text = label, Height = 48 }.Named ($"practice.sound.{which}", name);
                var chosen = which;
                button.Click += (_, _) => vm.HearSoundCommand.Execute (chosen);
                soundButtons.Add (button);
                Controls.Add (button);
            }

            soundNote = new Label { AutoSize = true, ForeColor = AlertPalette.Notice };
            Controls.Add (soundNote);
            scope.Add (vm.Observe (nameof (PracticeViewModel.SoundNote), v => v.SoundNote, note => { soundNote.Text = note; PerformCustomLayout (); }));

            scope.Add (vm.Observe (nameof (PracticeViewModel.Mood), v => v.Mood, m => beacon.Level = m));
            scope.Add (vm.Observe (nameof (PracticeViewModel.StatusText), v => v.StatusText, t => bubble.Text = t));
            scope.Add (vm.Observe (nameof (PracticeViewModel.Step), v => v.Step, _ => UpdateStepLine ()));
            scope.Add (vm.Observe (nameof (PracticeViewModel.Caption), v => v.Caption, _ => UpdateStepLine ()));
            scope.Add (vm.Observe (nameof (PracticeViewModel.IsRunning), v => v.IsRunning, running => {
                startButton.Text = running ? "Stop" : "Start practice";
                PerformCustomLayout ();
            }));

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

            var bw = Math.Min (420, w - 48);
            toldButton.Size = new Size (bw, 64);
            startButton.Size = new Size (bw, 64);
            toldButton.Location = new Point (cx - bw / 2, Height - 88 - 80);
            startButton.Location = new Point (cx - bw / 2, Height - 88);

            // The sounds sit above the child's button, in two rows of three. They go while the pretend alert runs (it plays its own). On a
            // short screen the buddy and its bubble shrink to make room, and when even that is not enough the sounds are left out.
            const int rowHeight = 48, gap = 8, headingHeight = 30, noteHeight = 24;
            var sectionHeight = headingHeight + rowHeight * 2 + gap + noteHeight;
            var sectionTop = toldButton.Top - 16 - sectionHeight;
            var wanted = !vm.IsRunning;

            PlaceBuddy (compact: false, cx);
            var show = wanted && sectionTop >= stepLine.Bottom + 8;
            if (wanted && !show) {
                PlaceBuddy (compact: true, cx);
                show = sectionTop >= stepLine.Bottom + 8;
                if (!show)
                    PlaceBuddy (compact: false, cx);
            }

            soundsHeading.Visible = show;
            soundNote.Visible = show;
            foreach (var button in soundButtons)
                button.Visible = show;
            if (!show)
                return;

            soundsHeading.Location = new Point (cx - bw / 2, sectionTop);
            var cell = (bw - gap * 2) / 3;
            for (var i = 0; i < soundButtons.Count; i++) {
                soundButtons[i].Size = new Size (cell, rowHeight);
                soundButtons[i].Location = new Point (cx - bw / 2 + (i % 3) * (cell + gap), sectionTop + headingHeight + (i / 3) * (rowHeight + gap));
            }

            soundNote.Location = new Point (cx - bw / 2, sectionTop + headingHeight + rowHeight * 2 + gap + 4);
        }

        private void PlaceBuddy (bool compact, int cx)
        {
            beacon.Size = compact ? new Size (100, 100) : new Size (200, 200);
            bubble.Size = compact ? new Size (340, 96) : new Size (340, 120);
            beacon.Location = new Point (cx - beacon.Width / 2, 88);
            bubble.Location = new Point (cx - bubble.Width / 2, beacon.Bottom + 8);
            stepLine.Location = new Point (cx - stepLine.Width / 2, bubble.Bottom + 12);
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
