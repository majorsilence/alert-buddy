# Android background delivery

PLAN.md section 6.2 asks for the foreground-service choice to be recorded here. Everything below was run on **an emulator**
(`alertbuddy-phone`, API 36, Google APIs x86_64, KVM-accelerated), not a real phone. An emulator is not a device: the rows marked
"not proven" are the ones that need hardware before milestone 4 can be called done.

## The choice: a `specialUse` foreground service

`ListenerService` (`src/AlertBuddy.Android/ListenerService.cs`) is a foreground service of type `specialUse`, declared by hand in
`AndroidManifest.xml` with the `PROPERTY_SPECIAL_USE_FGS_SUBTYPE` explanation, because the `[Service]` attribute cannot write that
property. `dataSync` was rejected: Android 15 caps it at about six hours in 24, which is wrong for a listener that must be up all night.
`specialUse` has no such cap, needs no Play review for a sideloaded build, and does need one if the app goes to the Play Store
(PLAN.md section 2: sideload first).

The service owns nothing. `AppHost` holds the one `AlertBuddyApp` of the process, so the service can start it with no window and an
activity opened later finds it already listening, with no IPC.

## What was run, and what it showed

| Check | Result on the emulator |
|---|---|
| App launches, First run completed through the soft keyboard | Works. Typing, Next, Test connection to a host `FakeNtfy` at `10.0.2.2:8080` over plain http |
| Foreground service starts with the app | `isForeground=true`, type `0x40000000` (specialUse), channel `listening` |
| Screen off, a scripted warning, alarm and all clear | Warning cue played, the alarm notification posted on the `alarm` channel (ongoing, high), the siren's `MediaPlayer` was created with `USAGE_ALARM` on `STREAM_ALARM`, the vibrator service was used, and the siren stopped at the all clear |
| The all clear replaces the alarm notification | Needed a fix: re-posting under the same id left the ongoing alarm in place when the channel changed from high to low importance. `AndroidAlertNotifier` now cancels first |
| **Reboot, app never opened** | The boot receiver restarted the service (`isForeground=true`, pid alive). A scripted alarm then posted its notification and created its player, so the framework's `LocalNotifications` and `AudioPlayer` work in a process that started with no activity |

## Not proven, and risks

- **Android 16 audio hardening.** After the reboot the system log said `AudioHardening background playback would be muted for
  com.majorsilence.alertbuddy ... level: full`. On this emulator it is only logged ("would be"). If a real Android 16 phone enforces it, a
  siren started from a background process may be muted. Needs a real device, with the screen off, before M4 is done.
- **No full-screen takeover seen.** The notification asks for one (`FullScreen`) and the manifest declares `USE_FULL_SCREEN_INTENT`, but the
  emulator's `dumpsys` did not show the intent and nothing took over the screen. Android 14 and later restrict it; the allow step
  belongs to the permission wizard, which is not built.
- **The permission wizard is built, and was run on the emulator.** First run's fourth step lists notifications, the alarm taking over the
  screen (Android 14 and later), the alarm volume with a two-second test sound, battery optimisation and Do Not Disturb. Each shows
  "Done" or "Not yet" in words, has an Allow or Change button, and reads Android's answer again on arriving at the step, on returning
  from system settings and after a permission answer. On the emulator: pressing Allow showed the system's notification question and the
  step turned to Done by itself; Change opened the app's own notification settings. Not run: the battery, Do Not Disturb and full-screen
  screens (the emulator reports full-screen as already allowed), and nothing asks again once First run is finished, so Settings does not
  yet offer these steps.
- **The honest banner is partly real.** `AndroidBackgroundListener.WhyNot` names, in order: notifications off, battery optimisation
  on (the emulator is always in this state, so Home always shows it), and no network. The text on Home is generic ("can't listen in the
  background") and does not yet say which one.
- **`InMemorySecretStore` still holds the password.** A grown-up with a signed-in server loses the password when the process is killed
  (TEMP-SHIM F16, waiting on the unpublished `Majorsilence.Forms.Essentials`).
- **Fonts are bundled but not yet seen on a device.** Grandstander (Bold) and Atkinson Hyperlegible Next (Regular, Bold) are embedded in `AlertBuddy.Shared` with their SIL OFL licences and registered through `PrivateFontCollection`, the route spike S2 ran on the emulator. A Headless test proves the bundled family draws differently from an unknown one; the new APK has not been run to look at it.
