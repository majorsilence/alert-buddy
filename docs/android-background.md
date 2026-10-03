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
- **The password and token now go to the Android Keystore** through `Majorsilence.Forms.Essentials` 26.6.0 (`PlatformSecretStore`). Run on the emulator on 2026-10-02: First run was finished with a password entered, the app was force-stopped and relaunched, and Settings then said "Password or token (leave blank to keep the saved one)", which it only says when a stored secret was read back. So a saved password survives a process kill on the emulator. The file itself could not be inspected (a release APK is not debuggable, so `run-as` is refused), so *that the value is encrypted at rest* is the framework's claim and is not checked here. Not run on a phone.
- **The bundled fonts are drawn on the emulator** (2026-10-02, 26.6.0 APK, API 36 emulator, KVM): Grandstander Bold for headings and buttons, Atkinson Hyperlegible Next for body text.
- **Found on that run:** the text box being typed in loses its outline (framework issue #366, recorded in `docs/framework-findings.md`), and the PIN fields in First run bring up the full letter keyboard rather than a number pad (majorsilence/Majorsilence.Forms#368; the gate's own PIN pad is a custom number pad and is fine). The emulator also showed one "System UI isn't responding" dialog while it was still settling after boot, which is the emulator and not the app.
- **Two clipped layouts, found and fixed on the same run:** Home's red "may miss an alert" line was cut off after two lines on a phone (a fixed 52 high), and Settings' long "Password or token (leave blank to keep the saved one)" label ran past the right edge and gave the screen a horizontal scrollbar. Both now size to their text (`FormColumn.ParagraphHeight`); the banner was seen whole on the emulator afterwards. The Settings label was not re-checked there.

## Tablet emulator run (2026-10-03)

Pixel Tablet profile (2560x1600), API 36, KVM, the 26.6.0 APK, against `tools/FakeNtfy` over plain HTTP at the emulator's host address
(`http://10.0.2.2:8080`, which the app accepts as a private-network address). An emulator, not a tablet.

- **First run to Home worked end to end** with typed input, then `POST /_scenario/home-alerts` showed a warning, an alarm and an all clear
  arriving live: "Listening. Last heard a few seconds ago.", the warning card on the right of the **two-pane layout** with its "Right now"
  heading, then the sentence and beacon returning to calm. The full-screen alarm takeover itself was not seen: the process was killed first
  (below).
- **Found: native memory grew without limit while the beacon animated, and the system killed the process** (`LOW_MEMORY`, rss 2.3 GB, within
  about six minutes). A static screen was flat. Forcing a garbage collection once a second keeps it between about 130 and 340 MB over 90
  seconds, so `MemoryGuard` does that (TEMP-SHIM F27, framework issue #371). A bedside tablet showing the beacon all night would have died
  without it; the shim has been run for 90 seconds, not overnight.
- **Found: a resolved alert's card stayed on screen** with its age frozen, while the sentence said "All quiet" (framework issue #370, TEMP-SHIM
  F28: Home and the Alert book invalidate their lists after removing cards). A freshly built Home was correct.
- **Seen and fixed afterwards:** on a tablet First run and Settings stretched their fields across the whole 2560-wide screen; rows now stop at 560 and sit in the middle (checked headlessly at 1280, not re-run on the emulator). It worked, but it was not a
  designed layout. Still open: the red reason line sits over the bottom of the form on a short step.
- **Not tried:** rotating, the soft keyboard in landscape beyond typing, a real tablet.

## Alarm with the screen off, phone emulator (2026-10-03)

Pixel 5 profile, API 36, KVM, the 26.6.0 APK, `tools/FakeNtfy` over `http://10.0.2.2:8080`, notification permission granted with `pm grant`
and `USE_FULL_SCREEN_INTENT` allowed with `appops`. An emulator, not a phone.

- **Live alarm, screen on:** the takeover appeared from the network ("Tell a grown-up now." in the display face, the sweeping beam, the red
  button), and `dumpsys audio` showed a `MediaPlayer` **started with `USAGE_ALARM`**: the siren is on the alarm stream.
- **Screen off (`KEYCODE_SLEEP`), foreground service running:** the siren still started on the alarm stream and an **alarm-channel
  notification was posted, importance high, carrying a full-screen intent**, so the listener, the siren and the notification all work with the
  screen off. **The screen did not wake and the takeover did not appear**, in three runs and also after the permission and the app op were
  granted. The cause is not established: SystemUI's log said nothing and Android 16 has its own notification-avalanche logic in the path. This
  needs a real phone.
- `MainActivity` now declares `ShowWhenLocked` and `TurnScreenOn` (an Activity started by a full-screen intent needs them to light a sleeping
  screen). Their effect is shown: `am start` on the sleeping emulator woke the screen. Their effect on a real full-screen notification is not.
  They also let the app be opened over a lock screen, which is wanted for an alarm and worth knowing.
- `MemoryGuard` (TEMP-SHIM F27) was running every second even with the screen off; it now runs only while the Activity is in front.

