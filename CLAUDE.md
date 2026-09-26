# Alert Buddy: guide for AI coding tools

A friendly alert receiver for a family: it connects to a self-hosted ntfy server and turns "something needs a look" messages
into a small character, a sound and one instruction: tell a grown-up. Built on **Majorsilence.Forms** (a WinForms-model UI
framework drawn with SkiaSharp), which is owned by the same person as this repo. `PLAN.md` is the plan; read its section 0
first. `docs/spikes.md`, `docs/framework-findings.md` and `docs/framework-versions.md` record what has been learned since.

## Layout

```
src/AlertBuddy.Core/        (milestone 1)  ntfy client, alert model, interpreter, hub. No UI, no framework, no platform code
src/AlertBuddy.ViewModels/  CommunityToolkit.Mvvm view models, one per screen. Never references Majorsilence.Forms
src/AlertBuddy.Shared/      Majorsilence.Forms views, custom controls, binder helpers, theme, platform adapters
src/AlertBuddy.Desktop/     Avalonia desktop head        src/AlertBuddy.Android/  Android head
src/AlertBuddy.Wasm/        browser head (a demo, not a live receiver)
tests/                      xunit v3: Core.Tests, ViewModels.Tests (no UI), Shared.Tests (Headless backend)
tools/                      hygiene and shim guards; FakeNtfy and SoundSynth arrive with their milestones
```

Dependency arrows point one way: Core <- ViewModels <- Shared <- heads. That is what keeps the logic testable without a UI.

## Commands

```bash
dotnet build src/AlertBuddy.Desktop -c Release               # desktop head; Release makes warnings errors
dotnet test tests/AlertBuddy.Shared.Tests -c Release         # Headless UI tests
MF_HEADLESS_SCALE=2 dotnet test tests/AlertBuddy.Shared.Tests -c Release   # the scaled-display shape; run both

# Android needs the SDK and a full JDK 21. On the author's machine the default JDK 25 has no `jar`, so set them per shell:
export ANDROID_HOME=$HOME/Android/Sdk JAVA_HOME=/usr/lib/jvm/java-21-openjdk-amd64
dotnet build src/AlertBuddy.Android -c Release -p:AndroidSdkDirectory=$ANDROID_HOME

./tools/check-hygiene.sh && ./tools/check-shims.sh           # what CI's first job runs
```

Emulator workflow: create an AVD (`avdmanager create avd -n alertbuddy-phone -k "system-images;android-36;google_apis;x86_64" -d pixel_5`),
boot it with `emulator -avd alertbuddy-phone -no-audio`, `adb install -r` the `*-Signed.apk` from
`src/AlertBuddy.Android/bin/Release/net10.0-android/`, and launch `com.majorsilence.alertbuddy/<activity>` (find the generated
activity name with `adb shell cmd package resolve-activity --brief com.majorsilence.alertbuddy`). Screenshots:
`adb exec-out screencap -p > out.png`. An emulator is not a device: say which one a result came from.

## Public-repo hygiene (read before every commit)

This repository is public. It must **never** contain: a real hostname, IP address, port, topic, username, password, token or ntfy
URL from a real deployment; the names of real people or real rooms (fixtures and screenshots use invented ones such as
"Sunny room" and "Workshop"); references to any private repository or infrastructure; signing keys. Use
`https://ntfy.example.com/home-alerts` in docs and tests. Screenshots come from Practice mode or `FakeNtfy` only.
`tools/check-hygiene.sh` is the backstop, not the plan.

## Framework first

If the app needs something a UI framework should provide, it is built in the framework, released, and this repo bumps its pin.
No lasting workarounds.

- File each bug or gap in `majorsilence/Majorsilence.Forms` the moment it is met (search first), label it `alert-buddy`, add it
  to the tracking issue #287, and record it in `docs/framework-findings.md`. Issue text is framework facts only.
- A workaround is a **shim**: mark it `// TEMP-SHIM (F<n>)`, list it in `docs/framework-shims.md`, delete it when the release
  lands.
- `Directory.Packages.props` pins one exact published version. Each bump is its own commit and adds a line to
  `docs/framework-versions.md`. To try unreleased framework code, pack it to `.local-feed/` and use the git-ignored
  `Directory.Build.local.props` (`MajorsilenceFormsVersion` and `RestoreAdditionalProjectSources`); never commit that.
- Framework changes are made in `../Majorsilence.Forms` under its own `CLAUDE.md` and `CONTRIBUTING.md`: branch off `main`,
  a failing-first test, all four test gates, no `Co-Authored-By` or generated-with trailers there, and **do not commit or push
  in that repo**: leave the working tree for its owner.

## Rules learned the hard way (each has a framework issue)

- **Custom `OnPaint` draws in device pixels.** Start with `e.Graphics.ScaleTransform ((float)e.Scaling, (float)e.Scaling)` and draw
  in logical units (#291). Test custom controls at scale 1 and 2.
- **Set a `TextBox`'s `Text` in its initializer, or after the form is shown**, never after parenting it to a panel that is not
  yet on a form (#289).
- **Do not use `DataBindings`.** Wire view models with `PropertyChanged` and `ICommand` through the binder helpers; anything
  trimming or iOS AOT must keep is referenced by code (`nameof`, lambdas), never by a string (#290).
- **Lay views out inside a docked host.** A docked panel is inset by the system bars on Android; a control placed by `Location`
  directly on the form is not (S5).
- **The Android head's theme must descend from `Theme.AppCompat`** (#288). Do not restore the template's `Theme.NoTitleBar`.
- `Timer` is ambiguous between `Majorsilence.Forms.Timer` and `System.Threading.Timer`; qualify it.

## Conventions

- The framework's style: a space before every parameter list (`Method (arg)`, `new Size (1, 2)`), block-scoped namespaces,
  4-space indent (`.editorconfig` carries the mechanical rules). Comments say **why**, not what.
- View models hold all behaviour and are the only place it lives; a view is layout and wiring. A view model never touches a
  control and never references a platform API.
- Tests: assert mechanisms and relationships, not "something was drawn". **Prove a test can fail** before trusting it: break the
  code it covers and watch it go red.
- Copy is in one table so it can be translated; sentence case, plain verbs, one exclamation mark in the whole app (PLAN.md 8.10).
- Do not commit or push unless asked. Design-review renders go to the git-ignored `render-out/`.
