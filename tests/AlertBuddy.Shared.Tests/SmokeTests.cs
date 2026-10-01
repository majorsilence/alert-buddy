using AlertBuddy.Core.Alerts;
using AlertBuddy.Core.Ntfy;
using AlertBuddy.Core.Settings;
using AlertBuddy.Shared.Views;
using AlertBuddy.TestSupport;
using AlertBuddy.ViewModels;
using AlertBuddy.ViewModels.Services;
using Majorsilence.Forms.Headless;
using SkiaSharp;
using Xunit;

namespace AlertBuddy.Shared.Tests
{
    /// <summary>Runs work at once, on the caller's thread, as <c>AlertBuddy.ViewModels.Tests</c>'s own fake does for the same reason.</summary>
    internal sealed class SynchronousDispatcher : IUiDispatcher
    {
        public void Post (Action action) => action ();
    }

    internal sealed class NoOpSound : ISoundPlayer
    {
        public bool IsSupported => false;
        public void Play (Cue cue) { }
        public void StartLoop (Cue cue) { }
        public void StopLoop () { }
    }

    internal sealed class NoOpHaptics : IHaptics
    {
        public bool IsSupported => false;
        public void Tap () { }
        public void Alarm () { }
        public void Stop () { }
    }

    internal sealed class NoOpNotifier : IAlertNotifier
    {
        public void Show (Alert alert) { }
        public void Clear (string alertId) { }
    }

    internal sealed class NoOpKeepAwake : IKeepAwake
    {
        public bool Enabled { get; set; }
    }

    internal sealed class AlwaysListening : IBackgroundListener
    {
        public bool CanListenInBackground => true;
        public string? WhyNot => null;
        public void Start () { }
        public void Stop () { }
    }

    /// <summary>
    /// Real screens rendered on the framework's Headless backend, with no display, driven through the real composition root and fakes
    /// for everything that touches the platform (PLAN.md section 10). Assertions stay loose -- proving the harness renders something,
    /// not "something was drawn" -- until milestone 2's <c>DesignGallery</c> and golden images arrive.
    /// </summary>
    public class SmokeTests
    {
        private static AlertBuddyApp CreateApp () => AlertBuddyApp.Create (new PlatformServices {
            Sound = new NoOpSound (),
            Haptics = new NoOpHaptics (),
            Notifier = new NoOpNotifier (),
            Background = new AlwaysListening (),
            KeepAwake = new NoOpKeepAwake (),
            Dispatcher = new SynchronousDispatcher (),
            Secrets = new InMemorySecretStore (),
            SettingsStore = new InMemorySettingsStore {
                Current = new AppSettings { ServerUrl = "https://ntfy.example.com", Topic = "home-alerts", FirstRunComplete = true },
            },
            AlertState = new InMemoryAlertStateStore (),
            Version = "test",
        });

        private static void AssertInked (byte[] png, string fileName)
        {
            File.WriteAllBytes (Path.Combine (TestEnvironment.RenderDirectory, fileName), png);

            using var bitmap = SKBitmap.Decode (png);
            var background = bitmap.GetPixel (bitmap.Width - 2, bitmap.Height - 2);
            var inked = false;
            for (var y = 0; y < bitmap.Height && !inked; y++)
                for (var x = 0; x < bitmap.Width; x++)
                    if (bitmap.GetPixel (x, y) != background) { inked = true; break; }

            Assert.True (inked, $"{fileName} was a single flat colour");
        }

        [Fact]
        public async Task MainForm_RendersHomeHeadlessly_AndTheBeaconIsInk ()
        {
            var app = CreateApp ();
            try {
                var form = new MainForm (app);
                AssertInked (HeadlessRenderer.CapturePng (form, 420, 720), "smoke-home-calm.png");
            } finally {
                await app.DisposeAsync ();
            }
        }

        [Fact]
        public async Task MainForm_RendersAWarningTicket_Headlessly ()
        {
            var app = CreateApp ();
            try {
                var form = new MainForm (app);

                app.Engine.Handle (new NtfyEvent (NtfyEventKind.Message, new NtfyMessage (
                    "w1", DateTimeOffset.UtcNow, "home-alerts", "Workshop: temperature warning", "Workshop is at 41.2 °C", 4, [])));

                AssertInked (HeadlessRenderer.CapturePng (form, 420, 720), "smoke-home-warning.png");
            } finally {
                await app.DisposeAsync ();
            }
        }

        [Fact]
        public async Task MainForm_RendersTheAlarmTakeover_Headlessly ()
        {
            var app = CreateApp ();
            try {
                var form = new MainForm (app);

                app.Engine.Handle (new NtfyEvent (NtfyEventKind.Message, new NtfyMessage (
                    "a1", DateTimeOffset.UtcNow, "home-alerts", "Workshop: temperature alarm", "Workshop is at 50.6 °C", 5, [])));

                Assert.IsType<AlertBuddy.ViewModels.Screens.AlarmViewModel> (app.Navigator.Current);
                AssertInked (HeadlessRenderer.CapturePng (form, 420, 720), "smoke-alarm-takeover.png");
            } finally {
                await app.DisposeAsync ();
            }
        }

        [Fact]
        public async Task MainForm_RendersTheBookDetailPracticeAndGate_Headlessly ()
        {
            var app = CreateApp ();
            try {
                var form = new MainForm (app);
                app.Engine.Handle (new NtfyEvent (NtfyEventKind.Message, new NtfyMessage (
                    "w1", DateTimeOffset.UtcNow, "home-alerts", "Workshop: temperature warning", "Workshop is at 41.2 °C", 4, [])));

                app.Main.OpenBookCommand.Execute (null);
                var book = Assert.IsType<AlertBuddy.ViewModels.Screens.AlertBookViewModel> (app.Navigator.Current);
                AssertInked (HeadlessRenderer.CapturePng (form, 420, 720), "smoke-book.png");

                book.Groups[0].Items[0].OpenCommand.Execute (null);
                Assert.IsType<AlertBuddy.ViewModels.Screens.AlertDetailViewModel> (app.Navigator.Current);
                AssertInked (HeadlessRenderer.CapturePng (form, 420, 720), "smoke-detail.png");

                app.Navigator.GoBack ();
                book.ClearHistoryCommand.Execute (null);
                Assert.IsType<AlertBuddy.ViewModels.Screens.GateViewModel> (app.Navigator.Current);
                AssertInked (HeadlessRenderer.CapturePng (form, 420, 720), "smoke-gate.png");

                app.Navigator.GoBack ();
                app.Navigator.GoBack ();
                app.Main.StartPracticeCommand.Execute (null);
                Assert.IsType<AlertBuddy.ViewModels.Screens.PracticeViewModel> (app.Navigator.Current);
                AssertInked (HeadlessRenderer.CapturePng (form, 420, 720), "smoke-practice.png");
            } finally {
                await app.DisposeAsync ();
            }
        }

        [Fact]
        public async Task MainForm_RendersSettingsAndFirstRun_Headlessly ()
        {
            var app = CreateApp ();
            try {
                var form = new MainForm (app);
                app.Navigator.GoTo<AlertBuddy.ViewModels.Screens.SettingsViewModel> ();
                AssertInked (HeadlessRenderer.CapturePng (form, 420, 720), "smoke-settings.png");

                app.Navigator.GoTo<AlertBuddy.ViewModels.Screens.FirstRunViewModel> ();
                var firstRun = Assert.IsType<AlertBuddy.ViewModels.Screens.FirstRunViewModel> (app.Navigator.Current);
                AssertInked (HeadlessRenderer.CapturePng (form, 420, 720), "smoke-firstrun-1.png");

                firstRun.BuddyName = "Pip";
                firstRun.NextCommand.Execute (null);
                firstRun.Pin = "1234";
                firstRun.PinConfirm = "1234";
                firstRun.NextCommand.Execute (null);
                Assert.Equal (AlertBuddy.ViewModels.Screens.FirstRunStep.Server, firstRun.Step);
                AssertInked (HeadlessRenderer.CapturePng (form, 420, 720), "smoke-firstrun-3.png");
            } finally {
                await app.DisposeAsync ();
            }
        }

        [Theory]
        [InlineData (420, 720, LayoutMode.Compact)]
        [InlineData (599, 400, LayoutMode.Compact)]
        [InlineData (500, 300, LayoutMode.Compact)]
        [InlineData (700, 900, LayoutMode.Medium)]
        [InlineData (800, 360, LayoutMode.Expanded)]
        [InlineData (1000, 1200, LayoutMode.Expanded)]
        public void LayoutModes_FollowTheWidthsInThePlan (int width, int height, LayoutMode expected)
            => Assert.Equal (expected, LayoutModes.For (width, height));

        [Fact]
        public async Task Home_RendersAtEveryWidth_AndInBedsideMode ()
        {
            var app = CreateApp ();
            try {
                var form = new MainForm (app);
                app.Engine.SetConnection (new ConnectionInfo (ConnectionState.Live, LastHeard: DateTimeOffset.UtcNow));
                app.Engine.Handle (new NtfyEvent (NtfyEventKind.Message, new NtfyMessage (
                    "w1", DateTimeOffset.UtcNow, "home-alerts", "Workshop: temperature warning", "Workshop is at 41.2 °C", 4, [])));
                app.Engine.Handle (new NtfyEvent (NtfyEventKind.Message, new NtfyMessage (
                    "w2", DateTimeOffset.UtcNow, "home-alerts", "Music studio: temperature warning", "Music studio is at 39 °C", 4, [])));

                AssertInked (HeadlessRenderer.CapturePng (form, 700, 900), "smoke-home-medium.png");
                AssertInked (HeadlessRenderer.CapturePng (form, 1000, 640), "smoke-home-expanded.png");

                app.Main.ToggleBedsideCommand.Execute (null);
                Assert.True (app.Main.IsBedside);
                AssertInked (HeadlessRenderer.CapturePng (form, 1000, 640), "smoke-home-bedside.png");
                app.Main.ToggleBedsideCommand.Execute (null);
            } finally {
                AlertBuddy.Shared.Theme.AlertBuddyTheme.SetBedside (false);
                await app.DisposeAsync ();
            }
        }

        /// <summary>
        /// Since framework 26.5.0 <c>OnPaint</c> draws in logical units. A control that still scaled itself would paint at twice its size
        /// on a 2x display, so it would be cropped and lose its right edge; this fails at <c>MF_HEADLESS_SCALE=2</c> if one does.
        /// </summary>
        [Fact]
        public void CustomControls_StayInsideTheirOwnBounds_AtAnyScale ()
        {
            var form = new Majorsilence.Forms.Form { ClientSize = new System.Drawing.Size (420, 300), BackColor = System.Drawing.Color.White, FormBorderStyle = Majorsilence.Forms.FormBorderStyle.None };
            var button = new Controls.ChunkyButton { Text = "Alert book", Location = new System.Drawing.Point (40, 40), Size = new System.Drawing.Size (200, 64) };
            var card = new Controls.TicketCard { Location = new System.Drawing.Point (40, 140), Size = new System.Drawing.Size (300, 110), Source = "Workshop", Sentence = "Keep an eye on it." };
            form.Controls.Add (button);
            form.Controls.Add (card);

            var png = HeadlessRenderer.CapturePng (form, 420, 300);
            using var bitmap = SKBitmap.Decode (png);
            var scale = bitmap.Width / 420f;
            const int chrome = 0;

            // A control painted at twice its size is clipped to its own bounds, so it loses its right edge rather than overflowing: the
            // dark outline and shadow must still be there, just inside the right edge, at mid-height.
            bool DarkJustInsideRight (Majorsilence.Forms.Control c)
            {
                var x = (int)((c.Right - 2) * scale);
                var y = (int)((c.Top + c.Height / 2 + chrome) * scale);
                var pixel = bitmap.GetPixel (x, y);
                return pixel.Red < 100 && pixel.Green < 100 && pixel.Blue < 120;
            }

            Assert.True (DarkJustInsideRight (button), "the button lost its right edge: it was painted larger than its bounds");
            Assert.True (DarkJustInsideRight (card), "the ticket lost its right edge: it was painted larger than its bounds");
        }
    }
}
