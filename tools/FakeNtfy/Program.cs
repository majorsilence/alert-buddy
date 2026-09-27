using System.Globalization;
using System.Runtime.InteropServices;
using AlertBuddy.FakeNtfy;

// A local ntfy-compatible server for developing and demonstrating Alert Buddy with no real server. Everything it serves is invented.
//
//   dotnet run --project tools/FakeNtfy -- --port 8080
//   curl -d "Workshop is at 41 C" -H "Title: Workshop: temperature warning" -H "Priority: 4" http://127.0.0.1:8080/home-alerts
//   curl -X POST "http://127.0.0.1:8080/_scenario/home-alerts?step=5"     # a warning, an alarm and an all clear, 5 seconds apart
//
// From an Android emulator the host is 10.0.2.2. For a phone on the same network add --lan and use the machine's address.

string? Arg (string name) => args.SkipWhile (a => a != name).Skip (1).FirstOrDefault ();

if (args.Contains ("--help")) {
    Console.WriteLine ("Options: --port N  --lan  --keepalive SECONDS  --basic USER:PASSWORD  --token TOKEN");
    return;
}

var basic = Arg ("--basic")?.Split (':', 2);
var options = new FakeNtfyOptions {
    Port = int.TryParse (Arg ("--port"), CultureInfo.InvariantCulture, out var port) ? port : 0,
    Lan = args.Contains ("--lan"),
    Keepalive = TimeSpan.FromSeconds (double.TryParse (Arg ("--keepalive"), CultureInfo.InvariantCulture, out var k) ? k : 45),
    BasicUser = basic?[0],
    BasicPassword = basic is { Length: 2 } ? basic[1] : null,
    Token = Arg ("--token"),
};

await using var server = await FakeNtfyServer.StartAsync (options);
Console.WriteLine ($"Fake ntfy listening on {server.BaseUri}");
if (options.Lan)
    Console.WriteLine ("Listening on every interface (--lan): anyone on the network can reach it.");
if (options.BasicUser is not null || options.Token is not null)
    Console.WriteLine ("Sign-in is required.");
Console.WriteLine ("Press Ctrl+C to stop.");

using var stop = new CancellationTokenSource ();
Console.CancelKeyPress += (_, e) => {
    e.Cancel = true;
    stop.Cancel ();
};

// The web host claims SIGTERM and only signals its own lifetime, which nothing here waits on, so without this a plain `kill` (or a
// container stop) left the server listening while any client held a stream open.
using var terminate = PosixSignalRegistration.Create (PosixSignal.SIGTERM, context => {
    context.Cancel = true;
    stop.Cancel ();
});

try {
    await Task.Delay (Timeout.Infinite, stop.Token);
} catch (OperationCanceledException) {
}
