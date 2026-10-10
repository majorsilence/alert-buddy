using AlertBuddy.Core.Abstractions;
using AlertBuddy.Core.Alerts;
using AlertBuddy.Core.Ntfy;
using AlertBuddy.Core.Practice;
using AlertBuddy.Core.Settings;
using AlertBuddy.Core.Store;
using AlertBuddy.ViewModels.Copy;
using AlertBuddy.ViewModels.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AlertBuddy.ViewModels.Screens
{
    /// <summary>
    /// A short, clearly labelled pretend alert, so a child knows what to expect (PLAN.md sections 4.4 and 9): a warning, an alarm, an all
    /// clear, over about 20 seconds. It runs through its own hub and store: nothing is saved to the Alert Book, no real notification is
    /// raised, the takeover never appears, and the sound is the short, quiet practice cue. It earns nothing (section 4.1).
    /// </summary>
    public sealed partial class PracticeViewModel : ScreenViewModel
    {
        private readonly SettingsService settings;
        private readonly IClock clock;
        private readonly IUiDispatcher dispatcher;
        private readonly IScheduler scheduler;
        private readonly ISoundPlayer sound;
        private readonly INavigator navigator;
        private readonly ISpeaker? speaker;
        private readonly List<IDisposable> timers = [];
        private readonly List<IDisposable> announcements = [];
        private AlertHub hub = new ();
        private AlertEngine engine;
        private HubSubscription? subscription;

        [ObservableProperty]
        private BeaconMood mood = BeaconMood.Watching;

        [ObservableProperty]
        private string statusText = "";

        [ObservableProperty]
        private int step;

        [ObservableProperty]
        private string caption = "";

        [ObservableProperty]
        [NotifyCanExecuteChangedFor (nameof (HearSoundCommand))]
        private bool isRunning;

        [ObservableProperty]
        private bool isFinished;

        [ObservableProperty]
        [NotifyCanExecuteChangedFor (nameof (ToldAGrownUpCommand))]
        private bool canTellAGrownUp;

        [ObservableProperty]
        private string soundNote = "";

        /// <summary>How loud a chosen tone is in Practice, against the real alarm.</summary>
        public const double PracticeVolume = 0.4;

        /// <summary>The banner that says this is not real.</summary>
        public string Banner => Words.PracticeBanner;

        /// <summary>Creates the practice screen.</summary>
        public PracticeViewModel (SettingsService settings, IClock clock, IUiDispatcher dispatcher, IScheduler scheduler, ISoundPlayer sound, INavigator navigator, ISpeaker? speaker = null)
        {
            this.speaker = speaker;
            this.settings = settings ?? throw new ArgumentNullException (nameof (settings));
            this.clock = clock ?? throw new ArgumentNullException (nameof (clock));
            this.dispatcher = dispatcher ?? throw new ArgumentNullException (nameof (dispatcher));
            this.scheduler = scheduler ?? throw new ArgumentNullException (nameof (scheduler));
            this.sound = sound ?? throw new ArgumentNullException (nameof (sound));
            this.navigator = navigator ?? throw new ArgumentNullException (nameof (navigator));

            engine = NewEngine ();
            Refresh ();
        }

        /// <summary>
        /// Plays one of the sounds once, quietly, so a child can learn what each one means before it matters. Not offered while the pretend
        /// alert is running, and silent (with a line saying why) when a grown-up has switched sounds off.
        /// </summary>
        [RelayCommand (CanExecute = nameof (CanHearSound))]
        private void HearSound (PracticeSound which)
        {
            if (!CanHearSound ())
                return;

            if (!settings.Current.SoundsEnabled) {
                SoundNote = Words.SoundsAreOff;
                return;
            }

            SoundNote = "";
            StopAnnouncements ();
            if (which == PracticeSound.Gentle) {
                sound.Play (Cue.Practice);
                return;
            }

            sound.Play (AlertFeedback.CueFor ((AlarmTone)((int)which - 1)), PracticeVolume);
            if (which == PracticeSound.VoiceEvacuation)
                Announce (repeat: false);
        }

        // The voice evacuation sound is a chime and then a voice saying which place and what to do, in the voice the grown-up picked.
        private void Announce (bool repeat)
        {
            if (speaker is not { IsSupported: true })
                return;

            void Say ()
            {
                if (IsDisposed)
                    return;

                if (repeat && !alarmSounding)
                    return;

                speaker.Speak (Words.AlarmAnnouncement (PracticeAlertSource.Source), settings.Current.Voice, PracticeVolume, settings.Current.VoiceId);
            }

            announcements.Add (scheduler.Schedule (AlertFeedback.AnnounceAfterTone, Say));
            if (repeat)
                announcements.Add (scheduler.Every (AlertFeedback.VoiceRepeat, Say));
        }

        private void StopAnnouncements ()
        {
            foreach (var timer in announcements)
                timer.Dispose ();
            announcements.Clear ();
        }

        private bool CanHearSound () => !IsRunning;

        [RelayCommand]
        private void Start ()
        {
            if (IsRunning)
                return;

            // A fresh pretend house each time, so an earlier run cannot leave anything behind.
            StopAnnouncements ();
            StopTimers ();
            subscription?.Dispose ();
            hub = new AlertHub ();
            engine = NewEngine ();
            hub.SetConnection (new ConnectionInfo (ConnectionState.Live, LastHeard: clock.Now));
            subscription = new HubSubscription (hub, dispatcher, _ => Refresh ());

            IsRunning = true;
            IsFinished = false;
            Step = 0;
            Caption = "";

            foreach (var scripted in PracticeAlertSource.Script (clock.Now))
                timers.Add (scheduler.Schedule (scripted.After, () => Run (scripted)));

            timers.Add (scheduler.Schedule (PracticeAlertSource.Length, Finish));
            Refresh ();
        }

        [RelayCommand]
        private void Stop ()
        {
            StopTimers ();
            IsRunning = false;
            Step = 0;
            Caption = "";
            Refresh ();
        }

        /// <summary>Practise the child's button: say "I told a grown-up" to the pretend alarm.</summary>
        [RelayCommand (CanExecute = nameof (CanTellAGrownUp))]
        private void ToldAGrownUp ()
        {
            var alarm = hub.Snapshot.Active.FirstOrDefault (a => a.Level == AlertLevel.Alarm && a.Status == AlertStatus.Active);
            if (alarm is not null)
                engine.Acknowledge (alarm.Id);
        }

        [RelayCommand]
        private void Back ()
        {
            Stop ();
            navigator.GoBack ();
        }

        private void Run (PracticeStep scripted)
        {
            if (IsDisposed || !IsRunning)
                return;

            Step = scripted.Number;
            Caption = scripted.Caption;

            engine.Handle (new NtfyEvent (NtfyEventKind.Message, scripted.Message, MessageOrigin.Live));
            Refresh ();

            // The warning and the all clear are one quiet cue. The alarm is not: it keeps sounding until the all clear or Stop, as a real
            // one does, and Refresh looks after that. A grown-up may pick which tone the child rehearses with.
            if (!alarmSounding && settings.Current.SoundsEnabled)
                PlayCue (loop: false);
        }

        private bool alarmSounding;

        private void PlayCue (bool loop)
        {
            var tone = settings.Current.PracticeTone;
            if (loop) {
                sound.StartLoop (tone is { } t ? AlertFeedback.CueFor (t) : Cue.Practice, PracticeVolume);
                if (tone == AlarmTone.VoiceEvacuation)
                    Announce (repeat: true);
                return;
            }

            if (tone is { } chosen)
                sound.Play (AlertFeedback.CueFor (chosen), PracticeVolume);
            else
                sound.Play (Cue.Practice);
        }

        // The alarm sound runs exactly while the pretend alarm is open and unanswered and the practice is running: the all clear, Stop,
        // "I told a grown-up" and leaving the screen all end it (the same rule as a real alarm).
        private void UpdateAlarmSound (bool alarmOpen)
        {
            var wanted = IsRunning && alarmOpen && settings.Current.SoundsEnabled;
            if (wanted && !alarmSounding) {
                alarmSounding = true;
                PlayCue (loop: true);
            } else if (!wanted && alarmSounding) {
                alarmSounding = false;
                StopAnnouncements ();
                sound.StopLoop ();
            }
        }

        private void Finish ()
        {
            if (IsDisposed || !IsRunning)
                return;

            IsRunning = false;
            IsFinished = true;
            Refresh ();
        }

        private void Refresh ()
        {
            if (IsDisposed)
                return;

            var snapshot = hub.Snapshot;

            // The same words and moods as the real Home, so what the child practises is exactly what they will see.
            var celebrating = snapshot.History.FirstOrDefault (a => a.Status == AlertStatus.Resolved) is { } cleared && snapshot.Active.Count == 0 && IsRunning
                ? new AllClearInfo (cleared.Source, cleared.Temperature is not null)
                : null;

            var status = HomeStatusCalculator.Compute (snapshot, settings.Current.BuddyName, celebrating, isNight: false);
            Mood = status.Mood;
            StatusText = status.Text;
            CanTellAGrownUp = snapshot.Active.Any (a => a.Level == AlertLevel.Alarm && a.Status == AlertStatus.Active);
            UpdateAlarmSound (CanTellAGrownUp);
        }

        // Nothing is saved: the engine has no persistence, and the store is its own.
        private AlertEngine NewEngine () => new (new AlertStore (), hub, clock);

        private void StopTimers ()
        {
            foreach (var timer in timers)
                timer.Dispose ();
            timers.Clear ();
        }

        protected override void OnDisposed ()
        {
            StopAnnouncements ();
            if (alarmSounding) {
                alarmSounding = false;
                sound.StopLoop ();
            }

            StopTimers ();
            subscription?.Dispose ();
        }
    }
}
