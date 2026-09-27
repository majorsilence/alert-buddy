# Framework findings

Every bug, gap, awkward API or documentation error found in Majorsilence.Forms while building this app (PLAN.md section 11.1,
policy 4). Each is filed in the framework tracker (`majorsilence/Majorsilence.Forms`, label `alert-buddy`) as soon as it is met
and listed on the tracking issue #287. This log is reviewed at every milestone.

Register items F1 to F24 (the planned work) are issues #263 to #286 and are tracked there, not here. This file is for what the
app **found** that was not in the register.

| Issue | Found | Symptom | Repro | App workaround | Status |
|---|---|---|---|---|---|
| #288 | M0, S1 | The Android head from the 26.3.0 template crashes on its first frame: `Theme.AppCompat` required | Generate with `--IncludeAndroid`, run on an API 36 emulator | `Resources/values/styles.xml` and `Theme = "@style/AlertBuddyTheme"` | Fixed: majorsilence/Majorsilence.Forms#294 merged 2026-09-26, not yet released |
| #289 | M0, S9 | A `TextBox` whose `Text` is assigned after being parented to a window-less panel is drawn at scale 1 (half size at 2, a third at 2.75), permanently | Headless at `MF_HEADLESS_SCALE=2`, variant C in the issue | Set `Text` in the initializer or after the form is shown | Fixed: majorsilence/Majorsilence.Forms#293 merged 2026-09-26, not yet released |
| #290 | M0, S9 | `DataBindings` silently does nothing for a missing or trimmed member; Android Release breaks reads (full trim) and write-back (default) | Spike on an emulator; three configurations in `docs/spikes.md` | Do not use `DataBindings`; helper wiring | Open |
| #291 | M0, F1 work | Custom `OnPaint` draws in device pixels, undocumented; the gallery sample ignores it | 10x10 `FillRectangle` at scale 2 covers 10x10 device pixels | Scale by `e.Scaling` in every custom control | Open |

Evidence added to existing issues:

- #266 (binding docs, trimming and AOT): the trimming evidence, and that the smoke test should bind in both directions.
- #290 (binding fails silently): NativeAOT (ILC) evidence, 2026-09-26. Not keeping `Control.Text` makes `DataBindings.Add` throw;
  not keeping `Control.TextChanged` or a view-model property fails silently (reads work without the event, typed text never
  reaches the source). The smallest working `TrimmerRootDescriptor` is those members and nothing broader.
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
