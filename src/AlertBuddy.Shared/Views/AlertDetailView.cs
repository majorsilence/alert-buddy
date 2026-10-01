using System.Drawing;
using AlertBuddy.Shared.Controls;
using AlertBuddy.Shared.Theme;
using AlertBuddy.ViewModels.Screens;
using Majorsilence.Forms;
using Majorsilence.Forms.Mvvm;

namespace AlertBuddy.Shared.Views
{
    /// <summary>One alert in full (PLAN.md section 9): who, how serious, when, the message and one line of what to do.</summary>
    public sealed class AlertDetailView : UserControl
    {
        private readonly AlertDetailViewModel vm;
        private readonly BindingScope scope = new ();
        private readonly ChunkyButton backButton;
        private readonly TicketCard card;
        private readonly Label levelLine;
        private readonly Label timeLine;
        private readonly Label body;
        private readonly Label whatToDo;
        private readonly HoldButton gotIt;

        /// <summary>Builds the detail screen for <paramref name="vm"/>.</summary>
        public AlertDetailView (AlertDetailViewModel vm)
        {
            this.vm = vm ?? throw new ArgumentNullException (nameof (vm));
            Dock = DockStyle.Fill;
            BackColor = AlertPalette.Ground;

            backButton = new ChunkyButton { Text = "Back", Size = new Size (120, 56), Location = new Point (16, 16) };
            scope.Add (backButton.BindCommand (vm.BackCommand));
            Controls.Add (backButton);

            card = new TicketCard { Height = 110 };
            Controls.Add (card);

            levelLine = new Label { AutoSize = true, ForeColor = AlertPalette.OnGround };
            timeLine = new Label { AutoSize = true, ForeColor = AlertPalette.OnGround };
            body = new Label { AutoSize = true, ForeColor = AlertPalette.OnGround };
            whatToDo = new Label { AutoSize = true, ForeColor = AlertPalette.OnGround };
            Controls.Add (levelLine);
            Controls.Add (timeLine);
            Controls.Add (body);
            Controls.Add (whatToDo);

            gotIt = new HoldButton { Text = "Got it", Size = new Size (140, 48) };
            gotIt.Held += (_, _) => vm.GotItCommand.Execute (null);
            Controls.Add (gotIt);

            scope.Add (vm.Observe (nameof (AlertDetailViewModel.Source), v => v.Source, s => card.Source = s));
            scope.Add (vm.Observe (nameof (AlertDetailViewModel.Level), v => v.Level, l => card.Level = l));
            scope.Add (vm.Observe (nameof (AlertDetailViewModel.Status), v => v.Status, s => card.Status = s));
            scope.Add (vm.Observe (nameof (AlertDetailViewModel.Temperature), v => v.Temperature, t => card.Temperature = t));
            scope.Add (vm.Observe (nameof (AlertDetailViewModel.TimeAgo), v => v.TimeAgo, t => { card.TimeAgo = t; UpdateTimeLine (); }));
            scope.Add (vm.Observe (nameof (AlertDetailViewModel.ClockTime), v => v.ClockTime, _ => UpdateTimeLine ()));
            scope.Add (vm.Observe (nameof (AlertDetailViewModel.LevelWord), v => v.LevelWord, w => { levelLine.Text = w; card.Sentence = w; PerformCustomLayout (); }));
            scope.Add (vm.Observe (nameof (AlertDetailViewModel.Body), v => v.Body, b => { body.Text = b; PerformCustomLayout (); }));
            scope.Add (vm.Observe (nameof (AlertDetailViewModel.WhatToDo), v => v.WhatToDo, w => { whatToDo.Text = w; PerformCustomLayout (); }));
            scope.Add (vm.Observe (nameof (AlertDetailViewModel.CanBeHandled), v => v.CanBeHandled, can => gotIt.Visible = can));

            Resize += (_, _) => PerformCustomLayout ();
            PerformCustomLayout ();
        }

        private void UpdateTimeLine () => timeLine.Text = $"{vm.ClockTime}, {vm.TimeAgo}";

        private void PerformCustomLayout ()
        {
            var w = Width;
            card.Location = new Point (16, backButton.Bottom + 16);
            card.Width = Math.Max (200, w - 32);

            levelLine.Location = new Point (24, card.Bottom + 16);
            timeLine.Location = new Point (24, levelLine.Bottom + 4);
            body.Location = new Point (24, timeLine.Bottom + 16);
            whatToDo.Location = new Point (24, body.Bottom + 16);
            gotIt.Location = new Point (w / 2 - gotIt.Width / 2, Height - gotIt.Height - 24);
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
