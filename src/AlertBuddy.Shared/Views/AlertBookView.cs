using System.Collections.Specialized;
using System.Drawing;
using AlertBuddy.Shared.Controls;
using AlertBuddy.Shared.Theme;
using AlertBuddy.ViewModels.Screens;
using Majorsilence.Forms;
using Majorsilence.Forms.Mvvm;

namespace AlertBuddy.Shared.Views
{
    /// <summary>The Alert Book (PLAN.md section 9): tickets grouped by day, scroll only. Clearing is a hold button and goes through the gate.</summary>
    public sealed class AlertBookView : UserControl
    {
        private readonly AlertBookViewModel vm;
        private readonly BindingScope scope = new ();
        private readonly ChunkyButton backButton;
        private readonly Panel list;
        private readonly Label empty;
        private readonly ChunkyButton clear;

        /// <summary>Builds the Alert Book for <paramref name="vm"/>.</summary>
        public AlertBookView (AlertBookViewModel vm)
        {
            this.vm = vm ?? throw new ArgumentNullException (nameof (vm));
            Dock = DockStyle.Fill;
            BackColor = AlertPalette.Ground;

            backButton = new ChunkyButton { Text = "Back", Size = new Size (120, 56), Location = new Point (16, 16) }.Named ("book.back");
            scope.Add (backButton.BindCommand (vm.BackCommand));
            Controls.Add (backButton);

            list = new Panel { AutoScroll = true };
            Controls.Add (list);

            empty = new Label { AutoSize = false, Text = vm.EmptyText, ForeColor = AlertPalette.OnGround };
            Controls.Add (empty);

            clear = new ChunkyButton { Text = "Clear", Size = new Size (120, 48) }.Named ("book.clear", "Clear the Alert book");
            clear.Click += (_, _) => vm.ClearHistoryCommand.Execute (null);
            Controls.Add (clear);

            vm.Groups.CollectionChanged += OnGroupsChanged;
            scope.Add (vm.Observe (nameof (AlertBookViewModel.IsEmpty), v => v.IsEmpty, isEmpty => {
                empty.Visible = isEmpty;
                list.Visible = !isEmpty;
                clear.Visible = !isEmpty;
            }));

            Rebuild ();
            Resize += (_, _) => PerformCustomLayout ();
            PerformCustomLayout ();
        }

        private void OnGroupsChanged (object? sender, NotifyCollectionChangedEventArgs e) => Rebuild ();

        private void Rebuild ()
        {
            foreach (var old in list.Controls.ToList ()) {
                list.Controls.Remove (old);
                old.Dispose ();
            }

            foreach (var group in vm.Groups) {
                list.Controls.Add (new Label { AutoSize = true, Text = group.Heading, ForeColor = AlertPalette.OnGround, Tag = "heading" });
                foreach (var item in group.Items) {
                    var card = new TicketCard {
                        Source = item.Source, TimeAgo = item.TimeAgo, Sentence = item.Sentence,
                        Temperature = item.Temperature, Level = item.Level, Status = item.Status,
                    };
                    card.Named ($"book.alert.{item.Id}");
                    card.AccessibleRole = AccessibleRole.PushButton;
                    card.Click += (_, _) => item.OpenCommand.Execute (null);
                    list.Controls.Add (card);
                }
            }

            LayoutList ();
        }

        private void LayoutList ()
        {
            var top = 0;
            foreach (var child in list.Controls) {
                child.Location = new Point (0, top);
                if (child is TicketCard)
                    child.Width = Math.Max (200, list.Width - 4);
                top += child.Height + (child is TicketCard ? 12 : 4);
            }
        }

        private void PerformCustomLayout ()
        {
            var w = Width;
            clear.Location = new Point (w - clear.Width - 16, 20);
            // Wrapped to the page: on one line this invitation ran off the right edge of a phone.
            empty.Width = Math.Max (160, w - 48);
            empty.Height = FormColumn.ParagraphHeight (empty.Text, empty.Width);
            empty.Location = new Point (24, backButton.Bottom + 32);
            list.Location = new Point (16, backButton.Bottom + 16);
            list.Size = new Size (w - 32, Math.Max (0, Height - list.Top - 16));
            LayoutList ();
        }

        /// <inheritdoc/>
        protected override void Dispose (bool disposing)
        {
            if (disposing) {
                vm.Groups.CollectionChanged -= OnGroupsChanged;
                scope.Dispose ();
            }

            base.Dispose (disposing);
        }
    }
}
