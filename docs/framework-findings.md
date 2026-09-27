# Framework findings

Every bug, gap, awkward API or documentation error found in Majorsilence.Forms while building this app (PLAN.md section 11.1,
policy 4). Each is filed in the framework tracker (`majorsilence/Majorsilence.Forms`, label `alert-buddy`) as soon as it is met
and listed on the tracking issue #287. This log is reviewed at every milestone.

Register items F1 to F24 (the planned work) are issues #263 to #286 and are tracked there, not here. This file is for what the
app **found** that was not in the register.

| Issue | Found | Symptom | Repro | App workaround | Status |
|---|---|---|---|---|---|
| #288 | M0, S1 | The Android head from the 26.3.0 template crashes on its first frame: `Theme.AppCompat` required | Generate with `--IncludeAndroid`, run on an API 36 emulator | `Resources/values/styles.xml` and `Theme = "@style/AlertBuddyTheme"` | Open |
| #289 | M0, S9 | A `TextBox` whose `Text` is assigned after being parented to a window-less panel is drawn at scale 1 (half size at 2, a third at 2.75), permanently | Headless at `MF_HEADLESS_SCALE=2`, variant C in the issue | Set `Text` in the initializer or after the form is shown | Open |
| #290 | M0, S9 | `DataBindings` silently does nothing for a missing or trimmed member; Android Release breaks reads (full trim) and write-back (default) | Spike on an emulator; three configurations in `docs/spikes.md` | Do not use `DataBindings`; helper wiring | Open |
| #291 | M0, F1 work | Custom `OnPaint` draws in device pixels, undocumented; the gallery sample ignores it | 10x10 `FillRectangle` at scale 2 covers 10x10 device pixels | Scale by `e.Scaling` in every custom control | Open |

Evidence added to existing issues:

- #266 (binding docs, trimming and AOT): the trimming evidence, and that the smoke test should bind in both directions.
- #281 (Android device shakeout): soft keyboard never appeared, safe-area behaviour, `BeginInvoke` result.
- #271 (colour emoji): renders on Android and Headless; iOS untested.

## Register item F1 (Graphics rounded rectangles, #263)

Implemented in `../Majorsilence.Forms` and open as majorsilence/Majorsilence.Forms#292 (closes #263). `Graphics.FillRoundedRectangle`
and `DrawRoundedRectangle` (`Rectangle`, `RectangleF` or x/y/width/height, one radius or four per-corner radii clockwise from the
top-left) and `GraphicsPath.AddRoundedRectangle`, with a shared geometry helper, 23 tests (pixel checks at scales 1 and 2, per-corner
order, the CSS scale-down rule, validation, overloads and path agreement) and a matrix row. All four CI gates pass (5502 tests, 0
failed), the API-diff gate reports no new gaps, and seven deliberate breakages each turned the intended tests red.

Still to do, by the owner: merge the PR, cut a release, then bump the pin here (`docs/framework-versions.md`). This app can use it
earlier through a locally packed build (PLAN.md 11.1, policy 8).
