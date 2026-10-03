using AlertBuddy.Core.Ntfy;
using AlertBuddy.Shared.Controls;
using AlertBuddy.ViewModels.Screens;
using Majorsilence.Forms;
using Majorsilence.Forms.Headless;
using Xunit;

namespace AlertBuddy.Shared.Tests
{
    /// <summary>
    /// Every screen, walked as a tree (PLAN.md section 10, "Framework automation"): each control a person or a test can act on has a stable
    /// <c>Name</c>, something a screen reader or an automation client can call it, and a touch target of at least 48 (section 8.11). The
    /// views are custom-painted, so the words on screen are not what an accessibility bridge sees: the names have to be set.
    /// </summary>
    public class AccessibilityTests
    {
        internal static IEnumerable<Control> Descendants (IEnumerable<Control> children)
        {
            foreach (var child in children) {
                yield return child;
                foreach (var nested in Descendants (child.Controls.Cast<Control> ()))
                    yield return nested;
            }
        }

        private static bool IsInteractive (Control c) => c is ChunkyButton or HoldButton or Button or TextBox or CheckBox or ComboBox or NumericUpDown;

        private static string Label (Control c) => c.AccessibleName is { Length: > 0 } name ? name : (c is TextBox or ComboBox or NumericUpDown ? "" : c.Text ?? "");

        private static string Where (Control c) => $"{c.GetType ().Name} '{c.Name}' ('{c.Text}')";

        /// <summary>Renders the screen first, so layout has run and sizes are real.</summary>
        private static void AssertAccessible (MainForm form, string screen)
        {
            HeadlessRenderer.CapturePng (form, 360, 1200);   // a common phone width: the narrower the page, the sooner words run off it
            var controls = Descendants (form.Controls.Cast<Control> ()).Where (c => c.Visible).ToList ();

            var unnamed = controls.Where (c => (IsInteractive (c) || c is BeaconBuddy or TicketCard or SpeechBubble) && string.IsNullOrEmpty (c.Name)).Select (Where).ToList ();
            Assert.True (unnamed.Count == 0, $"{screen}: no Name on {string.Join ("; ", unnamed)}");

            var duplicates = controls.Where (c => !string.IsNullOrEmpty (c.Name)).GroupBy (c => c.Name).Where (g => g.Count () > 1).Select (g => g.Key).ToList ();
            Assert.True (duplicates.Count == 0, $"{screen}: Name used twice: {string.Join (", ", duplicates)}");

            var mute = controls.Where (c => (IsInteractive (c) || c is BeaconBuddy or TicketCard) && Label (c).Length == 0).Select (Where).ToList ();
            Assert.True (mute.Count == 0, $"{screen}: nothing a screen reader could say for {string.Join ("; ", mute)}");

            // Words must not run off the right edge of the screen: a label wider than the page is cut off (the Alert book's empty line was).
            var clipped = controls.Where (c => c is Label && c.Parent is { } parent && c.Left + c.Width > parent.Width + 1).Select (c => $"{Where (c)} ends at {c.Left + c.Width}, page is {c.Parent!.Width}").ToList ();
            Assert.True (clipped.Count == 0, $"{screen}: text runs off the edge: {string.Join ("; ", clipped)}");

            var small = controls.Where (c => IsInteractive (c) && (c.Width < 48 || c.Height < 48)).Select (c => $"{Where (c)} is {c.Width}x{c.Height}").ToList ();
            Assert.True (small.Count == 0, $"{screen}: touch targets under 48: {string.Join ("; ", small)}");
        }

        [Fact]
        public async Task EveryScreen_HasNamesAccessibleLabelsAndBigEnoughTargets ()
        {
            var app = SmokeTests.CreateApp ();
            try {
                var form = new MainForm (app);
                AssertAccessible (form, "Home, calm");

                app.Main.OpenBookCommand.Execute (null);
                AssertAccessible (form, "Alert book, empty");
                app.Navigator.GoBack ();

                app.Engine.Handle (new NtfyEvent (NtfyEventKind.Message, new NtfyMessage (
                    "w1", DateTimeOffset.UtcNow, "home-alerts", "Workshop: temperature warning", "Workshop is at 41.2 °C", 4, [])));
                AssertAccessible (form, "Home, with a warning");

                app.Main.OpenBookCommand.Execute (null);
                var book = Assert.IsType<AlertBookViewModel> (app.Navigator.Current);
                AssertAccessible (form, "Alert book");

                book.Groups[0].Items[0].OpenCommand.Execute (null);
                AssertAccessible (form, "Alert detail");

                app.Navigator.GoBack ();
                book.ClearHistoryCommand.Execute (null);
                AssertAccessible (form, "Grown-up gate");

                app.Navigator.GoBack ();
                app.Navigator.GoBack ();
                app.Main.StartPracticeCommand.Execute (null);
                AssertAccessible (form, "Practice");

                app.Navigator.GoBack ();
                app.Navigator.GoTo<SettingsViewModel> ();
                AssertAccessible (form, "Settings");

                app.Navigator.GoTo<FirstRunViewModel> ();
                var firstRun = Assert.IsType<FirstRunViewModel> (app.Navigator.Current);
                AssertAccessible (form, "First run 1");
                firstRun.BuddyName = "Pip";
                firstRun.NextCommand.Execute (null);
                AssertAccessible (form, "First run 2");
                firstRun.Pin = "1234";
                firstRun.PinConfirm = "1234";
                firstRun.NextCommand.Execute (null);
                AssertAccessible (form, "First run 3");
                firstRun.ServerUrl = "https://ntfy.example.com";
                firstRun.Topic = "home-alerts";
                firstRun.NextCommand.Execute (null);
                AssertAccessible (form, "First run 4");
                firstRun.NextCommand.Execute (null);
                AssertAccessible (form, "First run 5");
            } finally {
                await app.DisposeAsync ();
            }
        }

        [Fact]
        public async Task TheAlarmTakeover_IsNamedAndReachable ()
        {
            var app = SmokeTests.CreateApp ();
            try {
                var form = new MainForm (app);
                app.Engine.Handle (new NtfyEvent (NtfyEventKind.Message, new NtfyMessage (
                    "a1", DateTimeOffset.UtcNow, "home-alerts", "Workshop: temperature alarm", "Workshop is at 50.6 °C", 5, [])));

                Assert.IsType<AlarmViewModel> (app.Navigator.Current);
                AssertAccessible (form, "Alarm takeover");
            } finally {
                await app.DisposeAsync ();
            }
        }

        [Fact]
        public async Task OnATablet_FormFieldsStayReadableWidth_AndCentred ()
        {
            // On a 2560-wide tablet First run and Settings stretched every field across the whole screen.
            var app = SmokeTests.CreateApp ();
            try {
                var form = new MainForm (app);
                app.Navigator.GoTo<FirstRunViewModel> ();
                File.WriteAllBytes (Path.Combine (TestEnvironment.RenderDirectory, "tablet-firstrun.png"), HeadlessRenderer.CapturePng (form, 1280, 800));

                var fields = Descendants (form.Controls.Cast<Control> ()).Where (c => c.Visible && c is TextBox or ComboBox).ToList ();
                Assert.NotEmpty (fields);
                Assert.All (fields, f => Assert.True (f.Width <= 640, $"{Where (f)} is {f.Width} wide on a 1280 page"));

                // centred: the space to its left and right is about equal
                var parent = fields[0].Parent!;
                Assert.InRange (fields[0].Left - (parent.Width - fields[0].Left - fields[0].Width), -24, 24);
            } finally {
                await app.DisposeAsync ();
            }
        }

        [Theory]
        [InlineData (360, 640)]     // a short phone
        [InlineData (420, 720)]
        public async Task FirstRun_ReasonLine_NeverCoversTheForm (int width, int height)
        {
            // On the emulator the red line saying what is wrong sat over the bottom of the form: the form's scrolling area ran to just above
            // the footer, and the line was drawn in the same strip. The form now ends where the line starts.
            var app = SmokeTests.CreateApp ();
            try {
                var form = new MainForm (app);
                app.Navigator.GoTo<FirstRunViewModel> ();
                var firstRun = Assert.IsType<FirstRunViewModel> (app.Navigator.Current);
                firstRun.BuddyName = "Pip";
                firstRun.NextCommand.Execute (null);
                firstRun.Pin = "1234";
                firstRun.PinConfirm = "1234";
                firstRun.NextCommand.Execute (null);
                Assert.Equal (FirstRunStep.Server, firstRun.Step);
                firstRun.ServerUrl = "https://ntfy.example.com";      // the topic is still empty, so there is a reason to show

                HeadlessRenderer.CapturePng (form, width, height);
                var all = Descendants (form.Controls.Cast<Control> ()).ToList ();
                var reason = all.First (c => c.Name == "firstRun.reason");
                var body = all.First (c => c.Name == "firstRun.body");

                Assert.NotEmpty (reason.Text);
                Assert.True (reason.Top >= body.Top + body.Height, $"the reason line starts at {reason.Top} but the form runs to {body.Top + body.Height}");
                Assert.True (reason.Top + reason.Height <= form.ClientSize.Height, "the reason line runs off the bottom of the page");
            } finally {
                await app.DisposeAsync ();
            }
        }
    }
}
