using System.Collections.Specialized;
using System.Drawing;
using AlertBuddy.Shared.Controls;
using AlertBuddy.Shared.Theme;
using AlertBuddy.ViewModels.Screens;
using Majorsilence.Forms;
using Majorsilence.Forms.Mvvm;

namespace AlertBuddy.Shared.Views
{
    /// <summary>
    /// Home (PLAN.md sections 8.3 and 9): the beacon, one speech bubble, the connection line, the open alerts, two big buttons. Compact,
    /// single-column layout only for now; the medium/expanded two-pane layout of section 7.3 is not built yet.
    /// </summary>
    public sealed class HomeView : UserControl
    {
        private readonly MainViewModel vm;
        private readonly BindingScope scope = new ();
        private readonly Dictionary<AlertItemViewModel, TicketCard> cards = [];

        private readonly HoldButton gear;
        private readonly BeaconBuddy beacon;
        private readonly SpeechBubble bubble;
        private readonly Label connectionLine;
        private readonly Label bannerLine;
        private readonly Panel alertList;
        private readonly ChunkyButton openBookButton;
        private readonly ChunkyButton practiceButton;

        /// <summary>Builds Home for <paramref name="vm"/>. Disposed with the view when the page is left.</summary>
        public HomeView (MainViewModel vm)
        {
            this.vm = vm ?? throw new ArgumentNullException (nameof (vm));
            Dock = DockStyle.Fill;
            BackColor = AlertPalette.Paper;

            gear = new HoldButton { Text = "⚙", Location = new Point (16, 16), Size = new Size (48, 48), HoldDuration = TimeSpan.FromSeconds (2) };
            gear.Held += (_, _) => vm.OpenSettingsCommand.Execute (null);
            Controls.Add (gear);

            beacon = new BeaconBuddy { Size = new Size (200, 200) };
            Controls.Add (beacon);

            bubble = new SpeechBubble { Size = new Size (340, 120) };
            Controls.Add (bubble);

            connectionLine = new Label { AutoSize = true, ForeColor = AlertPalette.GrapeInk };
            Controls.Add (connectionLine);

            bannerLine = new Label { AutoSize = true, ForeColor = AlertPalette.Cherry, Visible = false };
            Controls.Add (bannerLine);

            alertList = new Panel { AutoScroll = true };
            Controls.Add (alertList);

            openBookButton = new ChunkyButton { Text = "Alert book" };
            openBookButton.Click += (_, _) => vm.OpenBookCommand.Execute (null);
            Controls.Add (openBookButton);

            practiceButton = new ChunkyButton { Text = "Practice" };
            practiceButton.Click += (_, _) => vm.StartPracticeCommand.Execute (null);
            Controls.Add (practiceButton);

            scope.Add (vm.Observe (nameof (MainViewModel.Mood), v => v.Mood, mood => { beacon.Level = mood; beacon.Invalidate (); }));
            scope.Add (vm.Observe (nameof (MainViewModel.StatusText), v => v.StatusText, text => bubble.Text = text));
            scope.Add (vm.Observe (nameof (MainViewModel.ConnectionText), v => v.ConnectionText, text => connectionLine.Text = text));
            scope.Add (vm.Observe (nameof (MainViewModel.Banner), v => v.Banner, banner => {
                bannerLine.Text = banner ?? "";
                bannerLine.Visible = banner is not null;
                PerformCustomLayout ();
            }));

            vm.ActiveAlerts.CollectionChanged += OnAlertsChanged;
            SyncCards ();

            Resize += (_, _) => PerformCustomLayout ();
            PerformCustomLayout ();
        }

        private void OnAlertsChanged (object? sender, NotifyCollectionChangedEventArgs e) => SyncCards ();

        private void SyncCards ()
        {
            foreach (var stale in cards.Keys.Except (vm.ActiveAlerts).ToList ()) {
                alertList.Controls.Remove (cards[stale]);
                cards[stale].Dispose ();
                cards.Remove (stale);
            }

            foreach (var item in vm.ActiveAlerts) {
                if (!cards.TryGetValue (item, out var card)) {
                    card = new TicketCard ();
                    card.Click += (_, _) => item.OpenCommand.Execute (null);
                    cards[item] = card;
                    alertList.Controls.Add (card);
                }

                card.Source = item.Source;
                card.TimeAgo = item.TimeAgo;
                card.Sentence = item.Sentence;
                card.Temperature = item.Temperature;
                card.Level = item.Level;
                card.Status = item.Status;
            }

            LayoutCards ();
        }

        private void LayoutCards ()
        {
            var top = 0;
            foreach (var item in vm.ActiveAlerts) {
                if (!cards.TryGetValue (item, out var card))
                    continue;

                card.Location = new Point (0, top);
                card.Width = Math.Max (200, alertList.Width - 4);
                top += card.Height + 12;
            }
        }

        // Manual, compact-only layout (PLAN.md section 7.3's adaptive widths are not built yet).
        private void PerformCustomLayout ()
        {
            var w = Width;
            var centerX = w / 2;

            beacon.Location = new Point (centerX - beacon.Width / 2, 56);
            bubble.Location = new Point (centerX - bubble.Width / 2, beacon.Bottom + 8);

            connectionLine.Location = new Point (24, bubble.Bottom + 12);

            var listTop = connectionLine.Bottom + (bannerLine.Visible ? 32 : 8);
            if (bannerLine.Visible)
                bannerLine.Location = new Point (24, connectionLine.Bottom + 4);

            const int buttonsHeight = 88;
            openBookButton.Size = new Size (w / 2 - 32, 64);
            openBookButton.Location = new Point (16, Height - buttonsHeight);
            practiceButton.Size = new Size (w / 2 - 32, 64);
            practiceButton.Location = new Point (w / 2 + 16, Height - buttonsHeight);

            alertList.Location = new Point (16, listTop);
            alertList.Size = new Size (w - 32, Math.Max (0, openBookButton.Top - 12 - listTop));

            LayoutCards ();
        }

        /// <inheritdoc/>
        protected override void Dispose (bool disposing)
        {
            if (disposing) {
                vm.ActiveAlerts.CollectionChanged -= OnAlertsChanged;
                scope.Dispose ();
            }

            base.Dispose (disposing);
        }
    }
}
