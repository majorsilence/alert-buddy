using AlertBuddy.Core.Alerts;
using AlertBuddy.Core.Settings;
using AlertBuddy.Core.Store;
using AlertBuddy.FakeNtfy;
using AlertBuddy.ViewModels;
using AlertBuddy.ViewModels.Screens;
using AlertBuddy.ViewModels.Services;

// The milestone 1 harness: a warning, an alarm, the child's tap, an all clear, a dropped connection and its recovery, all driven through
// view models. Nothing here draws anything: the "screen" is what a view would be told, printed as text. The server is the fake one, over real
// HTTP, so the real listener, subscription and parser run exactly as they would against ntfy.

await using var server = await FakeNtfyServer.StartAsync (new FakeNtfyOptions { Keepalive = TimeSpan.FromSeconds (5) });
Console.WriteLine ($"Fake ntfy at {server.BaseUri}\n");

var settingsStore = new MemorySettings (new AppSettings {
    ServerUrl = server.BaseUri.ToString ().TrimEnd ('/'),
    Topic = "home-alerts",
    BuddyName = "Pip",
    FirstRunComplete = true,
    Night = new NightPolicy { Enabled = false },
});

await using var app = AlertBuddyApp.Create (new PlatformServices {
    Sound = new ConsoleSound (),
    Haptics = new ConsoleHaptics (),
    Notifier = new ConsoleNotifier (),
    Background = new NoBackground (),
    Dispatcher = new LockingDispatcher (),
    Secrets = new MemorySecrets (),
    SettingsStore = settingsStore,
    Version = "harness",
});

void Show (string step)
{
    var main = app.Main;
    Console.WriteLine ($"--- {step}");
    Console.WriteLine ($"    screen      : {app.Navigator.Current.GetType ().Name}");
    Console.WriteLine ($"    beacon      : {main.Mood}");
    Console.WriteLine ($"    buddy says  : {main.StatusText}");
    Console.WriteLine ($"    connection  : {main.ConnectionText}");
    foreach (var ticket in main.ActiveAlerts)
        Console.WriteLine ($"    ticket      : {ticket.Source} ({ticket.Level}, {ticket.Status}), {ticket.Sentence}");
    if (app.Navigator.Current is AlarmViewModel alarm)
        Console.WriteLine ($"    takeover    : \"{alarm.Heading}\" \"{alarm.Detail}\" [{alarm.ToldButtonText}]");
    Console.WriteLine ();
}

static async Task Until (Func<bool> condition, string what)
{
    var deadline = DateTime.UtcNow + TimeSpan.FromSeconds (20);
    while (!condition ()) {
        if (DateTime.UtcNow > deadline)
            throw new TimeoutException ($"Timed out waiting for {what}.");
        await Task.Delay (25);
    }
}

app.Start ();
await Until (() => app.Hub.Snapshot.Connection.State == AlertBuddy.Core.Ntfy.ConnectionState.Live, "the connection");
Show ("connected, all quiet");

server.Publish ("home-alerts", "Workshop is at 41.2 °C", "Workshop: temperature warning", 4);
await Until (() => app.Main.ActiveAlerts.Count == 1, "the warning");
Show ("a warning arrives");

server.Publish ("home-alerts", "Workshop is at 50.6 °C", "Workshop: temperature alarm", 5);
await Until (() => app.Navigator.Current is AlarmViewModel, "the takeover");
Show ("it becomes an alarm: one card, and the takeover");

((AlarmViewModel)app.Navigator.Current).ToldAGrownUpCommand.Execute (null);
Show ("the child says \"I told a grown-up\"");

server.Publish ("home-alerts", "Workshop is at 44.0 °C", "Workshop: temperature alarm (resolved)", 3);
await Until (() => app.Main.ActiveAlerts.Count == 0, "the all clear");
Show ("the all clear");

await Task.Delay (TimeSpan.FromSeconds (4.5));
Show ("four seconds later");

Console.WriteLine ("*** the network drops ***\n");
server.DropConnections ();
await Until (() => app.Hub.Snapshot.Connection.State == AlertBuddy.Core.Ntfy.ConnectionState.Reconnecting, "the drop");
Show ("connection dropped");

server.Publish ("home-alerts", "Sunny room is at 39.0 °C", "Sunny room: temperature warning", 4);       // published while nobody is listening
await Until (() => app.Main.ActiveAlerts.Count == 1, "the message from the gap");
Show ("reconnected: the message sent during the gap arrives");

Console.WriteLine ($"Alert Book: {string.Join (", ", app.Hub.Snapshot.History.Select (a => $"{a.Source} [{a.Status}]"))}");
Console.WriteLine ("Done.");

// ---- platform fakes: they say what they would have done ----

sealed class ConsoleSound : ISoundPlayer
{
    public bool IsSupported => true;
    public void Play (Cue cue, double volume = 1) => Console.WriteLine ($"    (sound: {cue})");
    public void StartLoop (Cue cue) => Console.WriteLine ($"    (sound: {cue} on a loop)");
    public void StopLoop () => Console.WriteLine ("    (sound: stopped)");
}

sealed class ConsoleHaptics : IHaptics
{
    public bool IsSupported => true;
    public void Tap () { }
    public void Alarm () => Console.WriteLine ("    (vibrating)");
    public void Stop () => Console.WriteLine ("    (vibration stopped)");
}

sealed class ConsoleNotifier : IAlertNotifier
{
    public void Show (Alert alert) => Console.WriteLine ($"    (notification: {alert.Source}, {alert.Level}, {alert.Status})");
    public void Clear (string alertId) => Console.WriteLine ("    (notification cleared)");
}

sealed class NoBackground : IBackgroundListener
{
    public bool CanListenInBackground => true;
    public string? WhyNot => null;
    public void Start () { }
    public void Stop () { }
}

// One action at a time, like a UI thread.
sealed class LockingDispatcher : IUiDispatcher
{
    private readonly object gate = new ();
    public void Post (Action action)
    {
        lock (gate)
            action ();
    }
}

sealed class MemorySettings (AppSettings initial) : ISettingsStore
{
    private AppSettings current = initial;
    public AppSettings Load () => current;
    public void Save (AppSettings settings) => current = settings;
}

sealed class MemorySecrets : ISecretStore
{
    private readonly Dictionary<string, string> values = new ();
    public string? Get (string key) => values.GetValueOrDefault (key);
    public void Set (string key, string value) => values[key] = value;
    public void Remove (string key) => values.Remove (key);
}
