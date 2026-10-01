using System.ComponentModel;
using AlertBuddy.Core.Alerts;
using AlertBuddy.Core.Ntfy;
using AlertBuddy.Core.Settings;
using AlertBuddy.Core.Store;
using AlertBuddy.TestSupport;
using AlertBuddy.ViewModels;
using AlertBuddy.ViewModels.Screens;
using AlertBuddy.ViewModels.Services;
using Xunit;

[assembly: CollectionBehavior (DisableTestParallelization = true)]

namespace AlertBuddy.ViewModels.Tests
{
    /// <summary>
    /// Runs work at once, on the caller's thread, but one action at a time, as the real UI thread does. Without the lock, the listener's
    /// background threads (one delivering messages, one delivering connection state) would run a view model's refresh concurrently, which
    /// the real dispatcher can never do. The lock is re-entrant, so an action may post another.
    /// </summary>
    internal sealed class SynchronousDispatcher : IUiDispatcher
    {
        private readonly object gate = new ();

        public void Post (Action action)
        {
            lock (gate)
                action ();
        }
    }

    internal sealed class RecordingSound : ISoundPlayer
    {
        public List<string> Calls { get; } = [];
        public bool IsSupported => true;
        public void Play (Cue cue) => Calls.Add ($"Play:{cue}");
        public void StartLoop (Cue cue) => Calls.Add ($"Loop:{cue}");
        public void StopLoop () => Calls.Add ("StopLoop");
    }

    internal sealed class RecordingHaptics : IHaptics
    {
        public List<string> Calls { get; } = [];
        public bool IsSupported => true;
        public void Tap () => Calls.Add ("Tap");
        public void Alarm () => Calls.Add ("Alarm");
        public void Stop () => Calls.Add ("Stop");
    }

    internal sealed class RecordingNotifier : IAlertNotifier
    {
        public List<Alert> Shown { get; } = [];
        public List<string> Cleared { get; } = [];
        public void Show (Alert alert) => Shown.Add (alert);
        public void Clear (string alertId) => Cleared.Add (alertId);
    }

    internal sealed class FakeBackground : IBackgroundListener
    {
        public string? WhyNot { get; set; }
        public bool CanListenInBackground => WhyNot is null;
        public void Start () { }
        public void Stop () { }
    }

    internal sealed class FakeTester : IConnectionTester
    {
        public ConnectionTestResult Result { get; set; } = new (true, ConnectionProblem.None, "Connected. Fine.");
        public List<(string Url, string Topic, string? Authorization)> Calls { get; } = [];
        public TaskCompletionSource? Gate { get; set; }

        public async Task<ConnectionTestResult> TestAsync (string serverUrl, string topic, NtfyCredentials credentials, CancellationToken cancellationToken)
        {
            Calls.Add ((serverUrl, topic, credentials.AuthorizationHeaderValue));
            if (Gate is not null)
                await Gate.Task.WaitAsync (cancellationToken);
            return Result;
        }
    }

    internal sealed class FakeListener : IListenerControl
    {
        public int Restarts { get; private set; }
        public bool IsRunning => false;
        public Task RestartAsync ()
        {
            Restarts++;
            return Task.CompletedTask;
        }
    }

    internal sealed class FakeKeepAwake : IKeepAwake
    {
        public bool Enabled { get; set; }
    }

    internal sealed class FakePermissionGuide : IPermissionGuide
    {
        public List<PermissionItem> Current { get; } = [
            new (PermissionKind.Notifications, "Notifications", "So an alarm can be shown.", false),
            new (PermissionKind.AlarmVolume, "Alarm volume", "So it is loud enough.", null),
        ];

        public List<PermissionKind> Opened { get; } = [];

        public IReadOnlyList<PermissionItem> Items => Current;

        public void Open (PermissionKind kind) => Opened.Add (kind);
    }

    internal sealed class FakeLifecycle : ILifecycle
    {
        public event Action? Resumed;
        public event Action? Paused;
        public event Func<bool>? BackPressed;

        public bool PressBack () => BackPressed?.Invoke () ?? false;
        public void Resume () => Resumed?.Invoke ();
        public void Pause () => Paused?.Invoke ();
    }

    /// <summary>Records which properties raised <see cref="INotifyPropertyChanged.PropertyChanged"/>, by name.</summary>
    internal sealed class PropertyLog
    {
        public List<string?> Names { get; } = [];

        public PropertyLog (INotifyPropertyChanged source) => source.PropertyChanged += (_, e) => Names.Add (e.PropertyName);
    }

    /// <summary>
    /// The real app, built through the real composition root, with fakes for everything that touches the platform, and the alerts fed in
    /// by hand. What a test drives here is what ships.
    /// </summary>
    internal sealed class AppRig : IAsyncDisposable
    {
        public static readonly DateTimeOffset Start = DateTimeOffset.FromUnixTimeSeconds (1790000100);

        public TestClock Clock { get; } = new (Start);
        public RecordingSound Sound { get; } = new ();
        public RecordingHaptics Haptics { get; } = new ();
        public RecordingNotifier Notifier { get; } = new ();
        public FakeBackground Background { get; } = new ();
        public FakeTester Tester { get; } = new ();
        public FakeLifecycle Lifecycle { get; } = new ();
        public FakeKeepAwake KeepAwake { get; } = new ();
        public FakePermissionGuide Permissions { get; } = new ();
        public InMemorySecretStore Secrets { get; } = new ();
        public InMemorySettingsStore SettingsStore { get; } = new ();
        public InMemoryAlertStateStore AlertState { get; } = new ();
        public FakeNtfyServer Server { get; } = new ();
        public AlertBuddyApp App { get; }

        public AlertHub Hub => App.Hub;
        public AlertEngine Engine => App.Engine;
        public MainViewModel Main => App.Main;
        public Navigator Navigator => App.Navigator;

        /// <param name="settings">Starting settings. By default first run is complete, so the app opens on Home.</param>
        public AppRig (AppSettings? settings = null, Action<AppRig>? beforeCreate = null)
        {
            SettingsStore.Current = settings ?? new AppSettings {
                ServerUrl = "https://ntfy.example.com",
                Topic = "home-alerts",
                FirstRunComplete = true,
                // The tests run at midday, but this keeps the night policy from being an accident of the fixture's date.
                Night = new NightPolicy { Enabled = false },
            };

            beforeCreate?.Invoke (this);

            App = AlertBuddyApp.Create (new PlatformServices {
                Sound = Sound,
                Haptics = Haptics,
                Notifier = Notifier,
                Background = Background,
                Dispatcher = new SynchronousDispatcher (),
                Secrets = Secrets,
                SettingsStore = SettingsStore,
                AlertState = AlertState,
                Lifecycle = Lifecycle,
                KeepAwake = KeepAwake,
                Permissions = Permissions,
                HttpHandler = Server,
                Time = Clock.Provider,
                Zone = TimeZoneInfo.Utc,
                Version = "1.2.3",
            });

            App.Hub.AlertChanged += _ => Interlocked.Increment (ref alertEvents);   // last subscriber: see AlertEventsAsync
        }

        private int alertEvents;

        /// <summary>
        /// Waits until <paramref name="count"/> alert events have been announced AND every subscriber has finished handling them. The probe
        /// that counts them subscribed last, and the hub calls subscribers in order, so once it has run the screens, the feedback and the
        /// takeover coordinator have all run. Waiting on a screen's own state instead can pass while a later subscriber is still working.
        /// </summary>
        public Task AlertEventsAsync (int count) => Wait.UntilAsync (() => Volatile.Read (ref alertEvents) >= count, $"{count} alert events (saw {Volatile.Read (ref alertEvents)})");

        /// <summary>Tells the app the connection is live, as the listener would.</summary>
        public void GoLive () => Engine.SetConnection (new ConnectionInfo (ConnectionState.Live, LastHeard: Clock.Now));

        /// <summary>Feeds one ntfy message in as if it had just arrived live, stamped with the clock's current time.</summary>
        public AlertChange? Send (string title, int priority = 3, string body = "", string? id = null, MessageOrigin origin = MessageOrigin.Live, TimeSpan? age = null)
        {
            id ??= "m" + Interlocked.Increment (ref counter);
            var message = new NtfyMessage (id, Clock.Now - (age ?? TimeSpan.Zero), "home-alerts", title, body, priority, []);
            return Engine.Handle (new NtfyEvent (NtfyEventKind.Message, message, origin));
        }

        public AlertChange? Warning (string source = "Workshop", double? degrees = 41.2, string? id = null)
            => Send ($"{source}: temperature warning", 4, degrees is { } d ? $"{source} is at {d} °C" : "needs a look", id);

        public AlertChange? Alarm (string source = "Workshop", double? degrees = 50.6, string? id = null)
            => Send ($"{source}: temperature alarm", 5, degrees is { } d ? $"{source} is at {d} °C" : "needs a look", id);

        public AlertChange? AllClear (string source = "Workshop", double? degrees = 44.0)
            => Send ($"{source}: temperature alarm (resolved)", 3, degrees is { } d ? $"{source} is at {d} °C" : "fine", null);

        public T Current<T> () where T : class => Assert.IsType<T> (Navigator.Current);

        private int counter;

        public ValueTask DisposeAsync () => App.DisposeAsync ();
    }

    internal sealed class DummyHome : CommunityToolkit.Mvvm.ComponentModel.ObservableObject
    {
    }

    /// <summary>
    /// The forms (first run and settings) built directly with fakes, so a test can see what they did to the listener, the secret store and
    /// the settings file without a whole app around them.
    /// </summary>
    internal sealed class FormRig
    {
        public InMemorySettingsStore Store { get; } = new ();
        public SettingsService Settings { get; }
        public InMemorySecretStore Secrets { get; } = new ();
        public FakeTester Tester { get; } = new ();
        public FakeListener Listener { get; } = new ();
        public FakeBackground Background { get; } = new ();
        public TestClock Clock { get; } = new (AppRig.Start);
        public AlertHub Hub { get; } = new ();
        public AlertEngine Engine { get; }
        public Navigator Navigator { get; } = new ();
        public DummyHome Root { get; } = new ();

        public FormRig (AppSettings? settings = null)
        {
            Store.Current = settings ?? new AppSettings ();
            Settings = new SettingsService (Store);
            Engine = new AlertEngine (new AlertStore (), Hub, Clock);
            Navigator.SetRoot (Root);
        }

        public FirstRunViewModel FirstRun ()
        {
            var vm = new FirstRunViewModel (Settings, Secrets, Tester, Listener, Navigator, Background);
            Navigator.Show (vm);
            return vm;
        }

        public SettingsViewModel SettingsScreen (string version = "1.2.3")
        {
            var vm = new SettingsViewModel (Settings, Secrets, Tester, Listener, Engine, Navigator, version);
            Navigator.Show (vm);
            return vm;
        }
    }
}
