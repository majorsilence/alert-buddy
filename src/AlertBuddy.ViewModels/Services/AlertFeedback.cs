using AlertBuddy.Core.Abstractions;
using AlertBuddy.Core.Alerts;
using AlertBuddy.Core.Ntfy;
using AlertBuddy.Core.Settings;
using AlertBuddy.Core.Store;

namespace AlertBuddy.ViewModels.Services
{
    /// <summary>
    /// What an alert does to the world: a sound, a vibration, a notification. Runs on whatever thread the hub raises on and needs no
    /// screen, so the Android foreground service uses exactly this with no UI open. The adapters it calls must be thread-safe.
    /// </summary>
    public sealed class AlertFeedback : IDisposable
    {
        private readonly object gate = new ();
        private readonly AlertHub hub;
        private readonly ISoundPlayer sound;
        private readonly IHaptics haptics;
        private readonly IAlertNotifier notifier;
        private readonly SettingsService settings;
        private readonly IClock clock;
        private readonly TimeZoneInfo zone;
        private readonly ISpeaker? speaker;
        private bool sirenRunning;

        /// <summary>Starts listening to the hub.</summary>
        public AlertFeedback (AlertHub hub, ISoundPlayer sound, IHaptics haptics, IAlertNotifier notifier, SettingsService settings, IClock clock, TimeZoneInfo? zone = null, ISpeaker? speaker = null)
        {
            this.hub = hub ?? throw new ArgumentNullException (nameof (hub));
            this.sound = sound ?? throw new ArgumentNullException (nameof (sound));
            this.haptics = haptics ?? throw new ArgumentNullException (nameof (haptics));
            this.notifier = notifier ?? throw new ArgumentNullException (nameof (notifier));
            this.settings = settings ?? throw new ArgumentNullException (nameof (settings));
            this.clock = clock ?? throw new ArgumentNullException (nameof (clock));
            this.zone = zone ?? TimeZoneInfo.Local;
            this.speaker = speaker;

            hub.AlertChanged += OnChange;
        }

        /// <summary>Stops listening, and silences the siren.</summary>
        public void Dispose ()
        {
            hub.AlertChanged -= OnChange;
            lock (gate)
                StopSiren ();
        }

        private void OnChange (AlertChange change)
        {
            lock (gate) {
                var current = settings.Current;

                // A replay builds state and never makes a sound or raises a notification (PLAN.md section 5.3).
                if (change.Origin == MessageOrigin.Live) {
                    Notify (change);

                    var wanted = SoundPolicy.Apply (change.Sound, current.Night, clock.Now, zone);
                    Play (wanted, current.SoundsEnabled);
                    Speak (change, wanted, current.ReadAloud);
                }

                // Whether or not this change made a sound, the siren must run exactly while an alarm is open and unanswered: it stops the
                // moment the child taps, a grown-up says "Got it", or the all clear arrives, and never outlives its alarm.
                var alarmPending = hub.Snapshot.Active.Any (a => a.Level == AlertLevel.Alarm && a.Status == AlertStatus.Active);
                if (!alarmPending)
                    StopSiren ();
            }
        }

        private void Play (AlertSound wanted, bool friendlySounds)
        {
            switch (wanted) {
                case AlertSound.Alarm:
                    // The siren is not one of the "friendly sounds": muting those never mutes an alarm.
                    if (!sirenRunning) {
                        sirenRunning = true;
                        sound.StartLoop (Cue.Alarm);
                        haptics.Alarm ();
                    }
                    break;
                case AlertSound.Warning when friendlySounds:
                    sound.Play (Cue.Warning);
                    break;
                case AlertSound.AllClear when friendlySounds:
                    sound.Play (Cue.AllClear);
                    break;
                case AlertSound.TestCheer when friendlySounds:
                    sound.Play (Cue.Cheer);
                    break;
            }
        }

        // The same line a grown-up would say: the alarm instruction, or which place needs a look. Only for news that is making a sound,
        // so quiet hours keep warnings silent here too, and an alarm is never hushed.
        private void Speak (AlertChange change, AlertSound wanted, bool readAloud)
        {
            if (!readAloud || speaker is not { IsSupported: true } || change.Alert is not { } alert)
                return;

            if (change.Kind is not (AlertChangeKind.Raised or AlertChangeKind.Upgraded))
                return;

            var line = wanted switch {
                AlertSound.Alarm => Copy.Words.TellAGrownUpNow,
                AlertSound.Warning => Copy.Words.NeedsALook (alert.Source),
                _ => null,
            };

            if (line is not null)
                speaker.Speak (line);
        }

        private void StopSiren ()
        {
            if (!sirenRunning)
                return;

            sirenRunning = false;
            sound.StopLoop ();
            haptics.Stop ();
        }

        // A notification for news that needs one; and the alarm's ongoing notification is removed once the child has answered.
        private void Notify (AlertChange change)
        {
            if (change.Alert is not { } alert)
                return;

            switch (change.Kind) {
                case AlertChangeKind.Raised:
                case AlertChangeKind.Upgraded:
                case AlertChangeKind.Downgraded:
                case AlertChangeKind.Resolved:
                    notifier.Show (alert);
                    break;
                case AlertChangeKind.Acknowledged:
                case AlertChangeKind.Handled:
                    notifier.Clear (alert.Id);
                    break;
            }
        }
    }
}
