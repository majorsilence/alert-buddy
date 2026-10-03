using AlertBuddy.Core.Ntfy;
using AlertBuddy.ViewModels.Screens;
using Majorsilence.Forms.Headless;
using SkiaSharp;
using Xunit;

namespace AlertBuddy.Shared.Tests
{
    /// <summary>
    /// A few screens rendered at scale 1 and compared with committed images (PLAN.md section 10), so a change that moves or recolours
    /// something fails the build and has to be looked at. Only screens with no clock-dependent text ("a few seconds") are here. After an
    /// intended visual change, look at the new render in <c>render-out/</c>, then run with <c>UPDATE_GOLDENS=1</c> to replace the images and
    /// commit them. The tolerance forgives anti-aliasing noise, not a moved button.
    /// </summary>
    public class GoldenTests
    {
        private const int ChannelTolerance = 12;       // per colour channel, out of 255
        private const double DifferingFraction = 0.002;  // of all pixels

        private static string GoldenPath (string name)
        {
            var dir = Directory.GetCurrentDirectory ();
            while (dir is not null && !File.Exists (Path.Combine (dir, "AlertBuddy.slnx")))
                dir = Path.GetDirectoryName (dir);

            return Path.Combine (dir!, "tests", "AlertBuddy.Shared.Tests", "Golden", name + ".png");
        }

        private static void AssertMatchesGolden (string name, byte[] png)
        {
            // The images are scale 1; at a scaled display the render is a different size, and the scaled shape is covered by the layout tests.
            if (Environment.GetEnvironmentVariable ("MF_HEADLESS_SCALE") is { Length: > 0 } scale && scale != "1")
                Assert.Skip ("golden images are rendered at scale 1");

            File.WriteAllBytes (Path.Combine (TestEnvironment.RenderDirectory, "golden-" + name + ".png"), png);
            var path = GoldenPath (name);

            if (Environment.GetEnvironmentVariable ("UPDATE_GOLDENS") == "1") {
                File.WriteAllBytes (path, png);
                return;
            }

            Assert.True (File.Exists (path), $"No golden image for {name}; run once with UPDATE_GOLDENS=1 after looking at render-out/golden-{name}.png");

            using var expected = SKBitmap.Decode (path);
            using var actual = SKBitmap.Decode (png);
            Assert.Equal ((expected.Width, expected.Height), (actual.Width, actual.Height));

            var differing = 0;
            for (var y = 0; y < expected.Height; y++) {
                for (var x = 0; x < expected.Width; x++) {
                    var a = expected.GetPixel (x, y);
                    var b = actual.GetPixel (x, y);
                    if (Math.Abs (a.Red - b.Red) > ChannelTolerance || Math.Abs (a.Green - b.Green) > ChannelTolerance || Math.Abs (a.Blue - b.Blue) > ChannelTolerance)
                        differing++;
                }
            }

            var fraction = differing / (double)(expected.Width * expected.Height);
            Assert.True (fraction <= DifferingFraction, $"{name}: {fraction:P2} of the pixels differ from the golden image (see render-out/golden-{name}.png)");
        }

        [Fact]
        public async Task Home_Calm ()
        {
            var app = SmokeTests.CreateApp ();
            try {
                AssertMatchesGolden ("home-calm", HeadlessRenderer.CapturePng (new MainForm (app), 420, 720));
            } finally {
                await app.DisposeAsync ();
            }
        }

        [Fact]
        public async Task AlarmTakeover ()
        {
            var app = SmokeTests.CreateApp ();
            try {
                var form = new MainForm (app);
                app.Engine.Handle (new NtfyEvent (NtfyEventKind.Message, new NtfyMessage (
                    "a1", DateTimeOffset.UtcNow, "home-alerts", "Workshop: temperature alarm", "Workshop is at 50.6 °C", 5, [])));
                Assert.IsType<AlarmViewModel> (app.Navigator.Current);
                AssertMatchesGolden ("alarm-takeover", HeadlessRenderer.CapturePng (form, 420, 720));
            } finally {
                await app.DisposeAsync ();
            }
        }

        [Fact]
        public async Task FirstRun_NameStep ()
        {
            var app = SmokeTests.CreateApp ();
            try {
                var form = new MainForm (app);
                app.Navigator.GoTo<FirstRunViewModel> ();
                AssertMatchesGolden ("firstrun-name", HeadlessRenderer.CapturePng (form, 420, 720));
            } finally {
                await app.DisposeAsync ();
            }
        }

        [Fact]
        public async Task Practice_BeforeItStarts ()
        {
            var app = SmokeTests.CreateApp ();
            try {
                var form = new MainForm (app);
                app.Main.StartPracticeCommand.Execute (null);
                Assert.IsType<PracticeViewModel> (app.Navigator.Current);
                AssertMatchesGolden ("practice-idle", HeadlessRenderer.CapturePng (form, 420, 720));
            } finally {
                await app.DisposeAsync ();
            }
        }
    }
}
