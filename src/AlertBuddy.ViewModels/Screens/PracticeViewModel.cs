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
        private readonly List<IDisposable> timers = [];
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
        public PracticeViewModel (SettingsService settings, IClock clock, IUiDispatcher dispatcher, IScheduler scheduler, ISoundPlayer sound, INavigator navigator)
        {
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
            if (which == PracticeSound.Gentle)
                sound.Play (Cue.Practice);
            else
                sound.Play (AlertFeedback.CueFor ((AlarmTone)((int)which - 1)), PracticeVolume);
        }

        private bool CanHearSound () => !IsRunning;

        [RelayCommand]
        private void Start ()
        {
            if (IsRunning)
                return;

            // A fresh pretend house each time, so an earlier run cannot leave anything behind.
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

            // Quiet and played once, never looped: it is a rehearsal. A grown-up may pick which tone the child rehearses with.
            var current = settings.Current;
            if (current.SoundsEnabled) {
                if (current.PracticeTone is { } tone)
                    sound.Play (AlertFeedback.CueFor (tone), PracticeVolume);
                else
                    sound.Play (Cue.Practice);
            }

            engine.Handle (new NtfyEvent (NtfyEventKind.Message, scripted.Message, MessageOrigin.Live));
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
            StopTimers ();
            subscription?.Dispose ();
        }
    }
}
