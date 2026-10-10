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
        private readonly IScheduler? scheduler;
        private IDisposable? voiceRepeat;
        private IDisposable? voiceFirst;
        private bool sirenRunning;

        /// <summary>How often the spoken evacuation instruction is repeated while the alarm is open.</summary>
        public static readonly TimeSpan VoiceRepeat = TimeSpan.FromSeconds (12);

        /// <summary>How long after the attention chime starts the voice begins: the length of the chime's two notes, so it speaks after the tone.</summary>
        public static readonly TimeSpan AnnounceAfterTone = TimeSpan.FromSeconds (1.3);

        /// <summary>Starts listening to the hub.</summary>
        public AlertFeedback (AlertHub hub, ISoundPlayer sound, IHaptics haptics, IAlertNotifier notifier, SettingsService settings, IClock clock, TimeZoneInfo? zone = null, ISpeaker? speaker = null, IScheduler? scheduler = null)
        {
            this.hub = hub ?? throw new ArgumentNullException (nameof (hub));
            this.sound = sound ?? throw new ArgumentNullException (nameof (sound));
            this.haptics = haptics ?? throw new ArgumentNullException (nameof (haptics));
            this.notifier = notifier ?? throw new ArgumentNullException (nameof (notifier));
            this.settings = settings ?? throw new ArgumentNullException (nameof (settings));
            this.clock = clock ?? throw new ArgumentNullException (nameof (clock));
            this.zone = zone ?? TimeZoneInfo.Local;
            this.speaker = speaker;
            this.scheduler = scheduler;

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
                    Play (wanted, current.SoundsEnabled, current.AlarmTone, change.Alert?.Source ?? "", change.Alert?.Temperature is not null);
                    Speak (change, wanted, current.ReadAloud, current.AlarmTone);
                }

                // Whether or not this change made a sound, the siren must run exactly while an alarm is open and unanswered: it stops the
                // moment the child taps, a grown-up says "Got it", or the all clear arrives, and never outlives its alarm.
                var alarmPending = hub.Snapshot.Active.Any (a => a.Level == AlertLevel.Alarm && a.Status == AlertStatus.Active);
                if (!alarmPending)
                    StopSiren ();
            }
        }

        private void Play (AlertSound wanted, bool friendlySounds, AlarmTone alarmTone, string source, bool hot)
        {
            switch (wanted) {
                case AlertSound.Alarm:
                    // The siren is not one of the "friendly sounds": muting those never mutes an alarm.
                    if (!sirenRunning) {
                        sirenRunning = true;
                        sound.StartLoop (CueFor (alarmTone));
                        haptics.Alarm ();
                        StartVoice (alarmTone, source, hot);
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
        private void Speak (AlertChange change, AlertSound wanted, bool readAloud, AlarmTone alarmTone)
        {
            if (!readAloud || speaker is not { IsSupported: true } || change.Alert is not { } alert)
                return;

            if (change.Kind is not (AlertChangeKind.Raised or AlertChangeKind.Upgraded))
                return;

            var line = wanted switch {
                // The voice evacuation tone already says this line itself, on repeat.
                AlertSound.Alarm when alarmTone != AlarmTone.VoiceEvacuation => Copy.Words.TellAGrownUpNow,
                AlertSound.Warning => Copy.Words.NeedsALook (alert.Source),
                _ => null,
            };

            if (line is not null)
                speaker.Speak (line, settings.Current.Voice, voiceId: settings.Current.VoiceId);
        }

        /// <summary>The cue a tone plays as. Shared with Practice so a rehearsal sounds like the real thing.</summary>
        public static Cue CueFor (AlarmTone tone) => tone switch {
            AlarmTone.Code3 => Cue.Code3,
            AlarmTone.MarchTime => Cue.MarchTime,
            AlarmTone.Continuous => Cue.Continuous,
            AlarmTone.VoiceEvacuation => Cue.VoiceEvacuation,
            _ => Cue.Alarm,
        };

        // The chime is a generated file; the words are the device's own voice, in the voice the grown-up picked: which place, and what to do, said
        // after the tone and again every few seconds while the alarm is open. A device with no voice (or no scheduler) just plays the chime,
        // which is still an unmistakable alarm.
        private void StartVoice (AlarmTone tone, string source, bool hot)
        {
            if (tone != AlarmTone.VoiceEvacuation || speaker is not { IsSupported: true } || scheduler is null)
                return;

            var line = Copy.Words.AlarmAnnouncement (source, hot);
            void Say ()
            {
                lock (gate) {
                    if (sirenRunning)
                        speaker.Speak (line, settings.Current.Voice, voiceId: settings.Current.VoiceId);
                }
            }

            voiceFirst = scheduler.Schedule (AnnounceAfterTone, Say);
            voiceRepeat = scheduler.Every (VoiceRepeat, Say);
        }

        private void StopSiren ()
        {
            voiceFirst?.Dispose ();
            voiceFirst = null;
            voiceRepeat?.Dispose ();
            voiceRepeat = null;

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
