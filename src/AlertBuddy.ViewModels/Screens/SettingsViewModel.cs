using System.ComponentModel;
using AlertBuddy.Core.Interpretation;
using AlertBuddy.Core.Ntfy;
using AlertBuddy.Core.Security;
using AlertBuddy.Core.Settings;
using AlertBuddy.Core.Store;
using AlertBuddy.ViewModels.Copy;
using AlertBuddy.ViewModels.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AlertBuddy.ViewModels.Screens
{
    /// <summary>Whether reduced motion follows the system, or is forced on or off.</summary>
    public enum MotionPreference
    {
        /// <summary>Follow the system setting.</summary>
        System,

        /// <summary>Always reduce motion.</summary>
        Reduce,

        /// <summary>Never reduce motion.</summary>
        Full,
    }

    /// <summary>
    /// Everything a grown-up can configure, behind the gate (PLAN.md sections 4.3 and 9). Edits are held here and applied only by
    /// <see cref="SaveCommand"/>, so half-typed values never reach the listener. A stored password or token is never shown, and a blank
    /// secret field means "leave it as it is".
    /// </summary>
    public sealed partial class SettingsViewModel : ScreenViewModel
    {
        private readonly SettingsService settings;
        private readonly ISecretStore secrets;
        private readonly IConnectionTester tester;
        private readonly IListenerControl listener;
        private readonly AlertEngine engine;
        private readonly INavigator navigator;
        private CancellationTokenSource? testing;

        [ObservableProperty] private string serverUrl = "";
        [ObservableProperty] private string topic = "";
        [ObservableProperty] private AuthMode auth;
        [ObservableProperty] private string username = "";
        [ObservableProperty] private string secret = "";
        [ObservableProperty] private bool hasStoredSecret;
        [ObservableProperty] private string buddyName = "";
        [ObservableProperty] private BuddyColour buddyColour;
        [ObservableProperty] private LookPreference look;
        [ObservableProperty] private MotionPreference motion;
        [ObservableProperty] private bool soundsEnabled = true;
        [ObservableProperty] private int silenceMinutes = 10;
        [ObservableProperty] private bool nightEnabled = true;
        [ObservableProperty] private TimeOnly nightStart = new (20, 0);
        [ObservableProperty] private TimeOnly nightEnd = new (7, 0);
        [ObservableProperty] private int alarmPriority = 5;
        [ObservableProperty] private int warningPriority = 4;
        [ObservableProperty] private bool stripLeadingEmoji = true;
        [ObservableProperty] private string sourceSeparator = ": ";
        [ObservableProperty] private string testTitlePattern = InterpretationSettings.DefaultTestTitlePattern;
        [ObservableProperty] private string temperaturePattern = InterpretationSettings.DefaultTemperaturePattern;
        [ObservableProperty] private string newPin = "";
        [ObservableProperty] private string newPinConfirm = "";
        [ObservableProperty] private string? testResult;
        [ObservableProperty] private bool isTesting;
        [ObservableProperty] private string? savedMessage;

        /// <summary>Creates the settings screen and loads the current settings into it.</summary>
        /// <param name="version">The app version, for About.</param>
        public SettingsViewModel (
            SettingsService settings,
            ISecretStore secrets,
            IConnectionTester tester,
            IListenerControl listener,
            AlertEngine engine,
            INavigator navigator,
            string version = "")
        {
            this.settings = settings ?? throw new ArgumentNullException (nameof (settings));
            this.secrets = secrets ?? throw new ArgumentNullException (nameof (secrets));
            this.tester = tester ?? throw new ArgumentNullException (nameof (tester));
            this.listener = listener ?? throw new ArgumentNullException (nameof (listener));
            this.engine = engine ?? throw new ArgumentNullException (nameof (engine));
            this.navigator = navigator ?? throw new ArgumentNullException (nameof (navigator));
            Version = version;

            LoadFrom (settings.Current);
        }

        /// <summary>The app version.</summary>
        public string Version { get; }

        /// <summary>The safety note for About.</summary>
        public string SafetyNote => Words.SafetyNote;

        /// <summary>One paragraph of privacy for About (PLAN.md section 4.3).</summary>
        public string Privacy => "Alert Buddy only talks to the server a grown-up set up. It has no accounts, no ads and no tracking, and its history stays on this device.";

        /// <summary>The problem with the server address, if any.</summary>
        public string? ServerProblem => string.IsNullOrWhiteSpace (ServerUrl) ? Words.NotSetUp : NtfyEndpoint.Check (ServerUrl).Problem;

        /// <summary>The problem with the topic, if any.</summary>
        public string? TopicProblem => NtfyTopic.IsValid (Topic) ? null : Words.TopicFormat;

        /// <summary>The problem with the sign-in fields, if any.</summary>
        public string? SignInProblem
            => Auth == AuthMode.Basic && Username.Trim ().Length == 0 ? Words.UsernameNeeded
             : Auth != AuthMode.None && Secret.Length == 0 && !HasStoredSecret ? Words.SecretNeeded
             : null;

        /// <summary>Problems with the interpretation patterns, for the settings screen to show. Empty when all is well.</summary>
        public IReadOnlyList<string> InterpretationProblems => new AlertInterpreter (BuildInterpretation ()).Problems;

        /// <summary>The problem with a new PIN, if one is being entered.</summary>
        public string? PinProblem
            => NewPin.Length == 0 && NewPinConfirm.Length == 0 ? null
             : !PinHasher.IsValidPin (NewPin) ? Words.PinFormat
             : NewPin != NewPinConfirm ? Words.PinMismatch
             : null;

        /// <summary>The problem with the buddy's name, if any.</summary>
        public string? NameProblem => BuddyName.Trim ().Length is >= 1 and <= 16 ? null : Words.NameNeeded;

        /// <summary>Whether Save may be pressed: nothing is invalid.</summary>
        public bool CanSave => ServerProblem is null && TopicProblem is null && SignInProblem is null && PinProblem is null && NameProblem is null;

        /// <summary>Whether the address is plain http on a private network.</summary>
        public bool ServerIsUnencrypted => NtfyEndpoint.Check (ServerUrl) is { IsValid: true, Unencrypted: true };

        protected override void OnPropertyChanged (PropertyChangedEventArgs e)
        {
            base.OnPropertyChanged (e);

            if (e.PropertyName is nameof (ServerProblem) or nameof (TopicProblem) or nameof (SignInProblem) or nameof (PinProblem) or nameof (NameProblem)
                or nameof (CanSave) or nameof (ServerIsUnencrypted) or nameof (InterpretationProblems) or nameof (SavedMessage) or nameof (TestResult) or nameof (IsTesting))
                return;

            OnPropertyChanged (nameof (ServerProblem));
            OnPropertyChanged (nameof (TopicProblem));
            OnPropertyChanged (nameof (SignInProblem));
            OnPropertyChanged (nameof (PinProblem));
            OnPropertyChanged (nameof (NameProblem));
            OnPropertyChanged (nameof (CanSave));
            OnPropertyChanged (nameof (ServerIsUnencrypted));
            OnPropertyChanged (nameof (InterpretationProblems));
            SaveCommand.NotifyCanExecuteChanged ();
            TestConnectionCommand.NotifyCanExecuteChanged ();
            SavedMessage = null;
        }

        /// <summary>Applies every edit: settings, secrets, PIN, the interpreter and the listener.</summary>
        [RelayCommand (CanExecute = nameof (CanSave))]
        private async Task SaveAsync ()
        {
            var before = settings.Current;
            var endpoint = NtfyEndpoint.Check (ServerUrl);
            var serverChanged = before.ServerUrl != endpoint.BaseUri!.ToString ().TrimEnd ('/') || before.Topic != Topic
                || before.Auth != Auth || before.Username != (Auth == AuthMode.Basic ? Username.Trim () : "") || Secret.Length > 0;

            // A blank secret field leaves the stored one alone; a typed one replaces it; switching mode drops the one for the mode left.
            if (Auth != AuthMode.Basic)
                secrets.Remove (SecretKeys.Password);
            if (Auth != AuthMode.Token)
                secrets.Remove (SecretKeys.Token);
            if (Secret.Length > 0)
                secrets.Set (Auth == AuthMode.Basic ? SecretKeys.Password : SecretKeys.Token, Secret);

            var updated = before with {
                ServerUrl = endpoint.BaseUri.ToString ().TrimEnd ('/'),
                Topic = Topic,
                Auth = Auth,
                Username = Auth == AuthMode.Basic ? Username.Trim () : "",
                BuddyName = BuddyName.Trim (),
                BuddyColour = BuddyColour,
                Look = Look,
                ReduceMotion = Motion switch { MotionPreference.Reduce => true, MotionPreference.Full => false, _ => null },
                SoundsEnabled = SoundsEnabled,
                SilenceWindow = TimeSpan.FromMinutes (Math.Clamp (SilenceMinutes, 1, 240)),
                Night = new NightPolicy { Enabled = NightEnabled, Start = NightStart, End = NightEnd },
                Interpretation = BuildInterpretation (),
                Pin = PinProblem is null && NewPin.Length == 4 ? PinHasher.Create (NewPin) : before.Pin,
            };

            settings.Save (updated);
            engine.SetInterpreter (new AlertInterpreter (updated.Interpretation));

            Secret = "";
            NewPin = "";
            NewPinConfirm = "";
            HasStoredSecret = HasSecret (updated.Auth);
            SavedMessage = "Saved.";

            // Only what changes the connection needs the listener to start over.
            if (serverChanged)
                await listener.RestartAsync ().ConfigureAwait (false);
        }

        /// <summary>Tries what is on screen, not what was saved, so a grown-up can check before committing.</summary>
        [RelayCommand (CanExecute = nameof (CanTest))]
        private async Task TestConnectionAsync ()
        {
            testing?.Cancel ();
            testing = new CancellationTokenSource ();
            var token = testing.Token;

            IsTesting = true;
            TestResult = Words.Testing;
            try {
                var result = await tester.TestAsync (ServerUrl, Topic, CredentialsForTest (), token).ConfigureAwait (false);
                if (!token.IsCancellationRequested)
                    TestResult = result.Message;
            } catch (OperationCanceledException) {
            } finally {
                if (!token.IsCancellationRequested)
                    IsTesting = false;
            }
        }

        private bool CanTest () => !IsTesting && ServerProblem is null && TopicProblem is null;

        [RelayCommand]
        private void ResetInterpretation ()
        {
            var d = InterpretationSettings.Default;
            AlarmPriority = d.AlarmPriority;
            WarningPriority = d.WarningPriority;
            StripLeadingEmoji = d.StripLeadingEmoji;
            SourceSeparator = d.SourceSeparator;
            TestTitlePattern = d.TestTitlePattern;
            TemperaturePattern = d.TemperaturePattern;
        }

        [RelayCommand]
        private void ClearHistory () => engine.ClearHistory ();

        [RelayCommand]
        private void Back () => navigator.GoBack ();

        private NtfyCredentials CredentialsForTest ()
        {
            // A typed secret is what to test; otherwise the stored one, so "test" works without retyping a password.
            var typed = Secret.Length > 0 ? Secret : (Auth == AuthMode.Basic ? secrets.Get (SecretKeys.Password) : secrets.Get (SecretKeys.Token)) ?? "";
            return Auth switch {
                AuthMode.Basic when Username.Trim ().Length > 0 => NtfyCredentials.Basic (Username.Trim (), typed),
                AuthMode.Token when typed.Length > 0 => NtfyCredentials.Bearer (typed),
                _ => NtfyCredentials.None,
            };
        }

        private InterpretationSettings BuildInterpretation ()
            => new () {
                AlarmPriority = AlarmPriority,
                WarningPriority = WarningPriority,
                StripLeadingEmoji = StripLeadingEmoji,
                SourceSeparator = SourceSeparator,
                TestTitlePattern = TestTitlePattern,
                TemperaturePattern = TemperaturePattern,
            };

        private bool HasSecret (AuthMode mode)
            => mode switch {
                AuthMode.Basic => !string.IsNullOrEmpty (secrets.Get (SecretKeys.Password)),
                AuthMode.Token => !string.IsNullOrEmpty (secrets.Get (SecretKeys.Token)),
                _ => false,
            };

        private void LoadFrom (AppSettings s)
        {
            ServerUrl = s.ServerUrl;
            Topic = s.Topic;
            Auth = s.Auth;
            Username = s.Username;
            HasStoredSecret = HasSecret (s.Auth);
            BuddyName = s.BuddyName;
            BuddyColour = s.BuddyColour;
            Look = s.Look;
            Motion = s.ReduceMotion switch { true => MotionPreference.Reduce, false => MotionPreference.Full, null => MotionPreference.System };
            SoundsEnabled = s.SoundsEnabled;
            SilenceMinutes = (int)s.SilenceWindow.TotalMinutes;
            NightEnabled = s.Night.Enabled;
            NightStart = s.Night.Start;
            NightEnd = s.Night.End;
            AlarmPriority = s.Interpretation.AlarmPriority;
            WarningPriority = s.Interpretation.WarningPriority;
            StripLeadingEmoji = s.Interpretation.StripLeadingEmoji;
            SourceSeparator = s.Interpretation.SourceSeparator;
            TestTitlePattern = s.Interpretation.TestTitlePattern;
            TemperaturePattern = s.Interpretation.TemperaturePattern;
            SavedMessage = null;
        }

        protected override void OnDisposed () => testing?.Cancel ();
    }
}
