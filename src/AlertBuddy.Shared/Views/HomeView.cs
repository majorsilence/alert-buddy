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
        private readonly ChunkyButton bedsideButton;
        private readonly Label rightNow;

        /// <summary>Builds Home for <paramref name="vm"/>. Disposed with the view when the page is left.</summary>
        public HomeView (MainViewModel vm)
        {
            this.vm = vm ?? throw new ArgumentNullException (nameof (vm));
            Dock = DockStyle.Fill;
            BackColor = AlertPalette.Ground;

            gear = new HoldButton { Text = "⚙", Location = new Point (16, 16), Size = new Size (48, 48), HoldDuration = TimeSpan.FromSeconds (2) }
                .Named ("home.settings", "Settings, for grown-ups", AccessibleNames.HoldHint);
            gear.Held += (_, _) => vm.OpenSettingsCommand.Execute (null);
            Controls.Add (gear);

            beacon = new BeaconBuddy { Size = new Size (200, 200) }.Named ("home.buddy");
            Controls.Add (beacon);

            bubble = new SpeechBubble { Size = new Size (340, 120) }.Named ("home.status");
            Controls.Add (bubble);

            connectionLine = new Label { AutoSize = false, Height = 26, ForeColor = AlertPalette.OnGround };
            Controls.Add (connectionLine);

            bannerLine = new Label { AutoSize = false, Height = 52, ForeColor = AlertPalette.Notice, Visible = false };
            Controls.Add (bannerLine);

            alertList = new Panel { AutoScroll = true };
            Controls.Add (alertList);

            openBookButton = new ChunkyButton { Text = "Alert book" }.Named ("home.alertBook");
            openBookButton.Click += (_, _) => vm.OpenBookCommand.Execute (null);
            Controls.Add (openBookButton);

            practiceButton = new ChunkyButton { Text = "Practice" }.Named ("home.practice");
            practiceButton.Click += (_, _) => vm.StartPracticeCommand.Execute (null);
            Controls.Add (practiceButton);

            rightNow = new Label { AutoSize = true, Text = "Right now", ForeColor = AlertPalette.OnGround, Font = AlertFonts.Display (20), Visible = false };
            Controls.Add (rightNow);

            bedsideButton = new ChunkyButton { Size = new Size (144, 48), Visible = vm.CanBedside }.Named ("home.bedside");
            scope.Add (bedsideButton.BindCommand (vm.ToggleBedsideCommand));
            Controls.Add (bedsideButton);

            scope.Add (vm.Observe (nameof (MainViewModel.IsBedside), v => v.IsBedside, bedside => {
                // Bedside forces Night while it is on (PLAN.md section 8.7). The look is global, so every screen built from now on follows
                // it, and this one is restyled in place.
                AlertBuddyTheme.SetBedside (bedside);
                bedsideButton.Text = bedside ? "Day" : "Bedside";
                Restyle ();
            }));

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
                    card = new TicketCard ().Named ($"home.alert.{item.Id}");
                    card.AccessibleRole = AccessibleRole.PushButton;
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

        private void Restyle ()
        {
            BackColor = AlertPalette.Ground;
            foreach (var label in new[] { connectionLine, rightNow })
                label.ForeColor = AlertPalette.OnGround;
            bannerLine.ForeColor = AlertPalette.Notice;
            Invalidate ();
        }

        // Manual layout for the three widths of PLAN.md section 7.3. Width/Height are logical, like every bound set below.
        private void PerformCustomLayout ()
        {
            var mode = LayoutModes.For (Width, Height);
            var w = Width;
            rightNow.Visible = mode == LayoutMode.Expanded;

            // The gear stays top-left and bedside top-right, whatever the mode.
            bedsideButton.Location = new Point (w - bedsideButton.Width - 16, 16);

            // The left pane (or the whole window, when there is one column): where the buddy, the sentence and the buttons live.
            var paneLeft = 0;
            var paneWidth = w;
            if (mode == LayoutMode.Expanded)
                paneWidth = Math.Clamp (w * 2 / 5, 320, 520);
            else if (mode == LayoutMode.Medium) {
                paneWidth = Math.Min (w, LayoutModes.ColumnMax);
                paneLeft = (w - paneWidth) / 2;
            }

            var centerX = paneLeft + paneWidth / 2;
            beacon.Location = new Point (centerX - beacon.Width / 2, 56);
            bubble.Location = new Point (centerX - bubble.Width / 2, beacon.Bottom + 8);
            // Wrapped to the pane, not left to run off the edge of a phone: the honest banner is the one line that must be readable whole.
            connectionLine.Width = paneWidth - 48;
            bannerLine.Width = paneWidth - 48;
            // As tall as its text needs at this width: a fixed height cut the last line off on a phone ("...fix this in settings.").
            bannerLine.Height = FormColumn.ParagraphHeight (bannerLine.Text, bannerLine.Width);
            connectionLine.Location = new Point (paneLeft + 24, bubble.Bottom + 12);
            if (bannerLine.Visible)
                bannerLine.Location = new Point (paneLeft + 24, connectionLine.Bottom + 4);

            const int buttonsHeight = 88;
            var half = paneWidth / 2 - 32;
            openBookButton.Size = new Size (half, 64);
            openBookButton.Location = new Point (paneLeft + 16, Height - buttonsHeight);
            practiceButton.Size = new Size (half, 64);
            practiceButton.Location = new Point (paneLeft + paneWidth / 2 + 16, Height - buttonsHeight);

            if (mode == LayoutMode.Expanded) {
                // The list gets the whole right pane, top to bottom, under its own heading.
                var listLeft = paneWidth + 16;
                rightNow.Location = new Point (listLeft + 8, 24);
                alertList.Location = new Point (listLeft, rightNow.Bottom + 12);
                alertList.Size = new Size (Math.Max (0, w - listLeft - 16), Math.Max (0, Height - alertList.Top - 16));
            } else {
                var listTop = (bannerLine.Visible ? bannerLine.Bottom : connectionLine.Bottom) + 8;
                alertList.Location = new Point (paneLeft + 16, listTop);
                alertList.Size = new Size (paneWidth - 32, Math.Max (0, openBookButton.Top - 12 - listTop));
            }

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
