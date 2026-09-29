using AlertBuddy.Core.Alerts;
using AlertBuddy.Core.Ntfy;
using AlertBuddy.Core.Settings;
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
    }
}
