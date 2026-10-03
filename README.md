# Alert Buddy

A friendly alert receiver for a family. It connects to a self-hosted [ntfy](https://ntfy.sh) server and turns "something
needs a look" messages into a small character, a sound, and one clear instruction: **tell a grown-up**.

It is built for a child who likes alerts: the child is a helper and tells a grown-up, and the grown-up owns the response. The
first alerts it carries are home temperature warnings and alarms, but it understands any ntfy topic.

> **Alert Buddy is a helper. It does not replace smoke or heat alarms.** Keep another receiver, such as the official ntfy app,
> running for anything that matters.

## Status

Working on desktop and in the Android emulator; **not yet run on a real phone, and there is no release to download yet**. What
exists: the alert pipeline (an ntfy client that reconnects and replays what it missed), every screen (Home, the alarm takeover,
alert detail, the Alert book, Practice, First run, Settings, the grown-up gate), a foreground service for Android, notification
and permission steps, sound and vibration, an optional "Read alerts aloud" setting (platform text-to-speech, off by default), and a browser demo that only plays Practice mode. [PLAN.md](PLAN.md) is the plan,
[docs/spikes.md](docs/spikes.md) and [docs/android-background.md](docs/android-background.md) record what has been tried and on
what (an emulator is not a phone, and the notes say which).

## Try it

You need the [.NET 10 SDK](https://dotnet.microsoft.com/download) and nothing else for the desktop head. A local ntfy-compatible
server is included so no real server is needed:

```bash
dotnet run --project tools/FakeNtfy -- --port 8080          # a fake ntfy server
dotnet run --project src/AlertBuddy.Desktop -c Release      # the app
```

In First run, a grown-up picks a PIN, then enters the server `http://127.0.0.1:8080` and the topic `home-alerts`, and presses
**Test connection**. Then send the pretend alerts, a warning, an alarm and an all clear, five seconds apart:

```bash
curl -X POST "http://127.0.0.1:8080/_scenario/home-alerts?step=5"
```

Practice mode on Home needs no server at all: it plays a clearly labelled pretend alert.

For a real server, use its `https://` address (for example `https://ntfy.example.com/home-alerts`), and the sign-in your server
needs: none, a username and password, or an access token. The password and token are kept in the platform's secure store
(the Android Keystore, the iOS Keychain, the desktop credential store). Where a platform has none, as on a Linux desktop with no
keyring, they are held for the session only and never written to a file.

## Principles

- No accounts, no cloud service, no analytics, no ads, no third-party SDKs. The app talks only to the server a grown-up
  configured, and history stays on the device.
- It only receives. It does not publish alerts or administer the server.
- It never rewards a child for an alert happening, and it never uses fire imagery.

## Building

Alert Buddy is a C# / .NET 10 app on [Majorsilence.Forms](https://github.com/majorsilence/Majorsilence.Forms), with Android,
desktop and browser heads.

```bash
dotnet build src/AlertBuddy.Desktop -c Release
dotnet test tests/AlertBuddy.Shared.Tests -c Release
dotnet run --project tools/Harness -c Release   # the whole alert lifecycle through view models only
```

The Android head needs the `android` workload and JDK 21; see [CLAUDE.md](CLAUDE.md) for the exact steps.

## Licence

MIT. See [LICENSE](LICENSE).
