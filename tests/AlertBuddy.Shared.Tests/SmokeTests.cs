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
        public void Play (Cue cue, double volume = 1) { }
        public void StartLoop (Cue cue, double volume = 1) { }
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

    internal sealed class SomePermissions : IPermissionGuide
    {
        public IReadOnlyList<PermissionItem> Items { get; } = [
            new (PermissionKind.Notifications, "Notifications", "So an alarm can show up on the screen.", true),
            new (PermissionKind.AlarmVolume, "Alarm volume", "Turn it up with the buttons while the test sound plays.", null),
            new (PermissionKind.BatteryOptimisation, "Keep listening with the screen off", "Some phones stop apps they think are idle.", false),
        ];

        public void Open (PermissionKind kind) { }
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
        // The heads apply the theme before they build a window; so do the renders, or they show the framework's default look.
        internal static AlertBuddyApp CreateApp (ISoundPlayer? sound = null, ISpeaker? speaker = null)
        {
            AlertBuddy.Shared.Theme.AlertBuddyTheme.Apply (AlertBuddy.Core.Settings.LookPreference.Day);
            return CreateAppCore (sound, speaker);
        }

        private static AlertBuddyApp CreateAppCore (ISoundPlayer? sound = null, ISpeaker? speaker = null) => AlertBuddyApp.Create (new PlatformServices {
            Sound = sound ?? new NoOpSound (),
            Speaker = speaker,
            Haptics = new NoOpHaptics (),
            Notifier = new NoOpNotifier (),
            Background = new AlwaysListening (),
            KeepAwake = new NoOpKeepAwake (),
            Permissions = new SomePermissions (),
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

        [Theory]
        [InlineData (false)]
        [InlineData (true)]
        public void TheBundledFonts_AreWhatIsDrawn_NotTheFallback (bool display)
        {
            // Two labels with the same words: one in a bundled family and one in a family that exists nowhere, which draws the fallback.
            // If the bundled font were not registered the two would be pixel-identical.
            byte[] Render (Func<Majorsilence.Forms.Drawing.Font> font)
            {
                var form = new Majorsilence.Forms.Form { ClientSize = new System.Drawing.Size (420, 80), BackColor = System.Drawing.Color.White, FormBorderStyle = Majorsilence.Forms.FormBorderStyle.None };
                form.Controls.Add (new Majorsilence.Forms.Label { AutoSize = true, Text = "Pip is keeping watch", Font = font (), Location = new System.Drawing.Point (10, 10) });
                return HeadlessRenderer.CapturePng (form, 420, 80);
            }

            var bundled = Render (() => display ? AlertBuddy.Shared.Theme.AlertFonts.Display (24) : AlertBuddy.Shared.Theme.AlertFonts.Body (24));
            var fallback = Render (() => new Majorsilence.Forms.Drawing.Font ("NoSuchFamilyAnywhere", 24, bold: display));

            Assert.False (bundled.AsSpan ().SequenceEqual (fallback), "the bundled family drew exactly what an unknown family draws");
        }

        [Theory]
        [InlineData (AlertBuddy.Core.Settings.LookPreference.Day)]
        [InlineData (AlertBuddy.Core.Settings.LookPreference.Night)]
        public void PlainText_IsDrawnInTheBundledBodyFont_NotWhateverTheMachineHas (AlertBuddy.Core.Settings.LookPreference look)
        {
            // Body text used to fall back to the platform's own sans-serif, so it differed from machine to machine (Roboto on the emulator,
            // Noto on one Linux box, something else on CI) and was not the Atkinson Hyperlegible the design asks for. The theme names the family.
            AlertBuddy.Shared.Theme.AlertBuddyTheme.Apply (look);
            byte[] Render (Func<Majorsilence.Forms.Drawing.Font>? font)
            {
                var form = new Majorsilence.Forms.Form { ClientSize = new System.Drawing.Size (420, 80), FormBorderStyle = Majorsilence.Forms.FormBorderStyle.None };
                var label = new Majorsilence.Forms.Label { AutoSize = true, Text = "Pip is keeping watch", Location = new System.Drawing.Point (10, 10) };
                if (font is not null)
                    label.Font = font ();
                form.Controls.Add (label);
                return HeadlessRenderer.CapturePng (form, 420, 80);
            }

            var themed = Render (null);
            // The theme's 18px is 13.5 in the font's own units.
            var explicitBody = Render (() => AlertBuddy.Shared.Theme.AlertFonts.Body (13.5f));
            var fallback = Render (() => new Majorsilence.Forms.Drawing.Font ("NoSuchFamilyAnywhere", 13.5f));

            Assert.True (themed.AsSpan ().SequenceEqual (explicitBody), "an unstyled label did not draw in the bundled body font");
            Assert.False (themed.AsSpan ().SequenceEqual (fallback), "an unstyled label drew the platform fallback");
        }

        [Fact]
        public void AFocusedTextBox_KeepsItsBorder_AsAFocusRing ()
        {
            // On the emulator the field being typed in lost its outline altogether (the focused border was drawn in the page colour, #366).
            // Samples the left edge of the box, vertically centred, with and without focus: the calm border is ink and the focused one is the
            // blueberry accent, so the box in use stands out and is never the page colour.
            SKColor EdgeOfTheBox (bool focused)
            {
                var form = new Majorsilence.Forms.Form { ClientSize = new System.Drawing.Size (300, 80), FormBorderStyle = Majorsilence.Forms.FormBorderStyle.None };
                var box = new Majorsilence.Forms.TextBox { Location = new System.Drawing.Point (20, 10), Size = new System.Drawing.Size (240, 50) };
                form.Controls.Add (box);
                form.Show ();
                if (focused)
                    box.Focus ();

                using var bitmap = SKBitmap.Decode (HeadlessRenderer.CapturePng (form, 300, 80));
                form.Close ();
                var scale = bitmap.Width / 300f;      // logical coordinates in, device pixels out
                return bitmap.GetPixel ((int)(21 * scale), (int)(35 * scale));
            }

            AlertBuddy.Shared.Theme.AlertBuddyTheme.Apply (AlertBuddy.Core.Settings.LookPreference.Day);
            var calm = EdgeOfTheBox (false);
            var focused = EdgeOfTheBox (true);

            Assert.Equal (SKColor.Parse ("2B1B4D"), calm);
            Assert.Equal (SKColor.Parse ("232E7A"), focused);
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

                // The permission steps sit part-way down the form: a tall window shows the whole of it.
                AssertInked (HeadlessRenderer.CapturePng (form, 420, 2400), "smoke-settings-tall.png");

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

                firstRun.ServerUrl = "https://ntfy.example.com";
                firstRun.Topic = "home-alerts";
                firstRun.NextCommand.Execute (null);
                Assert.Equal (AlertBuddy.ViewModels.Screens.FirstRunStep.Permissions, firstRun.Step);
                AssertInked (HeadlessRenderer.CapturePng (form, 420, 720), "smoke-firstrun-4.png");
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
