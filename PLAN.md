# Alert Buddy: implementation plan

Status: planning document, written 2026-09-26. Owner: Peter Gill. Written for the session that will
build the app. Read section 0 first.

## 0. How to use this document

- Sections 1 to 4 say what to build and why. Sections 5 to 9 say how. Section 12 is the order of work.
- Anything marked **(verify)** was reasoned from documentation or memory, not tested. Spike it before
  building on it (section 12, milestone 0).
- Anything marked **(decided)** is settled. Do not reopen it without a reason.
- **Framework first.** Alert Buddy is a fun app for a child and also a real-world exercise of
  Majorsilence.Forms, which is owned by the same person who owns this repo. If the app needs something a UI
  framework should provide (audio, rounded rectangles, lifecycle events, and so on), build it in the
  framework, get it released, and move this app to the release. Do not keep lasting workarounds. Section 11
  has the policy and the register of framework work; every milestone in section 12 lists the framework items
  it needs.
- This is a public repository. Section 14 lists what must never appear in it.

## 1. What Alert Buddy is

A friendly alert receiver for a family. It connects to a self-hosted [ntfy](https://ntfy.sh) server and
turns "something needs a look" messages into a small character, a sound, and one clear instruction: tell
a grown-up. The first alerts it will carry are home temperature warnings and alarms, but it understands
any ntfy topic.

**Who it is for**
- A 7-year-old who likes alerts and will use it on a phone or tablet. The child is a helper. The child
  tells a grown-up. The child is never the responder.
- The grown-ups who own the house and the server. They set the app up, and they own the response.

**Targets, in order**
1. Android phones and tablets. Published packages already ship Android assets.
2. iPad and iPhone. Needs upstream work first (section 11) and has real platform limits (section 6).
3. Desktop (Windows, macOS, Linux): a bedside or desk display.
4. Web (WebAssembly): a demo and simulator, not a live receiver.

**Non-goals**
- It is not a life-safety device and must say so. It does not replace smoke or heat alarms.
- No accounts, no cloud service, no analytics, no ads, no third-party SDKs.
- It only receives. It does not publish alerts or administer the server.

## 2. Decisions

| Topic | Decision |
|---|---|
| UI framework | Majorsilence.Forms 26.3.0 (latest published), pinned. WinForms programming model drawn with SkiaSharp. (decided) |
| Hosts | Avalonia 12.x backend for Android, iOS, desktop and browser. Template default is Avalonia 12.1.1. (decided) |
| Language / runtime | C# on .NET 10. iOS is full AOT and trimmed, so no reflection-based JSON (use `System.Text.Json` source generation) and be careful with reflective data binding (section 3). (decided) |
| Architecture | MVVM with CommunityToolkit.Mvvm 8.4.2, following the framework's own sample (section 3, section 7.5). View models live in a UI-free project so they are unit-tested without any UI. (decided) |
| View wiring | Small trim-safe binder helpers over `PropertyChanged` and `ICommand` by default. Declarative `DataBindings` only where spike S9 proves it under trimming. (decided, revisit after S9) |
| Server protocol | ntfy HTTP streaming (`/{topic}/json`). Not MQTT, not FCM. (decided) |
| Look | Custom kid-friendly design system: CSS theme for standard controls plus custom-painted controls (section 8). (decided) |
| Alert semantics | Priority 5 is an alarm, priority 4 is a warning, priority 3 or lower is calm (including "all clear"). (decided) |
| Distribution v1 | Sideloaded Android APK from GitHub Releases. Play Store later, and it changes some Android rules (section 6). |
| Licence | MIT (already in the repo). Fonts under OFL. Sounds generated in-repo (section 8.8). |
| Reward model | The app never rewards the child for an alert happening (section 4.1). (decided) |
| Framework policy | Framework first: missing capabilities are built in Majorsilence.Forms and adopted by release, never worked around for good (section 11). Exact version pins, bumped per release. (decided) |

## 3. What the framework gives you, and what it does not

Read from the Majorsilence.Forms repo (`docs/theming.md`, `docs/backends.md`, `COMPATIBILITY_MATRIX.md`,
samples) and the published NuGet packages. These facts drive the design.

**It is the WinForms programming model, and MVVM works on top of it.** `Form`, `Control`, `Panel`, `Label`,
`TextBox`, events, `Dock` and `Anchor` layout, all drawn with SkiaSharp. It is not XAML, but view models are
supported through **CommunityToolkit.Mvvm 8.4.2** (source-generated `ObservableObject`,
`[ObservableProperty]` and `[RelayCommand]`). The worked example is in the framework repo:
`samples/ControlGallery/ViewModels/CounterViewModel.cs` and
`samples/ControlGallery/Panels/CommunityToolkitMvvmPanel.cs`. Read both before designing any view. What that
example, the docs and the source together say:

- **There are two ways to connect a view to a view model.**
  (a) The sample's way: subscribe to `PropertyChanged` and push values into control properties, and forward
  `Click` into the generated command. It uses no reflection, so it is trim and AOT safe.
  (b) The framework's own WinForms data binding, which is **live**: `control.DataBindings.Add ("Text", vm,
  "Name")` moves values both ways (`INotifyPropertyChanged` for source to control, the `<Property>Changed`
  convention for write-back), with `Format`, `Parse`, `DataSourceUpdateMode` and a `DataContext` that children
  inherit. See the `Binding` row in `COMPATIBILITY_MATRIX.md`, `docs/behaviour-gap/binding.md` and
  `tests/Majorsilence.Forms.Tests/BindingLiveRuntimeTests.cs`. **The sample's own comment, and one in
  `Directory.Packages.props`, still say `DataBindings` is a stub. That is stale** (upstream item F4).
- **Binding is reflective.** It resolves members by name at runtime, and the source says "a trimmed app has to
  root the types it binds". iOS builds are AOT and trimmed, and Android release builds can be trimmed, so
  declarative bindings are unproven on the mobile heads. Default to (a), wrapped in small helpers (section
  7.5), and use (b) only where spike S9 proves it under trimming.
- **`ButtonBase.Command` exists but does not fit the toolkit.** It is typed to the framework's own
  `ICommandExecutor` (`Execute ()` and `CommandCanExecuteChanged`), not to `System.Windows.Input.ICommand`.
  `CommandParameter` is stored but never passed, and `CanExecute` does not enable or disable the button. A
  toolkit `RelayCommand` is an `ICommand`, so use the adapter in section 7.5, or fix it upstream (F3).

Start from `dotnet new majorsilenceforms` (section 12, milestone 0).

**CSS theming is a strict, small subset.** Tokens in `:root`, then rules per control type. It supports
colours, `border`, `border-radius`, `border-width`, and fonts (`font-family`, `font-size`,
`font-weight`, `font-style`). It does **not** support gradients, images, shadows, margin, padding,
classes, ids, descendant selectors, media queries or `:disabled`. The only pseudo-class is `:hover`, on
`Button`, `LinkLabel` and `TrackBar`. Anything unsupported is a reported error, never silently ignored.
Consequence: CSS themes the plain controls (settings screens). **The fun visuals must be custom-painted
controls.** That fits this brief.

**Custom controls are `Control` subclasses that override `OnPaint`.** The `Graphics` object is the
GDI+-style API backed by Skia. Confirmed available: `FillEllipse`, `FillPie`, `DrawArc`, `FillPath` and
`DrawPath` with `GraphicsPath`, `LinearGradientBrush`, `PathGradientBrush`, `SolidBrush`, `Matrix`,
`RotateTransform`, `TranslateTransform`, `ScaleTransform`, `Save`/`Restore`, `SetClip`, `DrawString`,
`MeasureString`, `DrawImage`. **`Graphics` has no `FillRoundedRectangle` yet**, although the framework already
draws rounded corners for `border-radius` (`SkiaExtensions.FillRoundedRectangle` and `DrawRoundedRectangle`
on `SKCanvas`); the raw `SKCanvas` is internal to `Graphics`, so app code cannot reach those. Adding rounded
rectangles to `Graphics` is framework item F1. The canonical animated example is
`samples/ControlGallery/Panels/GameOfLifePanel.cs` (a `Timer` plus `Invalidate ()`). `Majorsilence.Forms.Timer`
wraps Avalonia's `DispatcherTimer`, so it is not frame-aligned (F5), and there are no animation or easing
helpers (F6).

**Mobile is real but young.** Android, iOS and browser run through one `MajorsilenceFormsSingleViewHost`
(no window manager). Entry points: `Application.RunAndroid (() => new MainForm ())`, `RunIOS`,
`RunBrowserAsync`. Working on Android hardware: tap hit-testing, render scaling, touch scroll and flick.
Implemented but **only unit-tested, not exercised on a device**: soft keyboard, safe-area insets
(`Form.SafeAreaPadding`), rotation. **iOS has never run on a simulator or device.**

**Touch input.** Standard WinForms mouse events (touch arrives as mouse) plus `LongPress`, `Pinch`,
`Swipe` and `ScrollGesture` on `Control`. No multi-touch beyond pinch.

**Fonts.** `PrivateFontCollection` (in `Majorsilence.Forms.Drawing`) registers fonts at runtime, and the
typeface cache honours it. Whether CSS `font-family` resolves a private font on every head is **(verify)** (framework item F17).

**Images.** Skia decodes PNG, JPEG, GIF (animated, via `ImageAnimator`) and other formats. There is no SVG
renderer and no Lottie. Draw shapes in code.

**Audio is silent on mobile.** The WinForms-shaped `SoundPlayer` and `SystemSounds` exist and work on desktop,
but they play through `Media/NativeAudio.cs`, which spawns OS utilities (`afplay`, `paplay`, PowerShell) and
does nothing on Android and iOS. Its own comment says the seam for a native path is that class, and that the
core deliberately avoids per-platform SDKs. There is also no volume, no audio usage (an alarm must play on the
alarm stream) and no overlapping playback. Framework items F8 and F9.

**Lifecycle and back are not surfaced.** `Application` raises `ApplicationExit`, `OnExit` and
`ThreadException` only. `Form.Activated` and `Deactivate` do not fire when a mobile app goes to the
background, and the backend has no back-button handler. Avalonia does provide the hooks (the
`Avalonia.Android` assembly contains `IActivatableLifetime`, `Deactivated` and `BackRequested`
**(verify)**), so this is wiring, not research. Framework items F10 and F11.

**Other things a mobile app expects and the framework lacks.** No haptics, no keep-screen-awake, no
text-to-speech and no secure storage (F13, F12, F15, F16). `NotifyIcon` exists, but its own documentation says
`ShowBalloonTip` is a no-op (F14). No animation or easing helpers and no reduced-motion preference (F6, F7).

**Threading.** `Control.BeginInvoke (Action)` and `Control.Invoke (Action)` exist. Marshal all network
events to the UI thread with them **(verify on a single-view host)**.

**Published packages (NuGet, 26.3.0).** `Majorsilence.Forms.Avalonia` ships `net10.0`,
`net10.0-android36.0`, `net10.0-browser1.0` and `net8.0`. **There is no iOS asset.** The template README
already warns about this. iPad and iPhone are blocked on an upstream release.

**Automation.** The framework has a `Headless` backend that renders any form to a PNG with no display
(`HeadlessRenderer.CapturePng`), and a backend-neutral **automation tree** (id, name, role, value, state,
bounds) with an in-process test session, page objects and waits, a W3C WebDriver server
(`Majorsilence.Forms.WebDriver`), golden-image comparison, and an MCP server (`tools/Majorsilence.Forms.Mcp`)
for AI agents. `samples/AutomationTarget` shows the app-side hook. Use all of it for this app's UI tests
(section 10); it is part of what the app exercises. Whether custom-painted controls appear in the tree with
meaningful state is unconfirmed (F19).

## 4. Product

### 4.1 Principles

1. **Reward the response, not the emergency.** There are no points, badges or collectibles for alerts
   happening. A child who likes alerts must never learn that a hot room is a prize. The buddy can say
   thank you. The Alert Book is a history, not a collection. Practice mode earns nothing.
2. **Never scary.** Calm voice, short words, name the room, say what to do. No flames or fire imagery.
   Heat is shown as a thermometer and a sweating buddy.
3. **Grown-up first.** Every alarm ends in "Tell a grown-up" and a grown-up acknowledgement.
4. **Kid-proof.** Setup, credentials and destructive actions sit behind a grown-up gate. Nothing exits the
   app by accident.
5. **Honest about limits.** Connection state is always visible. If the app cannot listen in the
   background, it says so plainly (section 6).
6. **Private by default.** The app talks only to the server the grown-up configured. No telemetry.
   History stays on the device.
7. **A second receiver for anything that matters.** The README and first-run text tell grown-ups to keep
   another receiver (for example the official ntfy app) running for alarms.

### 4.2 Alert model

- `AlertLevel`: `Calm`, `Warning`, `Alarm`.
- `Source`: the thing alerting (a room, a machine). Taken from the message title (section 5.4).
- `Alert`: id, source, level, title, body, time, optional parsed temperature, `Status`.
- `Status`: `Active`, `Acknowledged` (the child said "I told a grown-up"), `Handled` (a grown-up said
  "Got it"), `Resolved`.

Rules:
- A Warning or Alarm makes an `Active` alert for its source.
- A Calm message for a source resolves that source's active alerts. This is the "all clear".
- An Alarm for a source that has an active Warning upgrades it (one card, not two).
- The same alarm repeated by the server (servers repeat while it stays hot) re-triggers the sound unless
  the alert was acknowledged within the silence window (default 10 minutes, grown-up configurable).
- Acknowledging silences sound only. The card stays until the source resolves.
- On startup and after a reconnect, replay recent history so "what is active right now" is correct
  (section 5.3). Replayed messages never make a sound.

### 4.3 Screens and flows

| Screen | Purpose | Who |
|---|---|---|
| Home | The buddy, one status sentence, active alerts, two big buttons | Everyone |
| Alarm takeover | Full screen "Tell a grown-up now", spinning beacon, one big button | Everyone |
| Alert detail | One alert: source, level, temperature, when, what to do | Everyone |
| Alert book | Past alerts, newest first. Empty state is an invitation | Everyone |
| Practice | A short, clearly labelled pretend alert so a child knows what to expect | Everyone |
| First run | Buddy name, then grown-up setup (server, topic, sign-in, permissions) | Grown-up |
| Grown-up settings | Everything below, behind the gate | Grown-up |

Grown-up settings: server URL, topic, sign-in (none, username and password, or token), Test connection,
notification permission wizard, sounds and volume, night policy, alert interpretation (section 5.4), buddy
name and colour, Day/Night/Auto look, reduced motion, PIN, clear history, About (version, licences, one
paragraph of privacy).

**The grown-up gate.** A `HoldButton` (press and hold about 2 seconds, a ring fills), then a PIN pad. The
PIN is set during first run and stored hashed. A 4-digit PIN is fine: it is a child gate, not security.

**Alarm flow.** Alarm arrives, then the takeover screen, siren, vibration. Big button: "I told a
grown-up". After it, the beacon calms to a slow amber pulse and the screen says "Thank you. A grown-up is
on it." A small `HoldButton`, "Got it", is for the grown-up. Both only change local state. The alert
stays until the server sends the all clear.

**Night policy** (grown-up setting, default shown): warnings are silent between 20:00 and 07:00 (notification only, no sound); alarms always sound. Alarms are never muted by quiet hours.

### 4.4 Demo and practice

`PracticeAlertSource` produces scripted alerts with fictional sources. It has two uses: Practice mode for
the child (a banner says "Practice", sound is short and quieter, nothing is saved to the Alert Book) and
the whole development loop and CI, so the app needs no real server to run.

## 5. ntfy client

### 5.1 Contract the app consumes

A generic ntfy message: `id`, `time`, `event`, `topic`, `title`, `message`, `priority` (1 to 5, omitted
when 3), `tags` (list). The app understands:

| Field | Meaning |
|---|---|
| priority 5 | Alarm |
| priority 4 | Warning |
| priority 3 or absent, or 1 to 2 | Calm (also used as "all clear") |
| `title` | Shown to the user. A leading emoji is stripped (section 5.4). Text before the first `: ` is the source |
| `message` | Body. A number followed by `C`, degrees, or `°C` is parsed as a temperature when present |
| `tags` | Optional. `warning` and `critical` are treated as hints, not required |

### 5.2 Streaming

- `GET {base}/{topic}/json` returns newline-delimited JSON, one event per line, held open. Events:
  `open`, `keepalive` (about every 45 seconds by default), `message`. Ignore all others.
- Headers: `Authorization: Basic base64(user:password)` or `Authorization: Bearer <token>`.
- Use `HttpClient` with an infinite timeout, `ResponseHeadersRead`, and a cancellation token. Read lines
  with a bounded buffer (reject lines over 64 KB).
- Resume with `?since=<last message id>`. Persist the last id after every message.
- **Watchdog:** if no line (including keepalive) arrives for 110 seconds, drop and reconnect.
- **Backoff:** 1, 2, 4, 8, 16, 32, then 60 seconds, plus or minus 20% jitter. Reset after 30 seconds of
  healthy streaming.
- **Errors:** 401 or 403 means `AuthFailed`: stop the fast loop, retry every 5 minutes, and show it.
  404 means "topic not found". 429 honours `Retry-After`. TLS errors say what failed.
- **State:** `Connecting`, `Live`, `Reconnecting`, `Offline`, `AuthFailed`, `Misconfigured`. Each has a
  sentence in the buddy's voice (section 8.10).
- **Security:** `https` by default. Plain `http` only for loopback, private (RFC 1918) addresses and
  `.local` names, shown with an "unencrypted" badge. Credentials live in `ISecretStore`. Never log
  secrets. Redact `Authorization` everywhere.
- **Web (WebAssembly):** browser streaming needs `SetBrowserResponseStreamingEnabled (true)`, CORS, and an
  `https` server. A page served over `https` cannot call an `http` LAN address. That is why web is a
  demo target.

### 5.3 History and replay

On first connect and after a long gap, request `?since=12h` (the server's default cache window; make it a
setting). Feed those messages through the same interpreter but tagged `Backlog`: they build the Alert
Book and the "active right now" set, and they **never** play a sound or raise a notification. Keep at most
the last 200 alerts. Store as a bounded JSON file with atomic writes (write temp, then rename). No
database in v1.

### 5.4 Interpretation rules (configurable, sensible defaults)

- Strip leading emoji and whitespace from the title (colour emoji may render as boxes on some heads, and
  the app draws its own icons anyway). **(verify emoji rendering, milestone 0)**
- `source` = text before the first `: `, else the whole title.
- Temperature = first number followed by an optional space and `C`, `°C` or `degrees` in the body.
- A message whose title matches `(?i)\btest\b` is a Test: play a friendly "it works" cheer, do not create
  an alert.
- All of the above are settings with a reset button, so the app works with any server's conventions.

## 6. Platform delivery

The hard part of any alert app is delivery when the app is not open. Be honest per platform.

### 6.1 Matrix

| Platform | App open | App in background / screen off | After reboot |
|---|---|---|---|
| Android | Live stream | Foreground service holds the stream, shows a quiet notification | Boot receiver restarts the service |
| iOS / iPadOS | Live stream, optional keep-awake | **No persistent connection possible.** Best effort only (6.3) | Not applicable |
| Desktop | Live stream | Runs as a normal app or tray app | User starts it |
| Web | Live stream (demo) | Nothing | Nothing |

### 6.2 Android (primary)

**(verify all of this against current Android documentation when implementing; these rules change.)**

- One `Service` owns the `NtfySubscription`. The Activity and the service live in the same process, so
  the UI subscribes to an in-process `AlertHub`. No IPC.
- Foreground service with a declared type. Candidates: `specialUse` (needs a manifest subtype property
  explaining the use; fine for sideloaded builds, needs Play review), or `dataSync` (Android 15 limits it
  to about 6 hours per day, which is wrong for an always-on listener). Choose after reading the current
  rules, and record the choice in `docs/android-background.md`.
- Permissions: `POST_NOTIFICATIONS` (runtime, API 33 and up), `FOREGROUND_SERVICE` plus the type
  permission, `RECEIVE_BOOT_COMPLETED`, optionally `USE_FULL_SCREEN_INTENT` for the alarm takeover (on
  Android 14 and later this is restricted to calling and alarm apps on Play; sideloaded users can allow it
  in settings), and battery-optimisation exemption guidance.
- Notification channels:

| Channel | Importance | Sound | Behaviour |
|---|---|---|---|
| Alarm | High | Siren on the alarm audio stream (`USAGE_ALARM`), so it plays even if media volume is low | Full-screen intent if allowed, vibrates, ongoing until acknowledged or resolved. Bypass Do Not Disturb only if the grown-up grants notification-policy access |
| Warning | High | Friendly two-note cue | Heads-up |
| Calm | Low | None | Auto-dismiss after 10 minutes |
| Listening | Low | None | The foreground service's required "Pip is listening" notification |

Channels, the permission request and the alarm notification go through the framework's `LocalNotifications`
(F14). The foreground service's own ongoing notification is created by the service in the Android head,
because `startForeground` needs a native `Notification`.

- Onboarding wizard for grown-ups, in this order: allow notifications, allow the alarm full-screen view,
  set the alarm volume (with a test sound), exempt from battery optimisation (link to the OEM-specific
  steps; some vendors kill background services aggressively), allow Do Not Disturb override. Each step
  shows whether it worked and has a "Later" button.
- The app must show a persistent, honest banner on Home when it cannot listen (permission missing,
  battery restricted, no network).
- A future upgrade path if OEM battery managers still kill the service: UnifiedPush (a distributor app
  such as ntfy receives the push, then wakes ours). Not in v1.

### 6.3 iOS and iPadOS

**An iOS app cannot hold a long-lived connection in the background.** Push on iOS means APNs. ntfy's own
relay to APNs only serves the official ntfy iOS app (it is bound to that app's identity), so a custom app
cannot use it. Therefore for v1:

- **Foreground live mode.** While the app is open the stream is live. A "Bedside mode" keeps the screen
  awake (`UIApplication.IdleTimerDisabled`) and dims it: an iPad on a charging stand as a status display.
- **Local notifications** for messages that arrive while the app is active or briefly backgrounded.
- **Best-effort background refresh** on wake: catch up with `?since=<last id>`. Do not promise timing.
- The first-run screen on iOS says, in plain words: keep the app open for live alerts.
- Later, real background alerts need: an Apple Developer Program account, the Push Notifications
  capability, an APNs relay service that receives the server's alerts and pushes them (a separate
  project, not part of this repo), and Time Sensitive notifications (available to all developers).
  Critical Alerts need an Apple entitlement approval and are unlikely for a hobby app.
- Building for a device needs a Mac and the `ios` workload. Free provisioning expires after 7 days.
- **Blocked by** the missing `net10.0-ios` asset in the published packages (section 11).

### 6.4 Sound and haptics

Provided by the framework, not by each head (F8, F9, F13). The app calls the framework's `AudioPlayer` (the
`Alarm` usage for the siren loop, `Effect` for cues), `SoundPlayer` and `SystemSounds` for simple cases, and
`Haptics`. Underneath, the framework's Android row uses `SoundPool` and `MediaPlayer` with alarm audio
attributes, and its iOS row uses `AVAudioPlayer` and an `AVAudioSession` in the playback category. `ViewModels`
sees these only through the narrow `ISoundPlayer` and `IHaptics` interfaces so tests can fake them; `Shared`
implements them as thin adapters and contains no platform code. Both report `IsSupported`, and the UI stays
honest when it is false. Until a framework release provides them, a minimal Android implementation may live in
the Android head as a `TEMP-SHIM (F8)` and is deleted on the bump.

### 6.5 Lifecycle and back button

Provided by the framework (F10, F11): `Application.Suspended` and `Resumed`, `Form.Activated` and
`Deactivate` firing on single-view hosts, and a cancellable `Form.BackRequested`. `Shared` adapts them to the
narrow `ILifecycle` interface the view models use. On Android the back button closes a sheet or steps back
one screen before it ever exits the app. Until released, the Android head may forward `OnResume`, `OnPause`
and `OnBackPressed` itself as a `TEMP-SHIM (F10, F11)`.

### 6.6 Desktop and web

Desktop uses the Avalonia head. In-app alerts only in v1; OS toasts later (a `INotifier` per desktop OS).
Web ships a Practice-mode-only demo, hosted on GitHub Pages, so people can see the app without a server.

## 7. Architecture

### 7.1 Solution layout

```
alert-buddy/
  README.md  LICENSE  PLAN.md  CLAUDE.md          (CLAUDE.md is created in milestone 0)
  AlertBuddy.slnx
  Directory.Build.props  Directory.Packages.props  (central package versions, pinned)
  src/
    AlertBuddy.Core/       net10.0, no UI. ntfy client, alert model, interpreter, hub, storage abstractions
    AlertBuddy.ViewModels/ net10.0, CommunityToolkit.Mvvm only. One view model per screen, navigation, commands
    AlertBuddy.Shared/     Majorsilence.Forms UI: views, custom controls, binder helpers, theme, fonts, assets,
                           platform interfaces
    AlertBuddy.Desktop/    Avalonia desktop head (WinExe)
    AlertBuddy.Android/    net10.0-android head: service, notifications, sound, secret store, permissions
    AlertBuddy.iOS/        net10.0-ios head (blocked until upstream ships the asset)
    AlertBuddy.Wasm/       browser head
  tests/
    AlertBuddy.Core.Tests/       xunit: parser, interpreter, backoff, watchdog, replay, hub
    AlertBuddy.ViewModels.Tests/ xunit: every screen's behaviour with fake services and a synchronous
                                 dispatcher. No UI, no framework
    AlertBuddy.Shared.Tests/     xunit on the Headless backend: golden renders, layout, binder wiring
  tools/
    FakeNtfy/              tiny local server that streams scripted NDJSON and accepts publishes
    SoundSynth/            generates the WAV/OGG cues (section 8.8)
  docs/  design.md  ntfy-contract.md  android-background.md  spikes.md
```

`Core` has no dependency on Majorsilence.Forms, Avalonia, CommunityToolkit or any platform API.
`ViewModels` depends on `Core` and CommunityToolkit.Mvvm only, never on Majorsilence.Forms or a platform API.
`Shared` depends on `ViewModels`, `Core` and Majorsilence.Forms. Heads depend on all three plus their
platform packages. The dependency arrows only point one way, and that is what keeps the logic testable.

### 7.2 Key types (sketch, see Appendix A for interfaces)

- `NtfySubscription`: `IAsyncEnumerable<NtfyEvent>` with reconnect, watchdog and backoff inside.
- `AlertInterpreter`: `NtfyMessage` to `AlertEvent`, using `InterpretationSettings`.
- `AlertStore`: the active set and bounded history, with replay support.
- `AlertHub`: in-process pub/sub (`AlertRaised`, `AlertResolved`, `ConnectionChanged`). The Android service
  publishes; the UI subscribes. It is a plain C# class, no framework.
- `IClock`, `ISettingsStore`, `ISecretStore`, `ISoundPlayer`, `IHaptics`, `IKeepAwake`, `IAlertNotifier`,
  `ILifecycle`, `IBackgroundListener`: narrow interfaces the view models depend on, faked in tests.
  `ISoundPlayer`, `IHaptics`, `IKeepAwake`, `IAlertNotifier` and `ILifecycle` are thin adapters in `Shared`
  over framework APIs (F8 to F14). Only `IBackgroundListener` (the Android foreground service) and, until F16
  ships, `ISecretStore` need per-head code.
- View models, navigation and the wiring helpers: section 7.5.
- Single UI form `MainForm` hosting a `PageHost` custom control that swaps page panels (with a slide
  transition). Do not open extra top-level forms on mobile: single-view hosts render them as canvas
  overlays.

### 7.3 Layout strategy

Design in logical pixels (the framework scales; Android reports a render scaling near 2.6). Three widths:
compact (under 600), medium (600 to 900), expanded (over 900 or landscape). `AdaptiveLayout` re-flows on
`Resize`. Compact is one column. Expanded is two panes (buddy left, list right). Respect
`Form.SafeAreaPadding`. Minimum touch target 64 for primary buttons, 48 for secondary.

### 7.4 Settings and secrets

`AppSettings` is a JSON file in the app data directory. Secrets (password, token) go through `ISecretStore`:
Android Keystore-backed encrypted storage, iOS Keychain, desktop OS credential store, web `localStorage`
with a warning. A plain-file fallback exists for tests only and must be impossible to select in a release
build.

### 7.5 MVVM conventions

Follows the framework's own example (`CounterViewModel` and `CommunityToolkitMvvmPanel`, section 3), with
the wiring pulled into reusable helpers so no view repeats it.

- **View models** derive from `ObservableObject`, use `[ObservableProperty]` for state and `[RelayCommand]`
  for actions (an async command for anything that awaits). They are the only place behaviour lives. A view
  holds layout and wiring, nothing else. Add `CommunityToolkit.Mvvm 8.4.2` (the version the framework pins)
  to `Directory.Packages.props`.
- **One view model per screen.**

| View model | Owns | Commands |
|---|---|---|
| `MainViewModel` | Connection state, active alerts, beacon level, the status sentence | `OpenBook`, `StartPractice` |
| `AlertItemViewModel` | One ticket: source, level, time ago, temperature | `Open` |
| `AlarmViewModel` | The takeover: which alarm, acknowledged or not | `ToldGrownUp`, `GotIt` |
| `AlertDetailViewModel`, `AlertBookViewModel` | One alert, the history | `Back`, `ClearHistory` (gated) |
| `PracticeViewModel` | The scripted run and its banner | `Start`, `Stop` |
| `FirstRunViewModel` | The steps (a real sequence), buddy name, PIN | `Next`, `Back` |
| `SettingsViewModel` | Server, sign-in, sounds, night policy, interpretation | `TestConnection` (async), `Save` |
| `GateViewModel` | PIN entry and the hold-to-unlock state | `Unlock`, `Cancel` |

- **Navigation is view-model first.** `INavigator.GoTo<TViewModel> ()` and `GoBack ()`. `PageHost` holds a
  registry from view model type to view factory and swaps pages when the current view model changes. The
  Android back button calls `GoBack ()` (through `ILifecycle`, section 6.5).
- **Threading.** Core and the Android service raise events on background threads. View models receive them
  through `IUiDispatcher.Post (Action)`, implemented in `Shared` with `Control.BeginInvoke (Action)` and
  faked with a synchronous version in tests. A view model never touches a control.
- **Wiring helpers** (`Observe`, `BindCommand`, `BindingScope`, `IUiDispatcher`) belong in the framework as
  the `Majorsilence.Forms.Mvvm` package (F2). Until it is released keep a minimal copy in `Shared/Binding`
  marked `TEMP-SHIM (F2)`. They use no reflection and no expression trees, so they are trim and AOT safe:
  - `vm.Observe (nameof (MainViewModel.StatusText), v => v.StatusText, text => bubble.Text = text)` pushes the
    current value now and again on every `PropertyChanged` for that name, on the UI thread, and returns an
    `IDisposable`.
  - `button.BindCommand (vm.ToldGrownUpCommand)` sets `Enabled` from `CanExecute`, follows
    `CanExecuteChanged`, and calls `Execute` on `Click`. For an async command it also disables the button
    while it runs. This is the adapter for the `ICommandExecutor` gap; once F3 lands it can set
    `ButtonBase.Command` directly instead.
  - A `BindingScope` collects a page's disposables and disposes them when the page is left, so no view leaks
    a subscription.
  - Two-way text: `textBox.TextChanged` writes to the view model, and the view model's `PropertyChanged`
    writes back, with a re-entrancy guard (the framework's own `Binding` needs the same one).
- **Custom controls expose plain properties** (`BeaconBuddy.Level`, `TicketCard.State`) that call
  `Invalidate ()` when they change. The helpers drive them like any other control, so the beacon is driven
  from a view model with no special cases and can be tested that way.
- **Messaging.** `AlertHub` (Core, no toolkit dependency) stays the source of truth for alert events. The
  toolkit's `WeakReferenceMessenger` is optional and only for view-model-to-view-model notices (for example
  "alarm acknowledged"). Never put alert state on it.
- **Trimming rule.** Anything that must survive trimming or iOS AOT is referenced by code, never by a string
  (`nameof` and lambdas). If spike S9 shows `DataBindings` works under trimming it may be used for simple text
  fields only, with the view model types rooted in a trimmer descriptor. Until then, do not use it in shipped
  code.

## 8. Visual design system

This section is the design brief. It is opinionated on purpose. Implement it, do not soften it into a
generic "colourful kids app".

### 8.1 Brief

- **Subject:** a household's alert receiver. Real content: rooms, temperatures, warnings, alarms, all
  clears, a grown-up to tell.
- **Audience:** a 7-year-old (reads simple words, loves sound and animation, likes to touch things that
  respond) and the grown-ups.
- **Primary job:** make an alert impossible to miss and easy to act on, without frightening a child.
- **The one memorable thing:** a beacon with a face. A friendly siren light that sleeps, watches, warms up,
  sounds off and cheers. Everything else stays quiet so the beacon can be the star.

### 8.2 Design plan (tokens)

**Colour.** Six colours plus one light. Colour carries meaning: the four state colours are used **only**
for state and never for decoration.

| Name | Hex | Role |
|---|---|---|
| Blueberry | `#232E7A` | Night ground. The house at night |
| Paper | `#F6F2FF` | Day ground, text on Blueberry. Lavender-tinted white, not cream |
| Grape ink | `#2B1B4D` | Text and outlines on light surfaces. A tinted dark, not black |
| Mint | `#6FD3A0` | Calm, all clear |
| Tangerine | `#FF8A3D` | Warning |
| Cherry | `#C8202E` | Alarm |
| Butter | `#FFD84D` | The lamp: beacon light, focus ring, text selection. Not a state |

Measured contrast (WCAG): Grape on Paper 13.95, Paper on Blueberry 11.9 (Night uses `#232E7A`, see
below), Grape on Butter 11.1, Grape on Mint 8.4, Grape on Tangerine 6.6, Paper on Cherry 5.15.
**Cherry must never sit directly on Blueberry** (1.6): an alarm shape on the Night ground always has a
Paper outline ring. State is never colour alone: every state also has a face, an icon shape and words.

**Type.** Two families, clearly different, both OFL. **(verify licences when bundling)**
- **Grandstander** (display): status sentences, buttons, headings. Weights 600 to 800. It has a hand-made
  friendliness that reads as children's book, not tech.
- **Atkinson Hyperlegible Next** (text): everything else. Designed for legibility, good for early readers.
- Scale: display 40 / 30 / 22, body 18, small never below 16. Line height 1.25 for display, 1.45 for body.
- Sentence case everywhere. No all-caps labels, no tracked-out eyebrows, no middle-dot separators.

**Shape language.** Cut paper and stickers. Every shape has a 3 px Grape outline (Paper on Night). A **hard
offset shadow** (4 px down and right, solid Grape, no blur) sits under raised things. It collapses on press.
Outlines wobble by up to 1 px and corner radii differ by corner (10 to 18), both driven by a seed stored
with the control so a shape never changes between frames. No soft shadows, no gradient washes, no
identical-card grids.

**Layout.** Centre the hero (beacon and its sentence: it is symmetrical and the eye should land there).
Left-align everything that is read (cards, settings). Never justify. Primary actions sit in the bottom
third, in the thumb zone.

**Principles.** Colour means temperature. The beacon is the only thing that moves on its own. Motion only
answers a touch or announces an alert. Words are short and say what to do.

### 8.3 Wireframes

Home, calm (phone, compact):

```
+----------------------------------+
| [gear]                           |  hold about 2 seconds for grown-up settings
|                                  |
|              .--""--.            |
|             /  o  o  \           |  Pip, the beacon: dome, eyes that follow a touch
|            |    __    |          |
|          __|__________|__        |
|         |________________|       |
|                                  |
|        All quiet.                |  SpeechBubble, centred, wobbly outline
|        Pip is keeping watch.     |
|                                  |
|  Listening. Last heard 3 min ago |  connection line, small, left aligned
|                                  |
|                                  |
|   [ Alert book ]   [ Practice ]  |  ChunkyButtons in the thumb zone
+----------------------------------+
```

Home, warning (amber beam sweeps slowly, buddy sweating):

```
+----------------------------------+
| [gear]                           |
|          beacon (Tangerine)      |
|     The workshop is getting      |
|     warm.                        |
|  +----------------------------+  |
|  | Workshop            22 min |  |  TicketCard, Tangerine edge, hard shadow
|  | 41 degrees. Keep an eye on |  |
|  | it.                        |  |
|  +----------------------------+  |
|  | Music studio         4 min |  |
|  | 40 degrees.                |  |
|  +----------------------------+  |
|   [ Alert book ]   [ Practice ]  |
+----------------------------------+
```

Alarm takeover (full screen, beam spins, siren, vibration):

```
+----------------------------------+
|        beacon (Cherry, spinning) |
|                                  |
|     Tell a grown-up now.         |
|     The workshop is too hot.     |
|                                  |
|  +----------------------------+  |
|  |     I told a grown-up      |  |  ChunkyButton, Paper on Cherry, 72 high
|  +----------------------------+  |
|                                  |
|  Grown-ups: hold Got it          |  small HoldButton
+----------------------------------+
```

Tablet, landscape (expanded, two panes):

```
+---------------------------+--------------------------------+
|                           |  Right now                     |
|        beacon             |  +--------------------------+  |
|      "All quiet."         |  | Workshop          22 min |  |
|                           |  +--------------------------+  |
|  [Alert book] [Practice]  |  | Music studio       4 min |  |
+---------------------------+--------------------------------+
```

### 8.4 Review against defaults (what was changed and why)

Checked the plan against common generated-design defaults and changed these:
- A mascot on a rainbow sky was the first idea. Dropped: that is the default kids-app look. The beacon is
  chosen because it is what an alert actually looks like, and a child already knows fire-truck lights.
- Rounded cards with one radius and soft grey shadows for the list. Replaced with hard-shadow paper
  tickets, seeded irregular corners and a state-coloured edge.
- A cream page with a serif and a clay accent, and a near-black page with one acid accent. Both avoided:
  the ground is lavender-white or a mid-deep blue, and the accents are a four-colour temperature ladder.
- Emoji as illustration. Avoided: the app draws its own glyphs, and titles arrive with emoji that are
  stripped.
- Confetti and reward bursts. Dropped on safety grounds (section 4.1) and because restraint is the
  design: the beacon is the single memorable thing.
- Numbered steps, ALL-CAPS labels and a headline with one accent word: none used. The one true sequence
  (first run) is numbered because it is a sequence.

### 8.5 Custom controls

All in `AlertBuddy.Shared/Controls`, all painted with `Graphics`, all read `Theme.*` for surface and text
colours and `AlertPalette` (a static class of the state colours, Day and Night variants) for state.

| Control | What it is | Notes |
|---|---|---|
| `BeaconBuddy` | The hero. Dome, lamp, rotating beam, base, face | Section 8.6. `Level`, `Name`, `ReduceMotion` |
| `SpeechBubble` | The status sentence with a tail toward the buddy | Wobbly outline, hard shadow |
| `TicketCard` | One alert. Source, time, one sentence, temperature glyph | State-coloured left edge, seeded corners |
| `ThermoGlyph` | A chunky thermometer, filled to the parsed temperature, tick marks at the two levels | Drawn only when a temperature was parsed |
| `ChunkyButton` | Big pill button, press collapses the hard shadow, plays a boop | 64 high minimum, focus ring in Butter |
| `HoldButton` | Press and hold, a ring fills, then it fires. The grown-up gate and "Got it" | Cancels cleanly on release |
| `PinPad` | Large keys for the gate | 72 keys, no accidental double taps |
| `PageHost` | Swaps pages with a short slide | Reduced motion means a cut, not a slide |
| `PaperSurface` | Base class: outline, hard shadow, seeded wobble | Everything above derives from it |
| `HeadingLabel`, `BodyLabel` | `Label` subclasses that set Grandstander or Atkinson in code | CSS cannot target classes |

Build a dev-only `DesignGallery` screen that shows every control in every state at compact and expanded
width, and render it headlessly to PNG for review (section 10). Look at it. Do not ship the first render.

### 8.6 The beacon

States and behaviour. All animation is driven by one `Timer` (try 16 ms, measure, fall back to 33 ms).

| Level | Lamp | Beam | Face | Motion |
|---|---|---|---|---|
| Asleep (no connection or idle overnight) | Dim blue-grey | None | Eyes closed | Slow breathing, 4 s |
| Watching (connected, all quiet) | Butter, soft | None | Eyes open, blinks every 3 to 7 s, glances around | Eyes follow a touch |
| Warning | Tangerine | One sweep every 3 s | Eyes wider, one sweat drop | Gentle |
| Alarm | Cherry with a Paper ring | One turn per second, flashing | Eyes wide, mouth a small "o" | Small shake |
| All clear (4 s, then Watching) | Mint | Fades out | Happy squint | One soft pulse |

- Press: squish, a boop, eyes look at the touch. Long press: a silly wiggle.
- The beam is two translucent wedges rotated with `RotateTransform`. Radial glow with
  `PathGradientBrush`. **(verify frame time on a mid-range Android phone; if it cannot hold 30 fps, cut the
  glow to two flat rings.)**
- **Reduced motion** (system setting, or the grown-up's override): no rotation, no shake. The lamp pulses
  its colour slowly, the beam is a fixed wedge, the words carry the state.
- The buddy's name is set by the child in first run and used in the speech bubble.

### 8.7 CSS theme (standard controls)

Register two themes and switch in code (`Theme.RegisterThemeCssFromFile`, `Theme.ApplyTheme`). Custom
controls follow `Theme.ThemeChanged`. Draft (Appendix B has the full files). It uses only supported
tokens, selectors and properties.

```css
@theme "AlertBuddyDay" extends Light;
:root {
  --accent-color: #232E7A;
  --accent-color-2: #232E7A;
  --background-color: #F6F2FF;
  --foreground-color: #2B1B4D;
  --foreground-color-on-accent: #F6F2FF;
  --border-low-color: #2B1B4D;
  --text-selection-background-color: #FFD84D;
  --warning-highlight-color: #C8202E;
  --font-size: 18px;
  --ui-font: "Atkinson Hyperlegible Next", "Atkinson Hyperlegible", "Noto Sans", sans-serif;
}
Form    { background-color: #F6F2FF; color: #2B1B4D; font-size: 18px; }
TextBox { background-color: #FFFFFF; color: #2B1B4D; border: 3px solid #2B1B4D; border-radius: 14px; }
ScrollBar::thumb { background-color: #2B1B4D; border-radius: 6px; }
```

`AlertBuddyNight` extends `Dark` with Blueberry `#232E7A` ground and Paper text and outlines. Day, Night and
Auto (by system setting) are a grown-up choice; bedside mode forces Night and dims.

### 8.8 Sound

Original, generated in-repo by `tools/SoundSynth` so there is no licensing question and the child can help
tweak them. Cues: press boop (short, soft), warning bloop-bloop (two rising notes), alarm siren (a rising
"whoop", not harsh, looped), all clear chime (three ascending notes), test cheer, practice (quieter).
Peak level capped at about -6 dBFS. The alarm plays on the alarm audio stream and needs its own volume
step in onboarding.

### 8.9 Motion

The beacon is the only thing that moves unprompted. Everything else moves only in answer to a touch: a
button collapsing its shadow, a page sliding. Do not add hover effects, staggered entrances or decorative
motion. Respect reduced motion everywhere.

### 8.10 Copy (voice: plain, warm, short, active)

Rules: sentence case, plain verbs, name the room, say what to do, one exclamation mark at most in the whole
app. Errors say what happened and how to fix it. Empty states invite an action. An action keeps one name
through the flow.

| Where | Text |
|---|---|
| Home, calm | "All quiet. {buddy} is keeping watch." |
| Home, warning | "The {source} is getting warm." |
| Home, several warnings | "{n} places are getting warm." |
| Alarm takeover | "Tell a grown-up now. The {source} is too hot." |
| Button (child) | "I told a grown-up" |
| After tapping | "Thank you. A grown-up is on it." |
| Button (grown-up) | "Got it" |
| All clear | "All clear. The {source} is cool again." |
| Practice banner | "Practice. Nothing is really hot." |
| Connection, live | "Listening. Last heard {time} ago." |
| Connection, retrying | "Can't reach the house. Trying again." |
| Auth failed | "The server didn't accept the sign-in. Ask a grown-up to check it in settings." |
| Alert book, empty | "No alerts yet. When something needs a look, it shows up here." |
| Cannot listen (Android) | "{buddy} can't listen in the background. A grown-up can fix this in settings." |
| Safety note (About, first run) | "Alert Buddy is a helper. It does not replace smoke or heat alarms." |

Optional later: read alert text aloud with platform text-to-speech (`ISpeaker`), because a 7-year-old is
still learning to read. It is in scope for milestone 6.

### 8.11 Accessibility

State is never colour alone (face, glyph and words repeat it). Minimum text 16, body 18. Touch targets 48
minimum, 64 for primary. High contrast pairs only (section 8.2). Reduced motion honoured. Mobile screen
readers (TalkBack, VoiceOver) are **(verify)**: the framework's accessibility bridge is Windows-only today,
so expect a gap. Record what is and is not accessible in `docs/design.md`, and consider an upstream fix.

## 9. Screen specs

Each screen is a view over the view model of the same name (section 7.5). The view is layout and binder
wiring only; every rule below about what a screen does is implemented and tested in its view model.

- **Home.** The `BeaconBuddy`, one `SpeechBubble`, the connection line, active `TicketCard`s (newest
  first, scrollable, using the framework's `ScrollGesture`), two `ChunkyButton`s. The gear is a `HoldButton`
  with a 48 target, top-left.
- **Alarm takeover.** Replaces Home while any alarm is `Active` and not `Acknowledged`. After the child taps,
  return to Home with the calm amber state and the thank-you line.
- **Alert detail.** Source, level word, time ago and clock time, `ThermoGlyph` when a temperature exists,
  the body text, one line of what to do. Back returns to Home.
- **Alert book.** A list of `TicketCard`s grouped by day. Pull to scroll only. Grown-up gate to clear.
- **Practice.** A three-step scripted run (warning, alarm, all clear), 20 seconds total, banner on top,
  nothing stored. Steps are numbered because they are a sequence.
- **First run.** 1 Name your buddy. 2 Grown-up gate and PIN. 3 Server and sign-in with Test connection.
  4 Permissions (Android wizard, section 6.2). 5 A practice run.
- **Settings.** A grouped `TextBox`, `CheckBox` and `ChunkyButton` form, themed by the CSS, behind the gate.

## 10. Testing and CI

- **Core tests** (xunit, no UI): NDJSON parser (fixtures in Appendix C, all fictional), interpreter rules,
  replay, backoff with a fake clock, watchdog, `Retry-After`, auth-failure behaviour, dedup by id, bounded
  history and atomic write. Use an in-process fake server for streaming tests.
- **View model tests** (xunit, no UI, no framework): this is where most behaviour is proven. Drive each view
  model with fake services (`IClock`, `INavigator`, `ISoundPlayer`, `IAlertNotifier`, a synchronous
  `IUiDispatcher`) and assert: the state after each alert sequence (warning, upgrade to alarm, repeat,
  acknowledge, all clear, backlog replay with no sound), which `CanExecute` is true when, the status
  sentence for every state, gate and PIN behaviour, first-run step order, and that navigation and the
  Android back button reach the right screen. Property change notifications are asserted by name.
- **UI tests** (xunit on the framework's Headless backend): render each screen and every custom control in
  every state at several sizes and render scales to PNG. Compare against golden PNGs with a small
  tolerance. Also assert layout relationships (nothing overlaps the safe area, targets are at least 48) and
  that the binder helpers actually drive the views: change a view model property and assert the control
  changed, press a button and assert the command ran, and dispose a `BindingScope` and assert nothing is
  still subscribed. **Prove tests can fail:** before trusting a test, break the code it covers and watch it
  go red.
- **Framework automation** (the app is also an exercise of it): give every control a stable `Name` and an
  accessible name; write in-process UI tests with the framework's headless session and page objects (open the
  alarm takeover, press "I told a grown-up", assert the state), and expose the app's own automation endpoint in
  debug builds (`samples/AutomationTarget` is the pattern) so the WebDriver server and the MCP server can drive
  a running app, including on an emulator. Anything the tree cannot express about a custom control is a
  framework finding (F19).
- **Design review loop:** after each visual change, render `DesignGallery` and Home in all states and look
  at the images. Fix what looks generic or crowded.
- **Manual device matrix:** one real Android phone, one Android tablet (emulator is acceptable), one
  emulator at a small size, one iPad simulator when iOS is unblocked. Test rotation, keyboard, safe area,
  notification permission denied, airplane mode, server restart, Wi-Fi to mobile data.
- **CI (GitHub Actions):** Linux job builds `Core`, `Shared`, `Desktop`, `Wasm` and runs all tests. An
  Android job installs the `android` workload and builds the APK. An iOS job on `macos-latest` builds and
  smoke-launches in a simulator once unblocked. Copy the job patterns from the framework's
  `.github/workflows/dotnet.yml`. Release job attaches the signed APK. **The signing keystore and its
  password are repository secrets, never in the repo.**

## 11. Framework work (Majorsilence.Forms)

Alert Buddy has two purposes: a fun app for a child, and a real-world exercise of Majorsilence.Forms. So the
rule is **framework first**. If the app needs something a UI framework should provide, it is built in the
framework, released, and this app moves to the release. The app does not keep lasting workarounds. The
framework source is at `../Majorsilence.Forms`; its `CLAUDE.md` and `CONTRIBUTING.md` are the rules for
changes there.

### 11.1 Policy

1. **Missing capability means a framework issue, then framework work.** File it first in the framework's
   GitHub issue tracker (`majorsilence/Majorsilence.Forms`, section 11.6), then branch off `main` in
   `../Majorsilence.Forms`, implement, add tests, open a PR that closes the issue, get it released, then bump
   this repo's pin.
2. **Shims are temporary and tracked.** A shim is allowed only to unblock progress. Mark it
   `// TEMP-SHIM (F<id>)`, list it in `docs/framework-shims.md`, and delete it in the commit that adopts the
   release. CI fails if a shim names an item that `docs/framework-versions.md` records as released.
3. **Exact pins, one bump per release.** `Directory.Packages.props` pins an exact published version. Each
   bump is its own commit that says what it unlocks and which shims it removes, and adds a line to
   `docs/framework-versions.md` (version, date, what it unlocked, what it broke). CI never uses the framework
   checkout.
4. **Every finding becomes an issue.** File each bug, gap, awkward API or documentation error in the
   framework tracker the moment it is met (search first, so there is no duplicate), then record it in
   `docs/framework-findings.md` with the symptom, repro, issue link and status. The log is a deliverable and
   is reviewed at each milestone. This is the "exercise the framework" half of the project.
5. **API shape.** (a) Where WinForms, System.Drawing or System.Media has an equivalent, implement it with the
   WinForms names and semantics (`Graphics`, `SoundPlayer`, `SystemSounds`, `NotifyIcon.ShowBalloonTip`,
   `SystemInformation`). (b) Where WinForms has none but a mobile app needs it, add a small, clearly named
   extension in a `Majorsilence.Forms.*` namespace with an `IsSupported` flag, so an app can be honest instead
   of silently doing nothing (the framework's own rule: never a silent no-op). (c) Platform code sits behind the
   backend seam, the pattern `IWindowBackend.SetTextInputActive` already uses: a default no-op member in core
   and real implementations in the `net10.0-android` and `net10.0-ios` rows of `Majorsilence.Forms.Avalonia`.
   The core deliberately avoids per-platform SDKs (`NativeAudio`'s comment says so), so anything that needs
   them goes in those rows or in a separate package (11.5). (d) Desktop keeps the existing
   spawn-an-OS-utility approach unless the owner decides otherwise.
6. **The framework's conventions apply to every PR.** A space before every parameter list, XML docs on all
   public members, comments that say why, stubs that never throw `NotImplementedException` (and a recorded
   baseline entry for any new accepted no-op), its four test gates, the public-API gate
   (`tools/Majorsilence.Forms.ApiDiff`), regenerated docs when the CSS grammar changes, an updated
   `COMPATIBILITY_MATRIX.md`, and a new test that **fails without the fix**. **No `Co-Authored-By` or
   generated-with trailers in that repo's commits or PR text.** Do not commit or push on the owner's behalf:
   leave the working tree for review.
7. **Who works where.** Framework sessions run in `../Majorsilence.Forms`; app sessions run here. A milestone
   here is not done until the framework items it needs are released and adopted.
8. **Developing against unreleased framework code.** Pack the framework to a local folder feed
   (`dotnet pack -o <folder>`) and use a **git-ignored** `Directory.Build.local.props` here that adds the
   folder as an extra restore source and overrides the framework version. The committed files always pin a
   published version.
9. **Use the framework's own tooling** for the app's tests and design review (section 10): the headless
   backend, the automation tree, the WebDriver server, the MCP server and golden images.

### 11.2 How this register was made

Each item was checked in the framework source, docs and published packages, and the evidence column says
where. A claim marked **(verify)** rests on a string search or documentation and must be confirmed by the
first framework session that picks the item up. Some items name behaviour that was found by reading code the
app would otherwise have discovered by hitting a bug; that is the point of doing the audit first.

### 11.3 The register

| ID | Capability | Evidence today | Proposed change | Needed by |
|---|---|---|---|---|
| F1 | Rounded rectangles on `Graphics` | `SkiaExtensions.FillRoundedRectangle` and `DrawRoundedRectangle` exist on `SKCanvas` and paint `border-radius`. `Graphics` has no equivalent and its canvas is internal | `Graphics.FillRoundedRectangle` and `DrawRoundedRectangle` (`Rectangle` and `RectangleF`, one radius or four per-corner radii, `Brush` and `Pen` overloads) and `GraphicsPath.AddRoundedRectangle`. Headless pixel tests. **Do this first**: it is small and proves the whole workflow | M2, and pilot in M0 |
| F2 | MVVM helpers package | The framework sample wires `PropertyChanged` and `Click` by hand. `Application.RunOnUIThread` exists | New `Majorsilence.Forms.Mvvm`: `Observe`, `BindCommand`, `BindingScope`, `IUiDispatcher` (default over `RunOnUIThread`). No reflection, no toolkit dependency, only `INotifyPropertyChanged` and `ICommand`. Docs and a sample | M1, M3 |
| F3 | `ICommand` on buttons | `ButtonBase.Command` is typed to the framework's `ICommandExecutor`. `CommandParameter` is stored, never passed. `CanExecute` does not drive `Enabled` | Accept `System.Windows.Input.ICommand` (what WinForms on modern .NET uses **(verify)**), pass the parameter, follow `CanExecuteChanged`. Same for `ToolStripItem`. Then a toolkit `RelayCommand` binds directly | M3 |
| F4 | Binding docs and trimming | The ControlGallery MVVM sample and a `Directory.Packages.props` comment call `DataBindings` a stub, but the docs and tests say it is live. `BindingRuntime` suppresses trim warnings ("a trimmed app has to root the types it binds"). The AOT smoke test does not bind | Fix both comments. Add trimmer annotations or root guidance, and an AOT smoke test that binds a view model | M1 (comments), M5 (AOT) |
| F5 | Frame-aligned animation clock | `Timer` wraps Avalonia's `DispatcherTimer` through `AvaloniaTimer`; nothing is frame-aligned | `Control.RequestAnimationFrame (Action<TimeSpan>)` or an `AnimationClock`; on Avalonia use its top-level frame request **(verify the API)**; the Headless backend gets a deterministic manual clock so animations are testable | M2 |
| F6 | Tween and easing helpers | None. Only `ImageAnimator` (animated GIF) exists | `Majorsilence.Forms.Animation`: `Easing`, `Tween<T>` for `float`, `Color` and `PointF`, an `Animator` built on F5 | M2 |
| F7 | Reduced-motion preference | `SystemInformation.UIEffectsEnabled` exists for WinForms parity but is a constant `false`, so it cannot answer the question | Add `SystemInformation.PrefersReducedMotion` with real values (Android animator duration scale, iOS `isReduceMotionEnabled`, Windows client-area animation setting, macOS reduce-motion, GNOME `enable-animations`) and a change event | M2 |
| F8 | Mobile audio for the existing API | `SoundPlayer` and `SystemSounds` are public but play through `NativeAudio`, which spawns desktop utilities; silent on Android and iOS by design. `SoundPlayer.Load` is a no-op | An audio seam on the backend; implement in the Android row (`SoundPool`, `MediaPlayer`, audio attributes) and the iOS row (`AVAudioPlayer`, `AVAudioSession`). `SoundPlayer.Play`, `PlayLooping`, `Stop` and `SystemSounds` then work on mobile | M4 |
| F9 | Richer audio | `SoundPlayer` has no volume, no usage, no overlap | `Majorsilence.Forms.Media.AudioPlayer`: `Volume`, `Loop`, `Usage` (`Effect`, `Notification`, `Alarm`, `Media`), several at once, `Completed`, `IsSupported`. `Alarm` maps to Android `USAGE_ALARM` (plays with media volume down) and an iOS playback session | M4 |
| F10 | App lifecycle events | `Application` raises `ApplicationExit`, `OnExit`, `ThreadException` only. `Form.Activated` and `Deactivate` do not fire on single-view hosts. `Avalonia.Android` contains `IActivatableLifetime`, `Deactivated`, `OnResume` **(verify)** | `Application.Suspended` and `Resumed`; make `Form.Activated` and `Deactivate` fire on single-view hosts; wire through Avalonia's activatable lifetime | M4 |
| F11 | Back button | No back handler found in the Avalonia backend. `Avalonia.Android` contains `BackRequested` **(verify)** | A cancellable `Form.BackRequested` mapped from the platform back request or gesture; unhandled keeps normal behaviour | M3 |
| F12 | Keep screen awake | None | `Application.KeepScreenAwake`: Android `FLAG_KEEP_SCREEN_ON`, iOS `IdleTimerDisabled`, desktop inhibit (Windows execution state, macOS assertion, Linux D-Bus inhibit) | M5 (bedside mode) |
| F13 | Haptics | None | `Haptics.Tap`, `Impact`, `Vibrate`, and `IsSupported`: Android `Vibrator`, iOS `UIImpactFeedbackGenerator`; a reported no-op elsewhere | M4 |
| F14 | Notifications | `NotifyIcon` exists but its own documentation says `ShowBalloonTip` is a no-op | Make `NotifyIcon.ShowBalloonTip` real on desktop, and add `LocalNotifications` for mobile and desktop toasts: permission request, channels, importance, sound, ongoing, full-screen, tap callback. The foreground service's own notification stays app side | M4 |
| F15 | Text to speech | None | `Speech.SpeakAsync (text, options)`: Android `TextToSpeech`, iOS `AVSpeechSynthesizer`, desktop `say`, `espeak` or SAPI | M6 (optional) |
| F16 | Secure storage | None | `SecureStorage.GetAsync`, `SetAsync`, `Remove`: Android keystore, iOS Keychain, desktop OS store. Lower priority; the app holds a per-head implementation until it ships | M4 (optional) |
| F17 | Bundled fonts on every head | `PrivateFontCollection` and `FontResourceLoader` exist. Whether CSS `font-family` resolves a private font, and how to load from Android assets or the iOS bundle, is unconfirmed | Fix CSS resolution if needed; a documented helper that registers fonts from embedded resources or assets on every head | M2 |
| F18 | Colour emoji on mobile | `Theme.cs` chooses an emoji font per desktop platform (Noto Color Emoji through fontconfig); mobile is unconfirmed | Emoji fallback on Android and iOS, or a documented limitation | M2 (verify first) |
| F19 | Custom controls in the automation tree | The tree exposes id, name, role, value, state and bounds for built-in controls; whether custom-painted controls appear with meaningful state is unconfirmed | Let a custom control publish role, name, value and extra state (for example the beacon level) to the tree and to accessibility | M3 |
| F20 | iOS in published packages, then a first real run | `Majorsilence.Forms.Avalonia` 26.3.0 ships no `net10.0-ios` asset; the iOS head has never run on a simulator or device | Publish the asset; run the ControlGallery on a simulator and an iPad; fix what appears; then verify F8 to F16 on iOS | M5 |
| F21 | Android device shakeout | Soft keyboard, safe area and rotation are unit-tested but never run on a device | Run on a real phone and tablet; fix what appears | M3, M4 |
| F22 | Mobile screen readers | Accessibility is a described surface; nothing is published to TalkBack or VoiceOver | Bridge the automation tree to the platform accessibility API on the single-view host. A larger epic | Not blocking; M6 notes |
| F23 | CSS states and hard shadow | The grammar has only `:hover` (Button, LinkLabel, TrackBar); no `:disabled`, `:active` or `:focus`; no shadow | **Owner decision** (it widens the deliberately small subset): add `:active`, `:disabled`, `:focus` and an offset, no-blur `box-shadow`, with the docs, `ThemeCssReference` and Theme Studio updated. Lets the kid-style buttons be mostly CSS | M2 |
| F24 | CSS corner radius and dashed borders | `border-radius` is one value for all corners; borders are always solid | **Owner decision**, lower priority: per-corner radius, `dashed` | M2 (nice to have) |
| F25 | Conditional: raw Skia access | `Graphics.Canvas` is internal | Only if spike S3 shows `Graphics` cannot draw the beacon fast enough: a supported, documented way to reach the canvas | M2 (conditional) |

### 11.4 Order

1. **F1** first. It is small and proves branch, test, PR, release and bump end to end.
2. **F2, F3 and the F4 comments**, so MVVM needs no local helpers.
3. **F5, F6, F7, F17, F18** for the design system.
4. **F8, F9, F10, F11, F13, F14** for Android delivery.
5. **F12, F19, F20, F21** for tablet, bedside mode and iOS.
6. **F15, F16, F22** and the CSS items (F23, F24) as decided.

The owner cuts a release as each batch merges, and this repo bumps per release (policy 3).

### 11.5 Decisions for the owner (defaults in brackets; the framework session proceeds with them if unanswered)

1. **Packaging.** [Core API plus a backend seam for anything the UI itself uses (drawing, animation,
   lifecycle, back, keep-awake, audio); `Majorsilence.Forms.Mvvm` as its own package; a separate
   `Majorsilence.Forms.Essentials` package for haptics, notifications, text to speech and secure storage,
   because those carry per-platform dependencies the core deliberately avoids.]
2. **Desktop audio.** [Keep the spawn-an-OS-utility approach: no native dependency, 50 to 200 ms latency. A
   button "boop" may feel late on desktop; revisit only if it does. Mobile uses native playback.]
3. **CSS widening (F23, F24).** [Yes for `:active`, `:disabled`, `:focus` and a hard, no-blur `box-shadow`. No
   for gradients and images, which the documentation rejects on purpose.]
4. **`NotifyIcon` versus `LocalNotifications`.** [`NotifyIcon` stays the WinForms-parity desktop tray API;
   `LocalNotifications` is the cross-platform one.]
5. **Release cadence.** [A release after each merged batch in 11.4.]

### 11.6 Filing the issues

- **Where.** `majorsilence/Majorsilence.Forms`, a public tracker, so issues contain only framework facts and
  nothing from any private deployment. The tracker has no issue templates. It uses the labels `enhancement`,
  `bug`, `platform`, `documentation`, `question` and `tooling`.
- **Duplicates.** Checked on 2026-09-26: no register item matches an existing issue. Search again immediately
  before filing each one. Related closed issues to link where they apply: #170 (mobile and WASM: on-screen
  keyboard, safe areas, multi-head template), #171 (Android heads, startup crash, touch), #172 (Android emulator
  smoke test) and #174 (trimming and NativeAOT support).
- **Labels.** `enhancement` for every feature; add `platform` for backend and mobile items (F5, F8 to F14, F16,
  F20, F21, F22); add `documentation` to F4; `question` for the two owner decisions (F23, F24). Create one new
  label, `alert-buddy` ("Needed by the Alert Buddy app"), and put it on all of them so they are easy to find.
- **One tracking issue**, "Alert Buddy: framework work needed", with a task list (`- [ ] #N title`) in the order
  of 11.4 and a link to this document. Individual issues are titled `<Area>: <what>` (for example
  `Graphics: add FillRoundedRectangle and DrawRoundedRectangle`); the F-number goes in the body, not the title.
- **F25 is not filed** unless spike S3 shows it is needed. Every other register item is filed.
- **Body template** (fill from the register row and the acceptance table in 11.7):

```
## Problem
What the Alert Buddy app needs and why a UI framework should provide it.

## Evidence
What the framework does today, with file paths.

## Proposed change
The API shape and where it lives (core, backend seam, Avalonia row, or package).

## Acceptance criteria
- ...

## Notes
Register item F<n> of the Alert Buddy plan (majorsilence/alert-buddy). Needed by: <milestone>.
Related: #<n>
```

- **Mechanics.** `gh issue create --repo majorsilence/Majorsilence.Forms --title ... --label ... --body-file ...`,
  capture each number, then add the links below and to the tracking issue. PRs say `Closes #N`; never close an
  issue by hand.
- **Filed on 2026-09-26** in the framework tracker: the tracking issue majorsilence/Majorsilence.Forms#287 and one issue per item: F1 majorsilence/Majorsilence.Forms#263, F2 majorsilence/Majorsilence.Forms#264, F3 majorsilence/Majorsilence.Forms#265, F4 majorsilence/Majorsilence.Forms#266, F5 majorsilence/Majorsilence.Forms#267, F6 majorsilence/Majorsilence.Forms#268, F7 majorsilence/Majorsilence.Forms#269, F8 majorsilence/Majorsilence.Forms#272, F9 majorsilence/Majorsilence.Forms#273, F10 majorsilence/Majorsilence.Forms#274, F11 majorsilence/Majorsilence.Forms#275, F12 majorsilence/Majorsilence.Forms#278, F13 majorsilence/Majorsilence.Forms#276, F14 majorsilence/Majorsilence.Forms#277, F15 majorsilence/Majorsilence.Forms#282, F16 majorsilence/Majorsilence.Forms#283, F17 majorsilence/Majorsilence.Forms#270, F18 majorsilence/Majorsilence.Forms#271, F19 majorsilence/Majorsilence.Forms#279, F20 majorsilence/Majorsilence.Forms#280, F21 majorsilence/Majorsilence.Forms#281, F22 majorsilence/Majorsilence.Forms#284, F23 majorsilence/Majorsilence.Forms#285, F24 majorsilence/Majorsilence.Forms#286. F25 is conditional and is not filed. Issue numbers follow the order in 11.4.

### 11.7 Acceptance criteria (copied into each issue)

| ID | The change is done when |
|---|---|
| F1 | New `Graphics` members have XML docs. Headless tests at scale 1 and 2 show transparent corner pixels and filled edges for a uniform radius and for four different radii. `GraphicsPath.AddRoundedRectangle` yields a valid closed path. The API-diff gate and `COMPATIBILITY_MATRIX.md` are updated. A test fails when the implementation is stubbed |
| F2 | The package builds with the trim and AOT analyzers on (no reflection). Tests cover: `Observe` pushes the current value, pushes on change, stops after dispose, and marshals through the dispatcher; `BindCommand` enables and disables from `CanExecute` and executes on click; `BindingScope` disposes everything. A ControlGallery sample and README exist. The packable-project lists in all three workflow files are updated |
| F3 | `ButtonBase.Command` accepts `ICommand` (the existing `ICommandExecutor` keeps working), passes `CommandParameter`, and `Enabled` follows `CanExecute` with the subscription released when the command changes. `ToolStripItem` behaves the same. Tests fail without the fix. The matrix is updated |
| F4 | Both stale "stub" comments are corrected. The docs gain a "Binding and trimming" section. An AOT smoke test binds a view model under `PublishAot`, or the required roots are documented and tested |
| F5 | The API exists with an Avalonia implementation driven by its frame request and a Headless manual clock. A test steps ten frames deterministically. Documented |
| F6 | Easing set (linear, quad and cubic in and out, back overshoot, bounce) and `Tween<T>` for `float`, `Color` and `PointF`. Deterministic tests at t = 0, 0.5, 1, completion and cancellation |
| F7 | `SystemInformation.PrefersReducedMotion` returns the real setting on Android, iOS, Windows, macOS and GNOME, raises a change event, and has a Headless override for tests. Sources listed in the docs. The matrix is updated |
| F8 | A backend audio seam exists. `SoundPlayer` and `SystemSounds` play on an Android emulator and an iOS simulator (checklist in the PR). Fake-backend tests cover routing. Any remaining no-op is recorded in the baseline. Documented |
| F9 | `AudioPlayer` with volume clamp, loop, concurrent playback, `Completed`, `IsSupported`, tested with a fake backend. Device checklist: an `Alarm` sound plays on Android with media volume at zero, and `Stop` stops it |
| F10 | `Suspended` and `Resumed` fire, and `Form.Activated` and `Deactivate` fire on single-view hosts, when an Android emulator app is backgrounded and foregrounded. Fake-backend tests. Documented |
| F11 | A cancellable `Form.BackRequested`. A test raises a backend back request. Device check: it closes a sheet without leaving the app, and unhandled it exits normally |
| F12 | `Application.KeepScreenAwake` works on Android and iOS and inhibits sleep on the three desktops. Fake-backend tests. Device checks recorded |
| F13 | `Haptics` API with `IsSupported` false on Headless. Checked on a real Android phone and an iPhone (emulators have no vibrator) |
| F14 | `NotifyIcon.ShowBalloonTip` shows a real notification on Windows, macOS and Linux. `LocalNotifications` covers permission (API 33 and up), channels, importance, sound, ongoing, full-screen, and tap callback on an Android emulator. Fake-backend tests. Documented |
| F15 | `Speech.SpeakAsync` speaks on an Android emulator and an iOS simulator, can be cancelled, and reports `IsSupported` |
| F16 | Set, get and remove round-trip and persist across an app restart; the value is not readable from plain files. Fake-backend tests and device checks |
| F17 | A test or sample shows CSS `font-family` with a bundled font measuring and drawing with that font (Headless and an Android device). Registering fonts from embedded resources or assets is documented per head. Any bug found is fixed with a failing-first test |
| F18 | Emoji behaviour on Android and iOS is checked and recorded with screenshots: either it renders, or a fallback is added, or the limitation is documented |
| F19 | A custom control can publish role, name, value and extra state. The automation XML shows a sample custom control's state. Tested and documented |
| F20 | `dotnet restore` of a template app generated with `--IncludeiOS` works against the published packages. The ControlGallery boots on an iOS simulator and manually on an iPad. Every defect found is filed separately |
| F21 | The Android soft keyboard, safe-area and rotation checklist is run on a real phone and a tablet with results recorded. Each defect is filed and fixed or linked |
| F22 | A design note and a spike in which TalkBack reads a `Button` label. Then the work is split into issues |
| F23 | The owner's decision is recorded in the issue. If accepted: `ThemeCssReference`, the parser, the docs and Theme Studio are updated, with tests including a clear diagnostic for misuse (never a silent no-op) |
| F24 | As F23 |

## 12. Milestones

Track F (framework work, section 11) runs in parallel with the app milestones. Each milestone lists the
framework items it needs. **A milestone is not done until those items are released and adopted, with no
`TEMP-SHIM` left for them.** Start the app work that needs nothing (M1) immediately.

**M0. Bootstrap and spikes.**
- Install the SDK, `dotnet workload install android`, and the template:
  `dotnet new install Majorsilence.Forms.Templates`, then
  `dotnet new majorsilenceforms -n AlertBuddy --IncludeAndroid --IncludeWasm --msformsVersion 26.3.0`.
  Rename into the layout in 7.1. Pin versions centrally. Add `CLAUDE.md` (conventions, commands, the
  hygiene rules of section 14).
- Run spikes S1 to S9 and write the results in `docs/spikes.md`: S1 hello world on a real Android device
  from the published packages; S2 custom font via CSS on Android and desktop; S3 beacon prototype frame
  time on a mid-range phone; S4 `BeginInvoke` from a background thread on a single-view host; S5 soft
  keyboard and safe area on the device; S6 emoji rendering; S7 NDJSON streaming on Android over Wi-Fi and
  mobile data; S8 does `net10.0-ios` restore; **S9 MVVM wiring**: reproduce the framework's
  `CounterViewModel` example with CommunityToolkit.Mvvm in this solution layout, then compare the binder
  helpers (section 7.5) with declarative `DataBindings` on desktop, on an Android **Release** build with
  `PublishTrimmed`, and on iOS AOT once F20 allows. Record which of the two survive trimming. Open an
  upstream issue or branch for each failure.
- The register items are already filed as issues in the framework tracker (links in 11.6). Confirm each is
  still open and not duplicated, file any new finding as an issue at once, and complete **F1** as the pilot:
  issue, branch, test, PR that closes it, release, then bump this repo's pin. It proves the whole
  framework-first loop on a small change.
- **Done when:** the empty app runs on a real Android phone from published packages, the spike log exists,
  every failed spike maps to a filed issue, and F1 has been released and adopted.

**M1. Core and view models.** *Needs framework: nothing to start; land F4 (comments) and F2 early.* `NtfySubscription`, models, interpreter, hub, store, replay, settings and
secret abstractions, `FakeNtfy`, `PracticeAlertSource`; then every view model in 7.5 with its navigation and
commands, and the view model tests. **Done when:** Core and view model tests pass, including a soak test that
survives 100 forced disconnects, and a console harness drives `FakeNtfy` through the whole alert lifecycle
(warning, alarm, acknowledge, all clear) purely through view models, with no UI project referenced.

**M2. Design system.** *Needs framework: F1, F5, F6, F7, F17, F18; F23 if accepted; F25 only if spike S3 fails.* Theme CSS (Day and Night), fonts, `AlertPalette`, the controls in 8.5, `DesignGallery`,
headless golden renders. **Done when:** the gallery renders every control in every state, the images have
been looked at and revised at least once, and no state relies on colour alone.

**M3. Screens and flows.** *Needs framework: F2, F3, F4, F11, F19, F21.* The views for every view model in 7.5 (Home, alarm takeover, detail, Alert book,
Practice, First run, Settings, the grown-up gate), the binder helpers and `BindingScope`, `PageHost`
navigation, adaptive layout. **Done when:** the whole flow runs on desktop against `FakeNtfy` and on an
Android emulator, and headless renders of every screen at compact and expanded widths look right.

**M4. Android background delivery.** *Needs framework: F8, F9, F10, F13, F14; F16 if it has landed.* Foreground service, channels, boot receiver, permission wizard,
sound and haptics, lifecycle and back button. **Done when:** on a real phone, with the screen off, a
scripted alarm rings on the alarm stream and a full-screen view appears, it survives a reboot, and every
honest-banner condition shows correctly.

**M5. Tablet, desktop, web, iOS.** *Needs framework: F12, F20, and F8 to F16 verified on iOS.* Expanded layout, bedside mode, desktop head, web demo on GitHub Pages,
and the iOS head once F20 is resolved. **Done when:** an Android tablet and a desktop window use the
two-pane layout, and iPad is either running or has a dated, linked upstream blocker.

**M6. Polish and release.** *Needs framework: F15 (optional); F22 findings documented.* Sounds, text-to-speech, reduced motion audit, accessibility notes, screenshots
(fictional data only), README, signed APK on a GitHub Release. **Done when:** a grown-up who has never
seen it can install, configure and receive a test alert using only the README.

**M7. Stretch, in this order.** UnifiedPush support, iOS background via an APNs relay, the child drawing
their own buddy, recording custom voice lines, QR setup.

## 13. Risks and open questions

**Risks**
- Android vendors kill background services. Mitigation: the honest banner, the onboarding wizard, and the
  UnifiedPush upgrade path.
- Foreground service policy changes across Android versions. Mitigation: **(verify)** at implementation
  time and record the choice.
- iOS cannot listen in the background and the iOS head is unproven. Mitigation: foreground and bedside
  mode, and set expectations in the README.
- The beacon may not hit 30 fps through `Graphics`. Mitigation: spike S3, then cut effects, or add a supported canvas API to the framework (F25).
- Reflective `DataBindings` can break under trimming or iOS AOT with no compile-time warning. Mitigation:
  the trim-safe binder helpers are the default, S9 proves anything else, and F4 fixes it upstream.
- Framework work can stall the app, or grow beyond what the app needs. Mitigation: F1 first as the pilot,
  the app milestone that needs nothing (M1) starts at once, `TEMP-SHIM`s are allowed to unblock, each register
  item is independently useful and must come with tests, and items that widen the framework's deliberate
  choices (F23, F24) wait for the owner's decision.
- A shim outlives its framework fix. Mitigation: `docs/framework-shims.md` and a CI check that fails when a
  shim names an item already recorded as released in `docs/framework-versions.md`.
- A child may be frightened by an alarm. Mitigation: never-scary rules (4.1), practice mode, the calm
  ack state, no fire imagery.
- False security. Mitigation: the safety note everywhere it matters and a second receiver.

**Open questions for the owner** (defaults in brackets, proceed with them if unanswered)
1. Is an Apple Developer account wanted? [No: iOS is foreground-only until then.]
2. Play Store or sideload? [Sideload APK first.]
3. Bilingual text? [English only, strings kept in one table so translation is possible.]
4. Should the child be able to pick the buddy colour? [Yes, from four safe colours that are not the
   state colours.]
5. Should Alert Book history be exportable by a grown-up? [Not in v1.]

## 14. Public-repo hygiene (read before every commit)

This repository is public. It must **never** contain: any real hostname, IP address, port, topic name,
username, password, token or ntfy URL from a real deployment; the names of real people or real rooms
(children's names appear in real room names, so fixtures and screenshots use invented ones such as
"Sunny room" and "Workshop"); references to any private repository or infrastructure; signing keys.
Use `https://ntfy.example.com/home-alerts` in docs and tests. Screenshots come from Practice mode or
`FakeNtfy` only. The README describes the app generically. Add a CI check that fails the build if a
committed file matches a small deny-list (`*.jks`, `*.keystore`, `*.p12`, `tk_` followed by 29 characters).

## Appendix A. Interface sketches

```csharp
public interface ISecretStore  { string? Get (string key); void Set (string key, string value); void Remove (string key); }
public interface ISettingsStore { AppSettings Load (); void Save (AppSettings settings); }
public interface ISoundPlayer  { void Play (Cue cue); void StartLoop (Cue cue); void StopLoop (); }
public interface IHaptics      { void Tap (); void Alarm (); void Stop (); }
public interface IKeepAwake    { bool Enabled { get; set; } }
public interface IAlertNotifier { void Show (Alert alert); void Clear (string alertId); }
public interface ILifecycle    { event Action Resumed; event Action Paused; event Func<bool> BackPressed; }
public interface IBackgroundListener { bool CanListenInBackground { get; } string? WhyNot { get; } void Start (); void Stop (); }
public interface IClock        { DateTimeOffset Now { get; } }
public enum Cue { Boop, Warning, Alarm, AllClear, Cheer, Practice }
public interface IUiDispatcher { void Post (Action action); }
public interface INavigator    { ObservableObject Current { get; } event Action? CurrentChanged; void GoTo<T> () where T : ObservableObject; void GoBack (); }
```

View model and wiring sketch (CommunityToolkit.Mvvm, trim-safe helpers):

```csharp
// AlertBuddy.ViewModels (no Majorsilence.Forms reference)
public partial class MainViewModel : ObservableObject
{
    [ObservableProperty] private string statusText = "";
    [ObservableProperty] private AlertLevel level;

    [RelayCommand] private void OpenBook () => navigator.GoTo<AlertBookViewModel> ();
    // partial void OnLevelChanged (AlertLevel value) => ... recompute StatusText
}

// AlertBuddy.Shared/Binding (TEMP-SHIM (F2): becomes the Majorsilence.Forms.Mvvm package)
public static class ViewBinding
{
    public static IDisposable Observe<TVm, TValue> (this TVm vm, string property,
        Func<TVm, TValue> read, Action<TValue> write) where TVm : INotifyPropertyChanged;
    public static IDisposable BindCommand (this ButtonBase button, ICommand command);
}

// In a view (AlertBuddy.Shared/Views/HomeView.cs)
scope.Add (vm.Observe (nameof (MainViewModel.StatusText), v => v.StatusText, t => bubble.Text = t));
scope.Add (vm.Observe (nameof (MainViewModel.Level),      v => v.Level,      l => beacon.Level = l));
scope.Add (bookButton.BindCommand (vm.OpenBookCommand));
```

## Appendix B. Theme files

`Themes/day.css` and `Themes/night.css` under `AlertBuddy.Shared`. Start from the excerpt in 8.7. Run each
through `ThemeStyleSheet.Parse` in a test and assert there are no diagnostics (the parser never throws; it
reports). Contrast-check every foreground and background pair in a test using the ratios in 8.2.

## Appendix C. Fictional fixture

```
{"id":"aB3dEf","time":1790000000,"event":"open","topic":"home-alerts"}
{"id":"aB3dEg","time":1790000045,"event":"keepalive","topic":"home-alerts"}
{"id":"aB3dEh","time":1790000100,"event":"message","topic":"home-alerts","priority":4,"title":"Workshop: temperature warning","message":"Workshop is at 41.2 °C"}
{"id":"aB3dEi","time":1790000400,"event":"message","topic":"home-alerts","priority":5,"title":"Workshop: temperature alarm","message":"Workshop is at 50.6 °C"}
{"id":"aB3dEj","time":1790000900,"event":"message","topic":"home-alerts","title":"Workshop: temperature alarm (resolved)","message":"Workshop is at 44.0 °C"}
```
