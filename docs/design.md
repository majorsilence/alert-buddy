# Design notes: accessibility and motion

What the app does about accessibility and reduced motion today, and what it does not. Written from the code and the emulator runs,
not from a screen reader session: **nobody has run TalkBack or VoiceOver against this app**, so the "not covered" list below is
what is known to be missing, not a full audit.

## What is built in

- **State is never colour alone.** The buddy's face, a glyph and words all repeat the level (PLAN.md section 8.11).
- **Sizes.** Body text 18, nothing under 16, touch targets 48 and primary buttons larger. Headings use Grandstander, everything
  else Atkinson Hyperlegible Next.
- **Contrast.** Only the high-contrast pairs of PLAN.md section 8.2 are used, in Day and in Night.
- **Copy** is plain, short, active and in one table (`Words`), so it can be translated. A grown-up can turn on "Read alerts aloud" in Settings (off by default, offered only where the device has a voice): a new warning says which place needs a look, an alarm says "Tell a grown-up now.", and quiet hours and replayed history stay silent. It has not been heard on an Android emulator or phone yet.

## Reduced motion (audited 2026-10-02)

Everything that moves, and what it does when motion is reduced:

| Thing | Moves when | Reduced |
|---|---|---|
| `BeaconBuddy` beam | On its own: sweeps for a warning, spins for an alarm | Stops. The lamp pulses in colour instead (`ReduceMotion`) |
| `HoldButton` ring | In answer to a press: fills over about 2 seconds | Still fills. It is progress the person is holding for, not decoration, and removing it would hide how long to hold |
| Buttons | In answer to a touch: the shadow collapses | Unchanged; a single step, not an animation |
| Page changes | None. Pages are swapped, not slid | n/a |

**Found and fixed in this audit:** the Settings choice (Follow the device, Calmer, Full) was saved but nothing read it, so the
beacon spun regardless. `AlertMotion` now resolves it (an explicit choice wins; "Follow the device" asks the system through
`SystemInformation.PrefersReducedMotion`), `MainForm` starts it, and the beacon follows it. `MotionTests` covers the resolution and
the beacon following a saved change. Whether the system preference really reaches the app on Android has not been run on a device.

## Screen readers (not covered)

- **Every control a person acts on now has a stable `Name` and words to say** (2026-10-03): buttons say their text, the glyph-only
  settings button says "Settings, for grown-ups" and that it must be held, each text box, drop-down and number box carries the label
  that sits above it (a separate control, so the field has to be told), the buddy describes its state in words, and a ticket card reads
  as where, what and how long ago. `AccessibilityTests` walks every screen and fails on a missing name, a repeated name or a target under
  48; it caught the 40-high text boxes, now 48. That is the **tree**, which an automation client reads; whether Android's TalkBack
  actually receives it is #284 and has not been run. The framework's bridge was Windows-only when PLAN.md was written, and this repo has
  not verified what TalkBack sees.
- **The alarm takeover is the one screen that must work without sight.** Until a screen reader run says otherwise, assume it does
  not announce itself. The audible siren, vibration and the notification (which Android does announce) are what carry an alarm.
- **Number entry and focus** (fixed in framework 26.7.0): the PIN fields bring up a number pad, and the box being typed in has a ring.

## What would close the gaps

1. Framework: a mobile screen-reader bridge (F22), and the framework items above are done.
2. App, once a bridge exists: an announcement when the alarm takeover opens (the names are already set).
3. A TalkBack run on a real phone, recorded in `docs/android-background.md`.
