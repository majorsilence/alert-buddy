# Design notes: accessibility and motion

What the app does about accessibility and reduced motion today, and what it does not. Written from the code and the emulator runs,
not from a screen reader session: **nobody has run TalkBack or VoiceOver against this app**, so the "not covered" list below is
what is known to be missing, not a full audit.

## What is built in

- **State is never colour alone.** The buddy's face, a glyph and words all repeat the level (PLAN.md section 8.11).
- **Sizes.** Body text 18, nothing under 16, touch targets 48 and primary buttons larger. Headings use Grandstander, everything
  else Atkinson Hyperlegible Next.
- **Contrast.** Only the high-contrast pairs of PLAN.md section 8.2 are used, in Day and in Night.
- **Copy** is plain, short, active and in one table (`Words`), so it can be translated and read aloud.

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

- **Nothing sets an accessible name or description** on any control. The views are custom-painted, so a screen reader would find
  unlabelled canvas, not the words on screen. The framework's accessibility bridge was Windows-only when PLAN.md was written
  (register item F22 is its mobile design note and spike); this repo has not verified what Android TalkBack sees.
- **The alarm takeover is the one screen that must work without sight.** Until a screen reader run says otherwise, assume it does
  not announce itself. The audible siren, vibration and the notification (which Android does announce) are what carry an alarm.
- **Number entry.** The PIN fields bring up the full letter keyboard on Android, not a number pad.
- **Focus.** The text box being typed in loses its outline (majorsilence/Majorsilence.Forms#366).

## What would close the gaps

1. Framework: a mobile screen-reader bridge (F22), a way to ask for a numeric keyboard, and the focus fix (#366).
2. App, once a bridge exists: names and descriptions on the buddy, ticket cards and buttons, and an announcement when the alarm
   takeover opens.
3. A TalkBack run on a real phone, recorded in `docs/android-background.md`.
