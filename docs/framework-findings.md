# Framework findings

Every bug, gap, awkward API or documentation error found in Majorsilence.Forms while building this app (PLAN.md section 11.1,
policy 4). Each is filed in the framework tracker (`majorsilence/Majorsilence.Forms`, label `alert-buddy`) as soon as it is met
and listed on the tracking issue #287. This log is reviewed at every milestone.

Register items F1 to F24 (the planned work) are issues #263 to #286 and are tracked there, not here. This file is for what the
app **found** that was not in the register.

| Issue | Found | Symptom | Repro | App workaround | Status |
|---|---|---|---|---|---|
| #288 | M0, S1 | The Android head from the 26.3.0 template crashes on its first frame: `Theme.AppCompat` required | Generate with `--IncludeAndroid`, run on an API 36 emulator | `Resources/values/styles.xml` and `Theme = "@style/AlertBuddyTheme"` | Fixed: majorsilence/Majorsilence.Forms#294 merged 2026-09-26, released in 26.4.0 |
| #289 | M0, S9 | A `TextBox` whose `Text` is assigned after being parented to a window-less panel is drawn at scale 1 (half size at 2, a third at 2.75), permanently | Headless at `MF_HEADLESS_SCALE=2`, variant C in the issue | Set `Text` in the initializer or after the form is shown | Fixed: majorsilence/Majorsilence.Forms#293 merged 2026-09-26, released in 26.4.0 |
| #290 | M0, S9 | `DataBindings` silently does nothing for a missing or trimmed member; Android Release breaks reads (full trim) and write-back (default) | Spike on an emulator; three configurations in `docs/spikes.md` | Do not use `DataBindings`; helper wiring | Fixed: majorsilence/Majorsilence.Forms#333 merged 2026-10-01, released in 26.5.0 |
| #291 | M0, F1 work | Custom `OnPaint` draws in device pixels, undocumented; the gallery sample ignores it | 10x10 `FillRectangle` at scale 2 covers 10x10 device pixels | Scale by `e.Scaling` in every custom control (removed in 26.5.0: painting became logical, #339) | Fixed: #332 and #339, released in 26.5.0 |
| #317 | Desktop-viewable slice (this session) | `Control.ClientSize` reads back in device pixels outside `OnPaint`, while `Width`/`Height`/`Top`/`Bottom` stay logical -- undocumented, and the two families look interchangeable | `HomeView`/`AlarmView`'s manual layout centred children correctly reading `Width` at both `MF_HEADLESS_SCALE` 1 and 2, but drifted off-screen reading `ClientSize.Width` at scale 2 | Read `Width`/`Height`, never `ClientSize`, in manual child-control layout | Fixed: majorsilence/Majorsilence.Forms#331 merged 2026-09-30, released in 26.5.0 |
| #352 | Settings and First run views (this session) | `Majorsilence.Forms.Mvvm` has no two-way text binding, so every app writes the same `TextBox` helper and its re-entrancy guard | Bind a `TextBox` to a view model without `DataBindings` | `Shared/Binding/FormBindings.cs`, TEMP-SHIM (F26) | Fixed: majorsilence/Majorsilence.Forms#353 merged, released in 26.6.0; shim removed (`BindIndex` is `BindSelectedIndex`, `BindNumber` is `BindValue` on a `decimal`) |
| #366 | Android emulator run on 26.6.0 (2026-10-02) | A themed `TextBox` loses its border while focused (drawn in the page colour), and `TextBox:focus` is rejected by the CSS parser | Apply the Day theme, focus a `TextBox`, sample its left edge: `#2B1B4D` calm, `#F6F2FF` focused | none yet; the field being typed in has no outline | Fixed: released in 26.7.0 (#375): a focused control keeps its look and `TextBox:focus` works; adopted (a blueberry ring in Day, Butter in Night) |
| #368 | Android emulator run on 26.6.0 (2026-10-02) | No way to ask a `TextBox` for a number pad: the four-digit PIN fields bring up the full letter keyboard | Focus a First run PIN field on an Android emulator | none; the PIN is typed on a letter keyboard | Fixed: released in 26.7.0 (#381, #382): `TextBox.InputKind`; adopted on the PIN fields, and a masked box asking for Number shows the number pad on the emulator |
| #370 | Tablet emulator run on 26.6.0 (2026-10-03) | Removing a child control leaves its pixels on screen until the parent is invalidated | Headless: remove a red child panel from a white parent, render: the pixel is still red; add `parent.Invalidate ()` and it is white | `Invalidate` after removing cards in Home and the Alert book, TEMP-SHIM (F28) | Fixed: released in 26.7.0 (#372); the F28 invalidations are removed and `Home_PaintsOverTheSpaceAResolvedCardLeft` passes without them |
| #371 | Tablet emulator run on 26.6.0 (2026-10-03) | An animated control grows native memory on the Android host until the process is killed (LOW_MEMORY, rss 2.3 GB); a forced GC each second bounds it | Pixel Tablet emulator, a 33 ms `Invalidate` timer; native heap climbs 194 MB, 226 MB, 258 MB, on; flat on a static screen | `MemoryGuard` collects once a second on Android, TEMP-SHIM (F27) | Fixed: released in 26.7.0 (#378); `MemoryGuard` (F27) is removed. Measured on the emulator with the released package: native heap 107-123 MB over 90 s with the beacon animating |
| #396 | Settings scroll bug (2026-10-06) | No public stack layout: `FlowLayoutPanel` cannot cap and centre content on a wide window, and anchored children lag the panel's width after a resize. `StackLayoutEngine` exists but is internal and works on toolbar-style items | Headless spike: a top-down `FlowLayoutPanel` form at 360 and 900 wide (details in the issue) | `FormColumn` (`src/AlertBuddy.Shared/Views/FormColumn.cs`), alert-buddy's own scrolling stack | Fixed: released in 26.9.0 (#397): `FormColumn` is now a `StackPanel` |
| #398 | Android test build of the StackPanel work (2026-10-07) | `ScrollControlIntoView` subtracts the scroll offset twice, so focusing a control in a scrolled panel jumps the content back up; a tap on a `ComboBox` while scrolled never opens it (the press focuses it and the content jumps before the release) | Headless: 30 text boxes in a scrolled `Panel`, focus one that is fully visible: `AutoScrollPosition` -900 becomes -308 | None; `FormColumn` already keeps its own scroll offset (alert-buddy `ViewBehaviourTests`) | Fixed: released in 26.9.0 (#400) |
| #399 | Android and desktop run (2026-10-07) | The caret of an empty single-line `TextBox` taller than its text hangs from the box's vertical centre (7 px low in a 48 px box) instead of sitting on the text's line, so the focused field is hard to find | Headless: `GetPositionFromCharIndex (0)` of an empty 48 px box is Y 25, of a box holding " " is Y 18 | None | Fixed: released in 26.9.0 (#401) |

Evidence added to existing issues:

- #266 (binding docs, trimming and AOT): the trimming evidence, and that the smoke test should bind in both directions.
- #290 (binding fails silently): NativeAOT (ILC) evidence, 2026-09-26. Not keeping `Control.Text` makes `DataBindings.Add` throw;
  not keeping `Control.TextChanged` or a view-model property fails silently (reads work without the event, typed text never
  reaches the source). The smallest working `TrimmerRootDescriptor` is those members and nothing broader.
- #284 (mobile screen readers, F22): nothing in this app sets an accessible name, and the views are custom-painted, so the first TalkBack run is expected to find unlabelled canvas. See `docs/design.md`.
- #281 (Android device shakeout): soft keyboard never appeared, safe-area behaviour, `BeginInvoke` result.
- #271 (colour emoji): renders on Android and Headless; iOS untested.

## Register item F1 (Graphics rounded rectangles, #263)

Implemented in `../Majorsilence.Forms` and open as majorsilence/Majorsilence.Forms#292 (closes #263). `Graphics.FillRoundedRectangle`
and `DrawRoundedRectangle` (`Rectangle`, `RectangleF` or x/y/width/height, one radius or four per-corner radii clockwise from the
top-left) and `GraphicsPath.AddRoundedRectangle`, with a shared geometry helper, 23 tests (pixel checks at scales 1 and 2, per-corner
order, the CSS scale-down rule, validation, overloads and path agreement) and a matrix row. All four CI gates pass (5502 tests, 0
failed), the API-diff gate reports no new gaps, and seven deliberate breakages each turned the intended tests red.

**Merged** 2026-09-26 (`1dc0db3` on the framework's `main`), but **not released**: the latest release is 26.3.0, dated 2026-09-25,
before the merge. Still to do, by the owner: cut a release, then bump the pin here (`docs/framework-versions.md`). This app can use
it earlier through a locally packed build (PLAN.md 11.1, policy 8).

## Register item F2 (`Majorsilence.Forms.Mvvm`, #264)

Open as majorsilence/Majorsilence.Forms#298 (closes #264), from a git worktree `../Majorsilence.Forms-f2` on branch `mvvm-package`.

- **What it is.** A new package, `net8.0` and `net10.0`, with the trim and AOT analyzers on, no reflection, no expression trees, no toolkit
  dependency. `Observe (nameof (...), vm => vm.X, x => ...)` pushes now and on every change of that property, on the UI thread, and
  drops a push queued before disposal; `BindCommand` sets `Enabled` from `CanExecute` (marshalled) and runs the command on click, on any
  `Control` or on a menu or tool strip item; `BindingScope` disposes a page's bindings latest first and survives one that throws;
  `IUiDispatcher` (`CheckAccess`, `Post`) has a default over the platform backend and a fake for tests. `BindCommand` uses `Click` and
  `Enabled`, not `Command`, so it works on this app's custom-painted cards and does not wait for F3.
- **Tests.** 31 in `MvvmHelpersTests`. The tests were written after the code, so their proof is mutation: 22 mutations, and **one
  survived** the first time. Leaving the click handler attached after dispose looked harmless, because a flag stops it running, but it
  keeps the binding alive on the control, which is the leak a scope exists to prevent. `Bind` became internal so a test counts the
  subscriptions, and that mutation is now killed. All 22 are caught and the files were restored identical.
- **Gates.** Four gates 5545 passed, 0 failed, 4 skipped in each shape (5514 on `main` plus the 31); the API-diff gate reports no new
  gaps; ControlGallery builds with the new `MvvmHelpersPanel`; `dotnet pack` gives `lib/net8.0` and `lib/net10.0` with the README.
- **Limits, stated in the PR and `docs/mvvm.md`.** It is not in the NativeAOT smoke test. The default dispatcher is tested only for
  `CheckAccess` on Headless. The async-command-disables-the-control behaviour is the command's own `CanExecute` and is not exercised
  (no toolkit in the tests). Two-way text is not included.

Effect on this app: PLAN.md 7.5 says to keep a minimal copy of these helpers in `Shared/Binding` marked `TEMP-SHIM (F2)` until the
package is released. Prefer a locally packed build (`.local-feed/` and `Directory.Build.local.props`, which is git-ignored) over
writing a shim: it is the same code, and there is then nothing to delete. `Majorsilence.Forms.Mvvm.IUiDispatcher` is the view layer's
dispatcher; `AlertBuddy.ViewModels` keeps its own one-method `IUiDispatcher`, and `Shared` adapts between them.

## Register item F3 (`ICommand` on buttons and tool strip items, #265)

Open as majorsilence/Majorsilence.Forms#295 (closes #265), from a git worktree `../Majorsilence.Forms-f3` on branch `command-icommand`.

- **What was wrong.** `ButtonBase.Command` was typed to the framework's own `ICommandExecutor`, so a toolkit `RelayCommand` could not be
  assigned; the parameter was stored and never passed; `CanExecute` never touched `Enabled`. `ToolStripItem.Command` already took an
  `ICommand` but was **inert**: choosing the item ran nothing. Real WinForms on .NET 10 types both as `System.Windows.Input.ICommand`
  (checked against its reference assembly, which settles the issue's "(verify)").
- **The change.** Both now run the command on click with `CommandParameter`, after the `Click` handlers and only while `CanExecute`
  is true; `Enabled` follows `CanExecute` (a new parameter re-asks); clearing the command gives back the control's **own** earlier state,
  not the parent-folded one; the subscription is released when the command changes; the three change events are raised. One shared
  internal helper (`CommandLink`) backs both classes so they cannot drift.
- **A deviation from the issue's acceptance criteria, confirmed by the owner.** The issue asks that `ButtonBase.Command` accept
  `ICommand` **and** that "the existing `ICommandExecutor` keeps working". Both cannot hold: C# has no conversion between two
  interfaces, and default interface members are unavailable on the `netstandard2.0` target. WinForms parity won, so `Command` is now an
  `ICommand`, and an executor moves over with `executor.AsCommand ()`. That is a **source break** for code that assigned an
  `ICommandExecutor` directly; nothing in the repository other than four existing tests did, and they now use the adapter. It is
  recorded in the matrix and in `docs/behaviour-gap-plan.md`.
- **Tests.** 20 in `CommandBindingTests`. Before the fix the four `ToolStripItem` tests failed behaviourally (`Expected ["grown-up"],
  Actual []`; `Enabled` ignored `CanExecute`), and the `ButtonBase` tests could not compile, which is the failure for a changed property
  type. Seventeen mutations of the change (ignore `CanExecute`, drop the parameter, skip the unsubscribe, skip or over-apply the restore,
  skip each refresh point, use the parent-folded flag, swap the click order, drop the `ToolStripItem` override, break the adapter) were
  each killed by the intended test, and every file was restored identical. A mutation of the null guard added afterwards was killed too.
- **Gates.** Four gates 5534 passed, 0 failed, 4 skipped in each shape, rerun after rebasing onto the merged #293 and #294 (5514 on
  `main` plus the 20); the API-diff gate reports no new gaps. Gate 2 **caught a Release-only failure** the Debug run had not: analyzer CA1510 on a hand-written null check, fixed with the codebase's
  `Guard.ThrowIfNull`. That is why all four shapes are run.
- **Limits.** A `CanExecuteChanged` raised off the UI thread is not marshalled (stated in the matrix). Only `ButtonBase` and
  `ToolStripItem` were changed; other command-capable controls were not looked for.

Effect on this app: once released, the command half of the planned binder helpers (PLAN.md 7.5, milestone 3, not written yet) is
unnecessary, because a view assigns a view model's command to `Command` directly. Nothing in this repository depends on it yet.

## Register item F7 (`SystemInformation.PrefersReducedMotion`, #269)

Open as majorsilence/Majorsilence.Forms#301 (closes #269), branch `reduced-motion`.

- **What it is.** `PrefersReducedMotion`/`PrefersReducedMotionChanged`, answered by `Backends.IReducedMotionSource` (the same
  optional-interface shape F5's `IAnimationFrameSource` set), false when the active backend does not implement it. Headless is
  settable directly; Avalonia answers per platform: Android (animator duration scale, a real `ContentObserver` push) and iOS
  (`UIAccessibility.IsReduceMotionEnabled`, a real push) need no polling; Windows, macOS and Linux/GNOME share one desktop build,
  told apart at run time, and are re-read every 2 s through a new reusable `PolledSetting` helper.
- **Verified for real, twice.** Android: on the `alertbuddy-phone` emulator, toggling `adb shell settings put global
  animator_duration_scale 0`/`1` while a probe app ran showed the value flip and the changed event fire twice, with no polling.
  Linux/GNOME: toggled the real `gsettings` key on the dev machine three times and confirmed the read tracked each step, then
  restored it. **Windows, macOS and iOS are written from documented APIs and were not run** — no host for any of the three; the
  desktop OS-branch selection itself is unit-tested with the OS faked, so that part is covered everywhere regardless.
- **Tests.** 26 in `ReducedMotionTests`; 21 mutations killed, two survived the first pass (an unsubscribe hidden by a redundant
  guard, and OS-branch selection a "doesn't throw" test couldn't see) and are now killed by tightened tests. One mutation
  (`AvaloniaPlatformBackend`'s lazy-cache) still survives, matching an existing gap: nothing in that framework unit-tests
  `AvaloniaPlatformBackend` directly anywhere (it needs a live Avalonia `Application`).
- **Gates.** Four gates 5647 passed, 0 failed, 4 skipped in each shape (5621 on `main`, with F6 merged, plus the 26 new); the
  API-diff gate reports no new gaps; ControlGallery builds.
- **A real build-configuration bug found and fixed along the way, unrelated to F7 itself:** a scratch verification app that
  references `Majorsilence.Forms.Avalonia` without passing `-p:EnableAndroidHead=true` through to that ProjectReference builds
  silently against the desktop row instead of the Android one, and crashes at launch with `WindowingPlatformStub.CreateWindow:
  NotSupportedException` — no compile error, no warning. Worth remembering for any future scratch Android app in this app's own
  spikes.

Effect on this app: milestone 2's motion (the beacon, section 8.6) can check this before animating, once released. Nothing in
this repository depends on it yet.

- **A real CI failure, corrected after the fact.** The iOS branch (no host, no workload here) had a genuine compile
  error: `UIAccessibility.Notifications.ObserveReduceMotionStatusDidChange` does not exist. Confirmed against
  Microsoft's dotnet/macios API docs that the notification is bound on `UIView.Notifications`, not `UIAccessibility`;
  `UIAccessibility.IsReduceMotionEnabled` itself was right. Fixed in a follow-up commit; PR #301's `ios` and
  `sample-ios` CI jobs are now green. The PR's own text already said this code was "written, not run" — this is why
  that qualifier matters, and why it stays in the PR description rather than being quietly dropped once fixed.

## Register item F6 (easing, tweens and an Animator, #268)

Open as majorsilence/Majorsilence.Forms#300 (closes #268), branch `animation-tween`, built on F5.

- **What it is.** `Easing` (the easings.net set), `Tween<T>` (a value moving from one to another over a duration, no clock,
  no state — `Tween.Of` for `float`, `PointF`, `Color`), and `Animator.Animate` which runs a tween on `RequestAnimationFrame`
  on a `Control` **or** a `WindowBase` (a `Form` is not a `Control` in this framework, so the window overload matters for the
  alarm takeover's own animation, not just a card's).
- **Tests.** 32 in `AnimationTweenTests`, including exact values for the back/bounce constants, not just endpoints. 28
  mutations (easing formulas, tween clamping/zero-duration/colour-clamping, animator start/elapsed/disposal/cancel-ordering/
  exception-handling, the window overload) all killed; one test was tightened after a mutation exposed it comparing too
  loosely (cancelling from inside `apply` was checked only for the immediate next frame, not a later one).
- **Measured on a real Avalonia window.** Driving a `Label.Text` update every frame for 4 s showed one long start-up stall
  (matching F5's own start-up finding) and otherwise steady ~16 ms frames; the same work on a `Timer` had no stall, which
  narrows the cause to the first `RequestAnimationFrame` calls specifically, not per-frame UI cost. Not investigated further.
- **Gates.** Four gates 5621 passed, 0 failed, 4 skipped in each shape (5589 on `main`, with F2/F3/F4/F5/F17 already merged,
  plus the 32 new); the API-diff gate reports no new gaps; ControlGallery builds.

Effect on this app: milestone 2's motion (the beacon's blink/breathe/sweep/spin, section 8.6) and page transitions
(`PageHost`, section 8.5) can use this instead of a hand-rolled timer loop, once released. Nothing in this repository
depends on it yet.

## Register item F17 (bundled fonts through CSS, #270)

Spike S2 (`docs/spikes.md`) found that CSS `font-family` did not resolve a font registered with `PrivateFontCollection`, on Headless
and on the Android emulator, while the API did. Open as majorsilence/Majorsilence.Forms#296 (closes #270), from a git worktree
`../Majorsilence.Forms-f17` on branch `fonts-css-private-family`.

- **Fix.** `ThemeCssValues.GetTypeface` now asks the private font registry first, in list order and ahead of its cache, and an
  earlier family the system really has still wins. Both CSS routes (the `--ui-font` token and a `Form { font-family }` rule) go
  through that one function, so one change fixed both.
- **Tests, failing first.** 9 tests in `ThemeCssPrivateFontTests`: 7 failed before the fix ("expected Caladea, actual Noto Sans",
  and different rendered bytes for the rule tests) and the 2 that passed are the regression guards. Four mutations of the fix (never
  let an earlier system family win, consider only the first family, let a cached fallback hide a later registration, treat every
  family as installed) were each caught by the intended test, and the fix was restored byte for byte. The tests use the framework's
  bundled Caladea, so no font file was added.
- **Docs.** The CSS reference sentence ("the first family *installed*") is corrected in `ThemeCssReference` and regenerated, and
  `docs/theming.md` gains "Bundling a font" with the embedded-resource recipe.
- **Gates.** Four gates 5523 passed, 0 failed, 4 skipped in each shape, rerun after rebasing onto the merged #293 and #294 (5514 on
  `main` plus the 9); the API-diff gate reports no new gaps.

Not covered, and said so in the docs: a real device, iOS, Android assets or an iOS bundle as the byte source (only an embedded
resource was run), and more than one weight. Register the font **before** loading the theme; a theme whose tokens were already
resolved is not re-applied automatically, which is stated as guidance and not tested.

The acceptance criterion "an Android device" is met only on the emulator, and the app-side shim (register with `AddMemoryFont`, set
`Font` in code for standard controls) is not needed yet because no view uses a bundled font. Still to do, by the owner: review, merge
and release, then this app names its typeface in the theme CSS.

## Register item F4 (binding docs, trimming and AOT, #266)

Open as majorsilence/Majorsilence.Forms#297 (closes #266), on branch `docs-binding-trimming`. Four gates pass (5514 tests, 0 failed,
4 skipped, in all four shapes, rerun after rebasing onto the merged #293 and #294), the API-diff gate reports no new gaps, and the
NativeAOT smoke publishes with no IL warnings and runs.

- Five statements that said binding is a stub were corrected: the two the issue names (a `Directory.Packages.props` comment and the
  ControlGallery MVVM sample) and three more, of which two were public XML docs (`Control.DataBindings`, `Binding`) and one a
  note in `WindowBase.cs`.
- `docs/backends.md` gains "Binding and trimming".
- `tests/Majorsilence.Forms.AotSmoke` now binds a view model to a `Label` and a `TextBox` in both directions, with a root
  descriptor. Removing any one entry breaks the native binary, and the failures are the ones in the #290 evidence above.

How it was measured matters: my first two ablation runs were void. One carried an unsupported `dotnet publish` switch and ran the
stale binary after the publish failed; the other changed only the descriptor, and ILC's up-to-date check does not see a
descriptor's contents, so it re-ran nothing. Both were caught because every binary had the same size and hash, and the results
reported here come from runs that forced a recompile and recorded size and hash per variant. Only `Text` on a `Label` and a `TextBox` is
covered; other properties, derived-type properties and `PublishTrimmed` (as opposed to NativeAOT) are not.

This app still wires view models by hand (`CLAUDE.md`). The rule can relax once F4 and #290 are released and a device build proves
binding with roots, not before.

## Register item F18 (colour emoji via VARIATION SELECTOR-16, #271/#281)

Merged as majorsilence/Majorsilence.Forms#302 (closed #271, evidence toward #281), branch `emoji-variation-selector`.

- **What it is.** RichTextKit resolves one typeface per `Style` (a run), not per character, so it could not notice that one
  codepoint inside a run was asking for a different presentation. `TextMeasurer.CreateTextBlock` now looks for a base character
  followed by VARIATION SELECTOR-16 (U+FE0F, emoji presentation) and splits it into its own run, resolved through a small
  per-platform list of known colour emoji font names (Apple Color Emoji, Segoe UI Emoji, Noto Color Emoji) instead of the run's
  normal style face, falling back to a hinted `SKFontManager.MatchCharacter` search for anything else installed. VARIATION
  SELECTOR-15 (U+FE0E, text presentation) still gets its own run — so the invisible selector is never measured as a stray glyph
  — but keeps the plain face.
- **The case that exposed it.** WARNING SIGN (U+26A0): most text fonts, DejaVu Sans included, already ship a plain monochrome
  triangle for it, so a font-coverage check alone never noticed anything was wrong — "⚠️" drew as a grayscale outline instead of
  the coloured triangle a phone's own text field shows for the same string.
- **A real macOS gap, found by running the tests on real CI, not assumed.** The first version resolved the emoji face purely
  through `SKFontManager.MatchCharacter(null, ["und-Zsye"], codepoint)` — the documented "give me the emoji one" hint. On a
  real macOS CI runner that resolved to **"Hiragino Sans"**, an ordinary CJK text font, not Apple Color Emoji — even with Apple
  Color Emoji installed and listed by the same font manager. CoreText silently ignores the `und-Zsye` script hint; Windows
  (DirectWrite) and Linux (fontconfig) both honour it correctly. Fixed by trying a short list of known emoji font family names
  directly first (the same pattern this file's own cross-platform substitution table already uses, for the same reason: a
  platform API that doesn't reliably answer "which face has X" is worked around with a known-name list instead), falling back
  to the hint only if none of those names are installed.
- **A second, separate macOS gap, found the same way — real, evidenced, and out of scope to fix here.** Even with the correct
  face now selected (proven at the coverage level), Topten.RichTextKit's `TextBlock.Paint` still rasterises Apple Color Emoji's
  `sbix` colour table as a *monochrome* shape on macOS specifically — a Skia/CoreGraphics colour-glyph rendering limitation in
  a third-party dependency ([mono/SkiaSharp#3244](https://github.com/mono/SkiaSharp/issues/3244) is the same class of bug),
  living inside RichTextKit's and Skia's own glyph rasteriser, not this repo's code. The pixel-level test is honest about this:
  strict colour-ink assertion on Linux and Windows, where it demonstrably renders in colour, and a "something was drawn, not
  silently dropped" check on macOS. No Majorsilence.Forms framework issue was filed for this one — it isn't the framework's
  own code, and there's nothing in this repo's control to fix.
- **Also found and fixed along the way: a hardcoded test font.** The original tests assumed "DejaVu Sans" was installed, which
  is true on the `ubuntu-latest` CI runner but not on `windows-latest` or `macos-latest`. `SKTypeface.FromFamilyName` never
  returns null, so every test in the file silently resolved to an unrelated substitute face there and failed on the setup
  assertion, before ever reaching the actual emoji logic. Fixed by discovering whichever installed font actually has a plain,
  monochrome U+26A0 glyph (and does not also cover the regression guard's U+1FAE0 MELTING FACE) rather than assuming one name;
  confirmed by running the failure on real CI, then fixing it and confirming green.
- **Tests.** 14 in `EmojiVariationSelectorTests`: `FontSubstitution.SplitByCoverage` run-splitting (plain-face-unaffected,
  switches-to-emoji-face, selector-never-starts-its-own-run, VS15-keeps-plain-face, an unambiguous emoji needing no selector at
  all, mid-string and trailing placement, a PUA codepoint no installed font covers falling back exactly the way the ordinary
  no-selector path does on that same platform — CoreText's own fallback for "nothing covers this" is its own `.LastResort`
  box-glyph face rather than `null`, unlike Linux/Windows, so the test compares against the platform's own behaviour rather
  than assuming one universal answer), `TextMeasurer.CreateTextBlock` run-level assertions (typeface count and total covered
  length, so a run silently dropped at either end of the string is caught), and two pixel-level tests proving the warning sign
  renders monochrome alone and with real colour ink (or, on macOS, at least a real glyph) once the selector is appended.
- **A mutation-testing false lead, resolved.** One deliberate mutation (forcing the "no selector" branch of an `if runStart==0
  .. else if runStart<text.Length ..` pair to never run) appeared to survive against a correctly-populated `TextBlock`, which at
  first looked like a caching or stale-build artefact. It was neither: for the no-selector case `runStart` stays `0` for the
  whole method, so the `else if` branch's own guard (`0 < text.Length`) is also true, and `text.Slice(0)` is span-identical to
  `text` — the two branches are provably equivalent for that input, an equivalent mutant rather than a real gap. Forcing *both*
  conditions false does produce an empty `TextBlock` and is correctly caught, confirming the test suite itself needed no change.
- **Gates.** Four gates pass (5665 tests, 0 failed, 4 skipped, in all four shapes); the API-diff gate reports no new gaps.
  `fonts-noto-color-emoji` added to CI's Linux font-install step alongside the existing `fonts-noto-cjk`, so CI itself can
  exercise the emoji path rather than only the Windows/macOS runners that ship a colour emoji face already. Full CI matrix
  (Linux, Windows, macOS, Android, iOS, and every sample/pack job) green after the two real fixes above.

Effect on this app: alert alerts and interpreter copy that include emoji (PLAN.md 8.10 copy table) will render with the correct
coloured presentation once this is released, on every platform except macOS's own colour-glyph rendering — where the character
still draws, correctly chosen, just not in colour until the upstream Skia/RichTextKit gap is fixed.

## Register item F8 (mobile audio for the existing API, #272)

Merged as majorsilence/Majorsilence.Forms#303 (closed #272, evidence toward #170), branch `mobile-audio`.

- **What it is.** `SoundPlayer` and `SystemSounds` were public but played only through `NativeAudio`, which spawns a desktop OS
  utility — silent on mobile by design. A new optional backend capability, `Backends.IAudioBackend`, gives them an in-process
  path to try first, falling back to `NativeAudio` (or silence) whenever it answers `null`, exactly as if the interface were
  not implemented at all. Android plays through `MediaPlayer` tagged with notification-stream `AudioAttributes`; `PlayLooping`
  loops natively via `MediaPlayer.Looping` instead of the desktop respawn trick. iOS plays through `AVAudioPlayer` on an
  `Ambient` audio session (respects the silent switch) for files, and `AudioToolbox.SystemSound` (Apple's own bundled
  system-sound bank) for the five stock names.
- **Two real iOS compile bugs found by CI, fixed the same way as F7's.** `AVAudioSession.SetCategory` has no
  `(AVAudioSessionCategory, out NSError)` overload — the compiler resolved that shape against the `(NSString, out NSError)`
  overload instead and rejected the enum argument; fixed with the 3-arg overload that takes an explicit (empty)
  `AVAudioSessionCategoryOptions`. Separately, a doc-comment `cref` to the Android-only backend type did not resolve in an
  iOS-only compile (`CS1574`); replaced with plain text. Both found by a real CI failure, not guessed, and both now green.
- **Verified for real** on the `alertbuddy-phone` Android emulator: a `Gallery.Android` change
  (`GalleryApplication.RunAudioSmokeTest`) plays a bundled test tone and a system sound through the real backend on every
  launch and logs one PASS/FAIL line; `android-smoke-test.sh` now fails the CI job if that line is missing or reports FAIL —
  a real, repeated-on-every-PR check going forward, not a one-off manual run. iOS is written from Microsoft's published
  dotnet/macios API docs but not run — no iOS host or simulator available.
- **Gates.** Four gates pass (5672 tests, 0 failed, 4 skipped, in all four shapes); the API-diff gate reports no new gaps.
  Full CI matrix green after the two iOS fixes.

Effect on this app: the alarm sound and the warning/alert cues (PLAN.md's audio playback needs) can now actually be heard on
an Android phone or an iPhone, not just on desktop, once this is released — the gap register item F9 (richer audio: volume,
looping usage, alarm stream) builds on directly.

## Register item F9 (richer audio: volume, loop, usage, overlap, #273)

Merged as majorsilence/Majorsilence.Forms#304 (closed #273), branch `audio-player-usage`.

- **What it is.** `Media.AudioPlayer`, SoundPlayer's richer sibling: `Volume` (clamped 0–1), `Loop`, `Usage` (`Effect`,
  `Notification`, `Alarm`, `Media`), a `Completed` event, `IsSupported`, and overlapping playback — repeated `Play` calls on
  one instance do not stop an earlier one, matching how rapid UI sound effects and alarm-style siren cues actually need to
  behave, unlike `SoundPlayer`. Built on F8's `IAudioBackend` seam with a third member, `PlayTrack`; `Usage.Alarm` maps to
  Android's `USAGE_ALARM` (its own volume stream, audible with media volume down) and an iOS `Playback` session (overrides the
  silent switch) — the case the whole class exists for. `IsSupported` is `true` only on Android and iOS: none of live volume,
  real audio-stream routing, or a genuine completion event map onto NativeAudio's process-spawn approach, and desktop already
  has `SoundPlayer` for the simple case.
- **Mutation-tested, with two confirmed-equivalent survivors.** Every routing/lifecycle assertion was checked against a
  deliberately broken version of the source line. Two mutations (skipping the `Completed` handler's list-removal; skipping
  `Stop`'s list-clear) survived, and were confirmed genuinely equivalent rather than gaps: `Dispose` is idempotent, so neither
  omission changes anything observable through the public API, only internal list hygiene nothing asserts on.
- **Verified for real on Android — by CI, not the local emulator this time.** A local run hit a genuine `system_server` crash
  mid-session (confirmed via logcat, caused by sustained local memory pressure on the dev machine, unrelated to this change),
  so `GalleryApplication.RunAudioPlayerSmokeTest` and `android-smoke-test.sh`'s matching `F9_AUDIOPLAYER_SMOKE` check — wired
  in as a permanent, repeated-on-every-PR verification, the same mechanism that gave F8 a real local pass earlier — got its
  first real run on PR #304's own `android-smoke` CI job instead, on the first push: PASS.
- **Gates.** Four gates pass (5683 tests, 0 failed, 4 skipped, in all four shapes); the API-diff gate reports no new gaps.

Effect on this app: the milestone 2 alarm/siren behaviour (a looping cue audible even with the phone's media volume turned
down) becomes possible once this lands — `SoundPlayer` alone cannot express "play on the alarm stream."

## Register item F10 (app lifecycle events, #274)

Merged as majorsilence/Majorsilence.Forms#305 (closed #274, evidence toward #170), branch `app-lifecycle-events`.

- **What it is.** `Application.Suspended`/`Resumed` (new), and `Form.Activated`/`Deactivate` now firing on single-view hosts
  (Android, iOS, browser) — every *other* window host already wired these from its own real activation signal, so the
  single-view host was the one gap. `AvaloniaPlatformBackend.HookApplicationLifecycle` (idempotent, wired once from
  `Initialize`/`InitializeAsync`) reaches Avalonia's `IActivatableLifetime` and forwards its `Background`-kind transitions to
  both `Application.RaiseSuspended`/`RaiseResumed` and, on the single-view root host, `WindowBase.OnBackendActivated`/
  `OnBackendDeactivated`.
- **A real correction found by inspecting the actual shipped assembly, not guessed.** The issue itself flagged
  "`Avalonia.Android` contains `IActivatableLifetime`... **(verify)**" — and the first version's straightforward reading of
  that (`Application.Current.ApplicationLifetime as IActivatableLifetime`, the same pattern F7/F8's own optional-capability
  checks use) silently never fired. Rather than guess further, inspected the real `Avalonia.Android.dll` (12.1.1) via
  `MetadataLoadContext`: `Avalonia.Android.ApplicationLifetime` implements only `IActivityApplicationLifetime`/
  `IApplicationLifetime`/`ISingleViewApplicationLifetime`, never `IActivatableLifetime` at all. That capability turned out to
  be a *separate* object (`Avalonia.Android.Platform.AndroidActivatableLifetime`), reached instead through
  `Application.TryGetFeature` — Avalonia's own optional-platform-capability lookup, a mechanism this register work hadn't
  needed before F10.
- **`RaiseSuspended`/`RaiseResumed` had to be made `public`, not `internal`.** Adding them tripped `UnraisedEventBaselineTests`
  (a real static-analysis gate flagging a declared event whose raiser is unreachable within its own assembly) — the only
  caller lives in `Majorsilence.Forms.Avalonia`, a different assembly, invisible to that gate at `internal` visibility. Fixed
  by making both public, the same reason the existing `RaiseIdle` already has a public overload.
- **Verified for real on Android — by CI, on the first push.** `GalleryApplication` logs `F10_LIFECYCLE:
  Suspended`/`Resumed`/`Form.Activated`/`Form.Deactivate`; `android-smoke-test.sh` now sends `KEYCODE_HOME` and relaunches the
  app, failing the job if any of the four lines is missing after a real background/foreground cycle. Two separate real local
  emulator failures this session (a `system_server` crash, then a wedged `adbd`) made local verification of this specific
  check inconclusive — both confirmed via logcat/process state to be infrastructure failures, not app issues — so this is the
  second register item in a row (after F9) where CI's own dedicated runner gave the real, definitive pass this session's local
  emulator could not. iOS is written from the same `IActivatableLifetime` contract but not run — no host available.
- **Gates.** Four gates pass (5685 tests, 0 failed, 4 skipped, in all four shapes); the API-diff gate reports no new gaps.

Effect on this app: the milestone 2 alarm takeover screen (section 8.6) and any future "app came back from the background"
handling (re-checking notification state, refreshing the alert feed) now have a real signal to hook, on every platform this
app targets, not just desktop.

## Register item F11 (back button, #275)

Merged as majorsilence/Majorsilence.Forms#306 (closed #275), branch `back-button`.

- **What it is.** `WindowBase.BackRequested`/`RaiseBackRequested` (new): a cancellable event for the platform back
  button/gesture, real on Android and iOS with no desktop equivalent to raise it from. Placed on `WindowBase` itself, not just
  `Form`, so `PopupWindow` has it too — the acceptance criterion is specifically "closes a sheet without leaving the app", and
  `PopupWindow` (a dropdown, a context menu, a filter grid) is exactly what a "sheet" is here. `AvaloniaPlatformBackend.RaiseBackRequested`
  prefers `Application.ActivePopupWindow` (the same "which window is really active right now" check
  `Application.ScheduleClosePopupsOnDeactivate` already uses), so an open sheet gets the back-press before the main screen.
- **Not automatic — a host app has to forward it.** Unlike F10's `HookApplicationLifecycle`, `Avalonia.Android.AvaloniaActivity.BackRequested`
  is declared directly on the Activity class, and nothing in `Majorsilence.Forms.Avalonia` can discover "the current Activity"
  generically (confirmed by inspecting the real `Avalonia.Android.dll`, same technique as F10's finding). A host app's own
  `MainActivity` (already required to subclass `AvaloniaMainActivity` and carry an AppCompat theme, #288) forwards its own
  `BackRequested` to `AvaloniaPlatformBackend.RaiseBackRequested` — one added line, the same shape `Application.RunAndroid`
  already requires.
- **A second naming finding, found by reading the gate's own source, not by guessing.** The new raiser was first named
  `OnBackendBackRequested`, matching F10's `OnBackendActivated`/`OnBackendDeactivated` convention, and made `public` (the F10
  fix for the same `UnraisedEventBaselineTests` gate) — but it still failed. Reading `StubSurfaceScanner`'s actual
  `NoNewUnraisedEvents` implementation (not the other, unused deep-reachability scanner also in that file) found the real
  rule: a public method only counts as a safe "definitely a real entry point" bypass when its name does *not* start with
  `On`, regardless of visibility — an `On`-prefixed method is treated as an internal framework convention (a backend
  overriding a hook), not a cross-assembly entry point. Renamed to `RaiseBackRequested` (matching `RaiseIdle`/`RaiseSuspended`/
  `RaiseResumed`) and the gate passed with no other change.
- **Verified for real on Android — by CI, on the first push.** This session's local Android emulator infrastructure had
  already failed twice earlier in the day (a `system_server` crash, then a wedged `adbd`), and swap was still fully exhausted
  when this item was ready to verify, so local verification was not attempted this time — the same judgment call F9 and F10
  already made. `Gallery.Android` shows a small `PopupWindow` ("sheet") right after `MainForm.Shown`; `android-smoke-test.sh`
  now presses `KEYCODE_BACK` twice, checking the foreground activity via `dumpsys` rather than process liveness (Android can
  leave a finished activity's process resident). PR #306's `android-smoke` job passed on the first push, with the exact
  confirming lines: `"F11 back button (popup open): cancelled, app still foreground"` and `"F11 back button (no popup):
  unhandled, app exited normally"`.
- **Gates.** Four gates pass (5690 tests, 0 failed, 4 skipped, in all four shapes); the API-diff gate reports no new gaps.

Effect on this app: a sheet or dialog (the milestone 2 alarm takeover screen's dismiss action, any future filter/detail sheet)
can now close itself on the Android/iOS back button/gesture instead of the press falling through and exiting the whole app —
this is the last of the four Android-delivery register items from PLAN.md 11.4 before F13 (haptics) and F14 (notifications).

## Register item F13 (haptics, #276)

Merged as majorsilence/Majorsilence.Forms#307 (closed #276), branch `haptics`.

- **What it is.** `Haptics.Tap`/`Impact`/`Vibrate`/`IsSupported` (new): real on Android (`Vibrator`, driven by `VibrationEffect`)
  and iOS (`UISelectionFeedbackGenerator`/`UIImpactFeedbackGenerator` for `Tap`/`Impact`; `Vibrate` triggers iOS's own
  fixed-length system buzz, since no public UIKit API takes an explicit duration), `false` everywhere else — explicitly
  including Headless, unlike F8/F9's audio (real and test-hooked under Headless too): haptics has no desktop/browser
  equivalent worth a "supported but does nothing" middle state, so `IHapticsBackend` is only declared on Android/iOS at all,
  not implemented everywhere with a null body.
- **The `android.permission.VIBRATE` manifest entry ships with the framework, not with each app** — an assembly-level
  attribute on `Majorsilence.Forms.Avalonia` merges it into any consuming app's manifest automatically, confirmed by
  grepping the built `Gallery.Android` APK's own merged manifest.
- **Two real build failures, both caught by CI, neither guessed.** Android: the platform-compat analyzer (`CA1416`) flagged
  `VibrationEffect.EffectClick`/`EffectHeavyClick` (API 29+ fields, this project floors at API 24) as reachable, because the
  version-guard was one call frame away from the field access itself; fixed by moving the guard to wrap the field access
  directly. iOS: `UIImpactFeedbackGenerator (UIImpactFeedbackStyle)` turned out to be obsoleted from iOS 17.5 in favour of a
  view-scoped factory this backend has no view reference to feed — the old constructor still works, so the warning is
  suppressed at that one call site rather than the API avoided.
- **Verification is a real, permanent gap by the acceptance criterion's own words: "emulators have no vibrator."**
  `GalleryApplication.RunHapticsSmokeTest` and `android-smoke-test.sh`'s `F13_HAPTICS_SMOKE` check (passed on Android via CI)
  only prove the plumbing — `IsSupported` true, `Tap`/`Impact`/`Vibrate` all run with no exception — not that anything was
  actually felt. iOS compiles clean (CI-confirmed after the fix above) but was not run on any simulator or device. **This
  register item still needs a human on a real Android phone and a real iPhone** before it can be considered fully verified.
- **Gates.** Four gates pass (5693 tests, 0 failed, 4 skipped, in all four shapes); the API-diff gate reports no new gaps.

Effect on this app: `IHaptics`'s Android/iOS implementation (PLAN.md 6.4, Appendix A) can now be a thin adapter over the
framework's own `Haptics`, matching `ISoundPlayer`'s adapter over `AudioPlayer` — but the milestone 2 alarm takeover's
vibration cue is not something to ship on trust: it needs a real-phone check before that flow is called done.

## Register item F14 (notifications, Android half, #277)

Merged as majorsilence/Majorsilence.Forms#308 (Android half of #277 — desktop `NotifyIcon.ShowBalloonTip` and iOS follow
separately), branch `local-notifications-android`.

- **What it is.** `Notifications.LocalNotifications` (new): `RegisterChannel`/`RequestPermission`/`Show`/`Cancel`/
  `IsPermissionGranted`/`IsSupported`, real on Android (`NotificationManagerCompat`/`NotificationChannelCompat`, AndroidX
  Core — already a transitive dependency via Avalonia.Android's own AppCompat requirement, #288, no new package needed),
  `false`/no-op everywhere else including Headless. Two capabilities neither F8–F13 needed: a live `Activity` for
  `RequestPermission` (API 33+ only) and the host's own `Intent` for the tap callback, both reached by extending F11's
  "host app forwards to the framework" idiom (`MainActivity.OnCreate` registers itself via `RegisterAndroidActivity`;
  `OnCreate`/`OnNewIntent` forward the Activity's own `Intent` to `ReportAndroidIntent`, which raises `Tapped` without the
  host ever needing to know the extra key itself). The tap `PendingIntent` targets `PackageManager.GetLaunchIntentForPackage`
  — the app's own launcher activity, found generically, no per-app registration needed.
- **A five-round CI debugging chase, every one a real bug this session did not guess at, not flakiness (one exception: a
  single unrelated `F8_AUDIO_SMOKE` flake, confirmed by a clean rerun).** In order: (1) `StoredOnlyPropertyBaselineTests` —
  the backend interface had to take each `NotificationChannel`/`LocalNotification` field individually rather than the
  object itself, the same shape `IAudioBackend.PlayTrack` already uses, since the gate only scans the core assembly and the
  real field readers lived in a different, Android-gated one; (2) `CA1416` — `PendingIntentFlags.Immutable` needs API 23+
  against this project's API 21 *library* floor; (3) the notification silently never posted — neither `Gallery.Android` nor
  the project's own Android template declares an app icon at all, so `ApplicationInfo.Icon` was `0` and
  `NotificationManager.notify` threw `IllegalArgumentException` inside a swallowed `catch`, fixed with a fallback to
  Android's own `Resource.Drawable.IcDialogInfo`; (4) the full-screen intent was silently stripped by Android 14+ without
  the normal, declare-only `USE_FULL_SCREEN_INTENT` permission; (5) the CI script's own tap-replay check needed
  `--activity-single-top` (an already-foreground task is otherwise a no-op for `am start`) *and* explicit activity
  resolution via `adb shell cmd package resolve-activity --brief` (the same command this repo's own CLAUDE.md documents),
  since combining that flag with package-only resolution failed outright.
- **Verified for real on Android — by CI, after all five fixes.** `android-smoke-test.sh` posts an ongoing, full-screen-intent
  notification on a High-importance channel and independently confirms via `dumpsys notification` that it actually posted
  (channel, title, full-screen intent all present — not just that `Show` didn't throw), then replays the exact launch intent
  a real tap would send and confirms `LocalNotifications.Tapped` fires with the right id. `Ongoing`/`Sound` are covered by
  the fake-backend unit tests (`LocalNotificationsTests`, 16 tests) instead of a second real-device signal — simple boolean
  pass-throughs already asserted precisely there.
- **Gates.** Four gates pass (5709 tests, 0 failed, 4 skipped, in all four shapes); the API-diff gate reports no new gaps.

Effect on this app: `IAlertNotifier` (PLAN.md Appendix A) and the notification half of the alarm takeover flow can now be a
thin adapter over the framework's own `LocalNotifications` — channels, importance and a full-screen intent are exactly what
an alarm-style "tell a grown-up now" notification needs, and this is real, CI-verified behaviour on Android, not an
untested surface. iOS and desktop toasts remain open for a later framework PR before this register item is fully closed.

## Register item F12 (keep screen awake, #278)

Merged as majorsilence/Majorsilence.Forms#315, branch `keep-screen-awake`. First item of PLAN.md 11.4's batch 5 (tablet,
bedside mode, iOS) — the owner chose to do all five platforms in one PR rather than split it, since the API itself is a
single bool property, not a subsystem the way F14 was.

- **What it is.** `Application.KeepScreenAwake` (new): real on Android (`Window.AddFlags`/`ClearFlags (WindowManagerFlags.KeepScreenOn)`,
  reusing the same registered Activity F14 already established), iOS (`UIApplication.IdleTimerDisabled`), and — new territory
  for this framework — all three desktop OSes via `Backends.DesktopKeepAwake`, a direct sibling of F7's `DesktopReducedMotion`:
  Windows `SetThreadExecutionState`, a macOS IOKit power assertion (`IOPMAssertionCreateWithName`, `PreventUserIdleDisplaySleep`)
  via raw CoreFoundation/IOKit P/Invoke (no Xamarin.Mac binding needed), and Linux `systemd-inhibit --what=idle:sleep ... sleep
  infinity` held for exactly as long as that placeholder process runs (no D-Bus cookie parsing needed).
- **Headless implements this one for real**, unlike F13/F14's `IsSupported false` there — a plain settable field, since
  `KeepScreenAwake` is a stateful property an app's own view-model code turns on and off (a bedside/status-display screen),
  exactly what a view-model test needs to assert against, matching the issue's own "fake-backend tests" wording.
- **Verified for real on all five platforms, on the first CI push — no debugging cycles needed this time**, a first for this
  register-item batch. Android: `MainActivity`'s own smoke test sets it true then false on a real Activity, confirmed via
  `android-smoke-test.sh`. Linux: a unit test calls the real (non-injectable) `Set` directly, genuinely spawning and killing a
  real `systemd-inhibit` child process — confirmed locally with `pgrep`/`pkill`. Windows and macOS: the *same* real-`Set` test
  also runs on CI's `build (windows-latest)`/`build (macos-latest)` jobs, which run the full test suite rather than just a
  compile check — both passed cleanly, for real P/Invoke correctness, not just "written from the documented API." iOS:
  compiles clean via CI but not run on a simulator or device, the same honest gap F13/F14 already record.
- **A real finding, caught mid-session, not in the shipped code.** Mutation-testing `Set`'s `IsEnabled` bookkeeping broke the
  real-Linux test mid-run and left a genuine `systemd-inhibit` process running on the dev machine, because the test's own
  cleanup trusted `IsEnabled`'s (now-wrong) tracked value to decide whether a real disable call was even needed. Caught with
  `pgrep`, killed with `pkill`, and fixed by making the test's cleanup force a real disable unconditionally rather than trust
  tracked state — a robustness lesson about test cleanup itself, not a bug in `DesktopKeepAwake`.
- **Gates.** Four gates pass (5793 tests, 0 failed, 4 skipped, in all four shapes); the API-diff gate reports no new gaps.

Effect on this app: `IKeepAwake` (PLAN.md Appendix A) can now be a thin adapter over the framework's own `KeepScreenAwake` —
milestone 5's bedside mode (a status display that must not sleep) is what this exists for, and unlike F13/F14 this one is
real, CI-verified behaviour on every platform the app could plausibly run on, not just Android.

## Register item F19 (automation: custom control state, #279)

Merged as majorsilence/Majorsilence.Forms#316, branch `automation-custom-control-state`.

- **What it is.** `Automation.IAutomationStateProvider` (new): lets a custom-painted control publish its own `Value` and
  extra `State` (a string dictionary) to the automation tree — role and name already worked for any control via the
  existing `AccessibleRole`/`AccessibleName` WinForms-compat properties, but value and extra state had no such home. Each
  state entry becomes its own `state-{key}` attribute in the automation XML page source (independently XPath-queryable,
  not one opaque blob) and is readable through WebDriver's `getAttribute` under the same name.
- **Verified with a real end-to-end WebDriver test, not just in-process tree assertions.** A test starts an actual
  `WebDriverServer`, finds a sample "beacon indicator" custom control by XPath, and reads its `state-level`/`state-status`
  through the real HTTP `getAttribute` endpoint — proving the framework's own documented promise ("`getAttribute` exposes
  the same fields as the XML page source") holds for the new attributes too, not just the fixed built-in set.
- **Gates.** Four gates pass (5797 tests, 0 failed, 4 skipped, in all four shapes); the API-diff gate reports no new gaps.
  Backend-neutral, pure C# — no platform-specific code — so CI's `build (ubuntu-latest)`/`build (windows-latest)`/
  `build (macos-latest)` jobs (full test suite) were the real verification, all green on the first push.

Effect on this app: any custom-painted status control (the beacon/alert-level display the whole app is built around) can
now publish its real level and state to the automation tree — the exact "status widget showing a level" example the
framework issue itself used, drawn directly from what this app needs. M3's screens and flows can be driven and asserted
through `AutomationSession`/WebDriver the same way a built-in control already can, not just visually inspected.

## Register item F16 (secure storage, #283)

Merged as majorsilence/Majorsilence.Forms#326, branch `secure-storage`. First item shipped in the new
`Majorsilence.Forms.Essentials` package — the owner's own packaging decision for haptics, notifications, text to speech
and secure storage (PLAN.md section 11.5), now actually followed for the first time since F13/F14 shipped in core instead
(recorded there as an accepted inconsistency).

- **What it is.** `Majorsilence.Forms.Essentials.SecureStorage.GetAsync`/`SetAsync`/`Remove`/`IsSupported` (new): a
  password or token in the platform's own secure store, never a plain file. Real on Android (an AES-256/GCM key generated
  inside the AndroidKeyStore encrypts each value, stored in a private-mode `SharedPreferences` file; `IsSupported` is a
  real API-23 check, since this project's own library floor is API 21), iOS (`Security.SecKeyChain`/`SecRecord`, the
  Keychain), and all three desktop OSes (Windows Credential Manager, macOS Keychain Services via the older
  `SecKeychainAddGenericPassword` C API, Linux via `secret-tool`). Not routed through the `Backends.Platform` seam
  Haptics/LocalNotifications/KeepScreenAwake use — which OS credential store exists has nothing to do with which UI
  backend is active — so `Majorsilence.Forms.Essentials` has no reference to core `Majorsilence.Forms` at all.
- **Linux's `IsSupported` is a real, load-bearing false, not a placeholder.** A missing `secret-tool` binary or no keyring
  daemon running (true of this session's own sandbox and, it turned out, of the framework's own Linux CI runner too — the
  desktop test suite ran there for real and confirmed it) means secrets genuinely cannot be stored securely, so this
  never falls back to a plain file the way a lesser implementation might have.
- **Two real bugs, both caught by CI, neither guessed.** (1) `KeyGenParameterSpec` (API 23+) tripped `CA1416` even behind
  a version guard — a negated early-return guard, and a guard through a named constant rather than the literal, both
  still failed the analyzer; only a literal-valued, positively-wrapping `if (OperatingSystem.IsAndroidVersionAtLeast
  (23))` satisfied it, one step stricter than F13's own CA1416 fix needed. (2) The real one: `android-smoke`'s own
  `F16_SECURESTORAGE_SMOKE` check failed for real on the first CI push — "expected the stored value back, got ''" — with
  no exception anywhere in logcat, because every Android backend's catch block here is silent by design (matching
  Haptics/Notifications). A temporary debug-logging commit found the actual cause on the next CI run:
  `KeyStore.GetKey` returns the binding's `IKey`, and a plain C# `(ISecretKey)` cast on that managed peer throws
  `InvalidCastException` at runtime — .NET-for-Android's JNI interop needs `JavaCast<T>()` to re-wrap the same underlying
  Java object as a different bound interface, not a CLR cast. Fixed, and the debug logging reverted once the real cause
  was found, back to the same silent-catch shape every other backend already uses.
- **A real test gap, caught by mutation-testing the desktop dispatch logic.** Mutating away the short-circuit return
  after a matched macOS branch (so execution falls through to also check `isLinux`) passed every existing test, because
  the original test's throwing `isLinux` predicate had its own exception silently swallowed by `Dispatch`'s `catch`.
  Fixed by tracking whether `isLinux` was called at all, not asserting on a side effect that never happens if the mutant
  is present.
- **Gates.** All four pass (5901 tests, 0 failed, 4 skipped, in all four shapes, after also merging in unrelated
  concurrent work from the same repo); the API-diff gate reports no new gaps (a net-new capability, not a WinForms-parity
  surface, the same as F12/F13/F14). Verified for real on every row: Android via `MainActivity.RunSecureStorageSmokeTest`
  and `android-smoke-test.sh`'s `F16_SECURESTORAGE_SMOKE` check (round-trips and confirms `Remove` actually removes it);
  Linux via `SecureStorageTests` running the real, non-injected desktop backend on this session's own sandbox and on
  CI's Linux runner, both reporting `IsSupported` false and every member degrading gracefully; Windows and macOS via the
  same test suite running for real on CI's `build (windows-latest)`/`build (macos-latest)` jobs (full test suite, not
  just a compile check); iOS compiles clean via CI's `ios`/`sample-ios` jobs but was not run on a simulator or device —
  the same honest gap F12/F13/F14 already have for iOS.

Effect on this app: `ISecretStore`'s desktop implementation — currently `InMemorySecretStore`, TEMP-SHIM (F16), holding
the password/token only for the process's lifetime — can now become a thin adapter over `SecureStorage` once this
release is adopted, on every desktop OS except a Linux box with no keyring daemon running (which stays honest about it
rather than silently degrading to memory-only, unlike today's shim). Android and iOS get the same real secure storage
the moment their own heads wire it in.

## Register item F15 (text to speech, #282)

Merged as majorsilence/Majorsilence.Forms#329, branch `speech`. Second capability shipped in
`Majorsilence.Forms.Essentials`, alongside F16.

- **What it is.** `Speech.SpeakAsync`/`IsSupported` (new): reads a line aloud with the platform's own voice. Real on
  Android (`TextToSpeech`, its own async engine init shared across calls), iOS (`AVSpeechSynthesizer`), and all three
  desktop OSes via the owner's own "spawn an OS utility" policy: macOS's `say`, Linux's `espeak-ng`/`espeak`, Windows via
  a short PowerShell script over `System.Speech.Synthesis`. The text always travels over the spawned process's stdin on
  desktop, never a command-line argument, so nothing needs escaping. Cancellation kills the process (desktop) or calls
  the platform's own stop API. Not routed through the `Backends.Platform` seam, same reasoning as F16.
- **Two real Android build findings, neither guessed.** This binding's `UtteranceProgressListener` still only declares
  the deprecated string-only `OnError` abstract (no separate `OnError(string, int)` exists to implement instead), so an
  `[Obsolete]` override is required; and `Java.Util.Locale` itself is flagged obsolete from API 36 with no other way to
  build the value `SetLanguage` takes, suppressed at that one call site the same way F13's own iOS CA1422 finding was.
- **A real, much bigger finding along the way, not specific to Speech at all.** Getting this PR green on CI surfaced a
  pre-existing, repo-wide test-isolation gap: 74 test files (on top of 104 that already had it) were missing
  `[Collection ("Headless")]`, filed as majorsilence/Majorsilence.Forms#330 and fixed in the same PR. That fix alone did
  **not** actually resolve the flake that found it (`MvvmHelpersTests.The_default_dispatcher_runs_on_the_active_backends_ui_thread`
  failing intermittently on Windows/macOS CI, never Linux) — the test assembly already disables parallelization
  assembly-wide, so it was never a concurrency race. The real cause: `HeadlessRenderer.Use ()` only replaces the active
  backend if it isn't already `HeadlessPlatformBackend`, and that backend pins its own "UI thread" once per instance,
  never again — so the shared instance's UI thread stayed wherever the *first* test in the whole run happened to
  construct a window, and any later test's assertion of "am I on the UI thread" was down to whether xUnit's pooled
  worker threads happened to schedule it back onto that same physical thread. Fixed by giving that one test its own
  freshly-initialised backend, the same explicit-pin idiom an existing test (`InvalidatedEventTests.cs`) already used
  for the identical underlying reason. Recorded as a correction on #330, which stays open for the broader "any other
  test with the same footgun" gap.
- **Gates.** All four pass (5912 tests, 0 failed, 4 skipped); the API-diff gate reports no new gaps. Verified for real:
  Android via `MainActivity.RunSpeechSmokeTest`/CI's `android-smoke` (`F15_SPEECH_SMOKE`, passed first try once the
  Android-specific findings above were fixed); Linux via the real desktop backend on this session's own sandbox (no
  `espeak`/`espeak-ng` installed, `IsSupported` false, everything degrades gracefully, including under cancellation);
  Windows and macOS via the same test suite running for real on CI's own runners; iOS compiles clean but was not run on
  a simulator or device, the same honest gap F12/F13/F14/F16 already have.

Effect on this app: milestone 6's optional text-to-speech (an early reader hearing a line alongside seeing it) can now
be a thin adapter over `Speech`, on every platform the app runs on, once this release is adopted.

## Register item F23 (CSS `:active`, `:disabled`, `:focus` and `box-shadow`, #285)

Merged as majorsilence/Majorsilence.Forms#334, branch `css-states-and-shadow`. Owner decision recorded on the issue before
implementation (PLAN.md section 11.5, decision 3): yes for the three pseudo-classes and a hard, no-blur `box-shadow`; no
for gradients and images.

- **What it is.** `Control.CurrentStyle` now resolves `:disabled` > `:hover` > `:active` > `:focus` > plain `Style`
  (hover kept exactly where it already was, so nothing already themed changes), on the same three controls that already
  had `:hover` (`Button`, `LinkLabel`, `TrackBar`). `box-shadow` is a new control-rule property on any selector -- exactly
  `<horizontal-offset> <vertical-offset> <color>`, no blur/spread/`inset`, rejected with a diagnostic rather than silently
  dropped -- painted as a hard offset rectangle behind the control's own shape.
- **Four real CI gate failures found and fixed on top of the PR as received, none guessed.** (1) A new worked doc example
  was nested under its bullet point (2-space indent); the doc-example test's closing-fence scan only matches an
  unindented fence, so it silently swallowed the next section's heading into the "CSS" it tried to parse -- every other
  example in the doc is unindented, now this one is too. (2) The three new `Control` members (`StyleActive`,
  `StyleDisabled`, `StyleFocus`) had no `WindowBase`/`Form` counterpart and tripped the Control/window parity gate;
  baselined alongside the pre-existing `StyleHover` entry, same reasoning. (3) A new test asserted
  `Button.DefaultStyleHover.BackgroundColor` is null after loading a `:active`-only rule; it never is, by long-standing
  design -- `Button.cs` gives hover its own accent-coloured background directly in C#, independent of any theme CSS --
  confirmed by running the test alone, before touching anything else, and getting the identical value. Rewrote the
  assertion to check what it actually meant to: that the accent-coloured hover look survives an unrelated `:active`
  rule untouched. (4) A pre-existing diagnostics test still expected the message from before `:active` was a recognised
  pseudo-class at all; generalising pseudo-class support changed the part-specific message to something more precise
  ("a part only supports ':hover'"), and the old test's expectation hadn't been updated to match.
- **Gates.** All four pass (5938 tests after the fixes above, 0 failed, 4 skipped); the API-diff gate reports no new
  gaps. The WinForms/Avalonia theming-support docs (Windows-only generators) were hand-traced rather than run locally,
  the same constraint F17's own WinForms doc hit -- CI's own doc-sync tests are what actually proved them correct.

Effect on this app: once this release is adopted, a tactile pressed/disabled/focus look and a hard drop shadow are
available in CSS on `Button`, `LinkLabel` and `TrackBar` without any code-side styling.
