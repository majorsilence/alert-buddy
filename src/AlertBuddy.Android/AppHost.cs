using Android.Content;
using AlertBuddy.Android.Platform;
using AlertBuddy.Core.Settings;
using AlertBuddy.Core.Store;
using AlertBuddy.Shared.Platform;
using AlertBuddy.ViewModels;

namespace AlertBuddy.Android
{
    /// <summary>
    /// The one <see cref="AlertBuddyApp"/> of the process. The foreground service and the activity share it, which is why the UI can
    /// subscribe to the in-process hub with no IPC (PLAN.md section 6.2): the service may start it with no window, and a window opened
    /// later finds it already listening.
    /// </summary>
    internal static class AppHost
    {
        private static readonly object Gate = new ();
        private static AlertBuddyApp? app;

        public static AndroidLifecycle Lifecycle { get; } = new ();

        public static JsonFileSettingsStore Settings { get; private set; } = null!;

        public static AlertBuddyApp Get (Context context)
        {
            lock (Gate) {
                if (app is not null)
                    return app;

                var appContext = context.ApplicationContext ?? context;
                var data = System.Environment.GetFolderPath (System.Environment.SpecialFolder.LocalApplicationData);
                Settings = new JsonFileSettingsStore (System.IO.Path.Combine (data, "settings.json"));

                var sound = new AndroidSoundPlayer (appContext);
                app = AlertBuddyApp.Create (new PlatformServices {
                    Sound = sound,
                    Haptics = new AndroidHaptics (),
                    Notifier = new AndroidAlertNotifier (),
                    Background = new AndroidBackgroundListener (appContext),
                    Dispatcher = new AndroidUiDispatcher (),
                    Secrets = new PlatformSecretStore (),
                    KeepAwake = new DesktopKeepAwake (),
                    Lifecycle = Lifecycle,
                    Permissions = new AndroidPermissionGuide (appContext, sound),
                    SettingsStore = Settings,
                    AlertState = new JsonFileAlertStateStore (System.IO.Path.Combine (data, "alerts.json")),
                    Version = typeof (AppHost).Assembly.GetName ().Version?.ToString (3) ?? "0.0.0",
                });

                return app;
            }
        }
    }
}
