# Alert Buddy

A friendly alert receiver for a family. It connects to a self-hosted [ntfy](https://ntfy.sh) server and turns "something
needs a look" messages into a small character, a sound, and one clear instruction: **tell a grown-up**.

It is built for a child who likes alerts: the child is a helper and tells a grown-up, and the grown-up owns the response. The
first alerts it carries are home temperature warnings and alarms, but it understands any ntfy topic.

> **Alert Buddy is a helper. It does not replace smoke or heat alarms.** Keep another receiver, such as the official ntfy app,
> running for anything that matters.

## Status

Early: the project has its structure, its spikes and its first framework work, and no features yet. See [PLAN.md](PLAN.md)
for what it will be, and [docs/spikes.md](docs/spikes.md) for what has been learned so far.

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
```

The Android head needs the `android` workload and JDK 21; see [CLAUDE.md](CLAUDE.md) for the exact steps.

## Licence

MIT. See [LICENSE](LICENSE).
