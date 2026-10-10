using AlertBuddy.Core.Settings;
using AlertBuddy.Shared;
using AlertBuddy.Shared.Platform;
using AlertBuddy.ViewModels;
using AlertBuddy.ViewModels.Screens;
using Majorsilence.Forms;

namespace AlertBuddy.Wasm
{
    public class Program
    {
        // No [STAThread]/blocking Application.Run: the browser backend starts asynchronously (attaching
        // to the "out" div in wwwroot/index.html) and, once started, is driven by the browser's own
        // JS event loop.
        //
        // The browser build is a demo, not a receiver (PLAN.md section 1): it never connects to a server and opens on Practice, so a
        // visitor can see what the buddy does. It also does not call Start () on the listener.
        private static Task Main (string[] args) => Application.RunBrowserAsync (() => {
            MainForm? form = null;
            var app = AlertBuddyApp.Create (new PlatformServices {
                Sound = new BrowserSoundPlayer (),
                Haptics = new QuietHaptics (),
                Notifier = new QuietNotifier (),
                Background = new ForegroundOnlyListener ("This is a demo in a browser. It does not receive real alerts."),
                Dispatcher = new DesktopUiDispatcher (() => form ?? throw new InvalidOperationException ("The window has not been created yet.")),
                Secrets = PlatformSecretStore.MemoryOnly (),
                SettingsStore = new MemorySettingsStore (new AppSettings { FirstRunComplete = true, Pin = null }),
                AlertState = new MemoryAlertStateStore (),
                Version = "demo",
            });

            form = new MainForm (app);
            app.Navigator.GoTo<PracticeViewModel> ();
            return form;
        });
    }
}
