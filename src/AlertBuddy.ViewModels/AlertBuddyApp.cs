using AlertBuddy.Core.Abstractions;
using AlertBuddy.Core.Ntfy;
using AlertBuddy.Core.Settings;
using AlertBuddy.Core.Store;
using AlertBuddy.ViewModels.Screens;
using AlertBuddy.ViewModels.Services;
using CommunityToolkit.Mvvm.ComponentModel;

namespace AlertBuddy.ViewModels
{
    /// <summary>
    /// What a head (Android, desktop, the console harness) supplies: the parts that touch the platform. Everything else in the app is built
    /// from these, so a test can supply fakes and run the real app.
    /// </summary>
    public sealed class PlatformServices
    {
        /// <summary>Plays sounds.</summary>
        public required ISoundPlayer Sound { get; init; }

        /// <summary>Vibrates.</summary>
        public required IHaptics Haptics { get; init; }

        /// <summary>Shows notifications.</summary>
        public required IAlertNotifier Notifier { get; init; }

        /// <summary>Reads alerts aloud when a grown-up has turned that on; null where the device has no voice.</summary>
        public ISpeaker? Speaker { get; init; }

        /// <summary>Reports whether the app can listen in the background.</summary>
        public required IBackgroundListener Background { get; init; }

        /// <summary>Runs work on the UI thread.</summary>
        public required IUiDispatcher Dispatcher { get; init; }

        /// <summary>Stores the password and the token.</summary>
        public required ISecretStore Secrets { get; init; }

        /// <summary>Stores the settings.</summary>
        public required ISettingsStore SettingsStore { get; init; }

        /// <summary>Stores the alerts between runs; null keeps them in memory only.</summary>
        public IAlertStateStore? AlertState { get; init; }

        /// <summary>The app lifecycle and back button; null on a platform with neither.</summary>
        public ILifecycle? Lifecycle { get; init; }

        /// <summary>The permissions a grown-up can allow so alarms are heard; null where there is nothing to allow.</summary>
        public IPermissionGuide? Permissions { get; init; }

        /// <summary>Keeps the screen on for bedside mode; null where unavailable.</summary>
        public IKeepAwake? KeepAwake { get; init; }

        /// <summary>The HTTP handler for the listener and the connection test; null uses the production one. Tests pass a scripted one.</summary>
        public HttpMessageHandler? HttpHandler { get; init; }

        /// <summary>The source of time; the system's by default. Tests pass a manual one.</summary>
        public TimeProvider Time { get; init; } = TimeProvider.System;

        /// <summary>The time zone quiet hours and "Today" are read in; the device's by default.</summary>
        public TimeZoneInfo? Zone { get; init; }

        /// <summary>The app version, for About.</summary>
        public string Version { get; init; } = "";
    }

    /// <summary>
    /// The composition root: builds the listener, the store, the hub, the feedback, the screens and the navigator once, in one place, from
    /// <see cref="PlatformServices"/> (PLAN.md section 7.5). Nothing else in the app constructs one of these.
    /// </summary>
    public sealed class AlertBuddyApp : IAsyncDisposable
    {
        private readonly AlertFeedback feedback;
        private readonly TakeoverCoordinator takeover;
        private readonly ILifecycle? lifecycle;
        private readonly Func<bool>? backHandler;

        private AlertBuddyApp (
            SettingsService settings,
            AlertHub hub,
            AlertEngine engine,
            AlertListener listener,
            Navigator navigator,
            MainViewModel main,
            AlertFeedback feedback,
            TakeoverCoordinator takeover,
            ILifecycle? lifecycle,
            Func<bool>? backHandler)
        {
            Settings = settings;
            Hub = hub;
            Engine = engine;
            Listener = listener;
            Navigator = navigator;
            Main = main;
            this.feedback = feedback;
            this.takeover = takeover;
            this.lifecycle = lifecycle;
            this.backHandler = backHandler;
        }

        /// <summary>The settings, in memory, with change notification.</summary>
        public SettingsService Settings { get; }

        /// <summary>Where alerts and connection state are announced.</summary>
        public AlertHub Hub { get; }

        /// <summary>The pipeline from a stream event to the screen.</summary>
        public AlertEngine Engine { get; }

        /// <summary>The running subscription.</summary>
        public AlertListener Listener { get; }

        /// <summary>The screen stack. Its <see cref="INavigator.Current"/> is what a view host shows.</summary>
        public Navigator Navigator { get; }

        /// <summary>Home, the root screen.</summary>
        public MainViewModel Main { get; }

        /// <summary>Builds the app. Call <see cref="Start"/> to begin listening.</summary>
        public static AlertBuddyApp Create (PlatformServices platform)
        {
            ArgumentNullException.ThrowIfNull (platform);

            var time = platform.Time;
            var clock = new TimeProviderClock (time);
            var scheduler = new TimeProviderScheduler (time, platform.Dispatcher);
            var settings = new SettingsService (platform.SettingsStore);
            var hub = new AlertHub ();

            var current = settings.Current;
            var store = new AlertStore (new AlertStoreOptions { SilenceWindow = current.SilenceWindow });
            var engine = new AlertEngine (store, hub, clock, platform.AlertState, new Core.Interpretation.AlertInterpreter (current.Interpretation));

            // Changing a setting takes effect for the next message: how they are read, and how long a repeated alarm stays quiet.
            settings.Changed += () => engine.Configure (settings.Current);

            var listener = new AlertListener (engine, settings, platform.Secrets, platform.HttpHandler, time);
            var tester = new NtfyConnectionTester (platform.HttpHandler, time);
            var gateLock = new GateLock (clock);

            var navigator = new Navigator ();
            var screens = new ScreenFactory ();
            var main = new MainViewModel (hub, navigator, screens, clock, platform.Dispatcher, scheduler, settings, platform.Background, platform.Zone, platform.KeepAwake);
            navigator.SetRoot (main);

            navigator.Register (() => new AlertBookViewModel (engine, hub, navigator, screens, clock, platform.Dispatcher, platform.Zone));
            navigator.Register (() => new PracticeViewModel (settings, clock, platform.Dispatcher, scheduler, platform.Sound, navigator));
            navigator.Register (() => new SettingsViewModel (settings, platform.Secrets, tester, listener, engine, navigator, platform.Version, platform.Permissions, platform.Lifecycle, platform.Speaker, platform.Sound));
            navigator.Register (() => new FirstRunViewModel (settings, platform.Secrets, tester, listener, navigator, platform.Background, platform.Permissions, platform.Lifecycle));

            screens.Wire (
                alert => new AlertDetailViewModel (alert, engine, hub, navigator, clock, platform.Dispatcher, platform.Zone),
                alert => new AlarmViewModel (alert, engine, hub, platform.Dispatcher),
                (onUnlocked, holdDone) => new GateViewModel (settings, navigator, scheduler, gateLock, onUnlocked, holdDone));

            var feedback = new AlertFeedback (hub, platform.Sound, platform.Haptics, platform.Notifier, settings, clock, platform.Zone, platform.Speaker, scheduler);
            var takeover = new TakeoverCoordinator (hub, navigator, screens, platform.Dispatcher);

            // The back button steps back one screen before it ever leaves the app (PLAN.md section 6.5).
            Func<bool>? backHandler = null;
            if (platform.Lifecycle is { } lifecycle) {
                backHandler = navigator.HandleBack;
                lifecycle.BackPressed += backHandler;
            }

            var app = new AlertBuddyApp (settings, hub, engine, listener, navigator, main, feedback, takeover, platform.Lifecycle, backHandler);

            // Before first run has finished there is nothing to listen to: the steps are shown on top of Home.
            if (!settings.Current.FirstRunComplete)
                navigator.GoTo<FirstRunViewModel> ();

            return app;
        }

        /// <summary>Starts listening, if the server is set up.</summary>
        public void Start () => Listener.Start ();

        /// <summary>Stops everything and lets go of what was subscribed.</summary>
        public async ValueTask DisposeAsync ()
        {
            if (lifecycle is not null && backHandler is not null)
                lifecycle.BackPressed -= backHandler;

            takeover.Dispose ();
            feedback.Dispose ();
            await Listener.DisposeAsync ().ConfigureAwait (false);
            Main.Dispose ();
        }

        // Builds the screens that need an argument. The delegates are supplied by Create, once everything they need exists.
        private sealed class ScreenFactory : IScreenFactory
        {
            private Func<Core.Alerts.Alert, AlertDetailViewModel>? detail;
            private Func<Core.Alerts.Alert, AlarmViewModel>? alarm;
            private Func<Action, bool, GateViewModel>? gate;

            public void Wire (Func<Core.Alerts.Alert, AlertDetailViewModel> detail, Func<Core.Alerts.Alert, AlarmViewModel> alarm, Func<Action, bool, GateViewModel> gate)
            {
                this.detail = detail;
                this.alarm = alarm;
                this.gate = gate;
            }

            public AlertDetailViewModel Detail (Core.Alerts.Alert alert) => detail!.Invoke (alert);

            public AlarmViewModel Alarm (Core.Alerts.Alert alert) => alarm!.Invoke (alert);

            public GateViewModel Gate (Action onUnlocked, bool holdDone = false) => gate!.Invoke (onUnlocked, holdDone);
        }
    }
}
