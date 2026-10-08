using AlertBuddy.Core.Settings;
using AlertBuddy.Core.Store;
using AlertBuddy.Shared.Platform;
using AlertBuddy.Shared.Theme;
using AlertBuddy.ViewModels;
using Majorsilence.Forms;

namespace AlertBuddy.Desktop
{
    internal static class Program
    {
        [STAThread]
        private static void Main (string[] args)
        {
            var appData = Path.Combine (Environment.GetFolderPath (Environment.SpecialFolder.ApplicationData), "AlertBuddy");
            Directory.CreateDirectory (appData);

            var settingsStore = new JsonFileSettingsStore (Path.Combine (appData, "settings.json"));
            SeedFirstRunDefaults (settingsStore);

            AlertBuddyTheme.Apply (settingsStore.Load ().Look);

            MainForm? form = null;
            var platform = new PlatformServices {
                Sound = new DesktopSoundPlayer (),
                Haptics = new DesktopHaptics (),
                Notifier = new DesktopAlertNotifier (),
                Speaker = new PlatformSpeaker (),
                Background = new DesktopBackgroundListener (),
                Dispatcher = new DesktopUiDispatcher (() => form ?? throw new InvalidOperationException ("The window has not been created yet.")),
                Secrets = new PlatformSecretStore (),
                SettingsStore = settingsStore,
                AlertState = new JsonFileAlertStateStore (Path.Combine (appData, "alerts.json")),
                KeepAwake = new DesktopKeepAwake (),
                Version = typeof (Program).Assembly.GetName ().Version?.ToString (3) ?? "0.0.0",
            };

            var app = AlertBuddyApp.Create (platform);
            form = new MainForm (app);
            using (var icon = typeof (Program).Assembly.GetManifestResourceStream ("AlertBuddy.Desktop.icon.png"))
                if (icon is not null)
                    form.Image = Majorsilence.Forms.Drawing.Image.FromStream (icon);
            app.Start ();

            Application.Run (form);
        }

        // There is no First run wizard yet (PLAN.md milestone 3): a brand-new desktop install seeds settings that point at the local
        // FakeNtfy dev loop this repo's own CLAUDE.md documents, so the app opens straight to Home with something to look at, instead
        // of the setup steps it cannot show yet. A grown-up can still change all of this in Settings once that screen exists.
        private static void SeedFirstRunDefaults (ISettingsStore store)
        {
            var current = store.Load ();
            if (current.FirstRunComplete)
                return;

            store.Save (current with {
                ServerUrl = "http://127.0.0.1:8080",
                Topic = "home-alerts",
                FirstRunComplete = true,
            });
        }
    }
}
