using System.ComponentModel;
using AlertBuddy.Core.Ntfy;
using AlertBuddy.Core.Security;
using AlertBuddy.Core.Settings;
using AlertBuddy.ViewModels.Copy;
using AlertBuddy.ViewModels.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AlertBuddy.ViewModels.Screens
{
    /// <summary>The first-run steps, in order. This is the one true sequence in the app, so it is numbered (PLAN.md section 8.4).</summary>
    public enum FirstRunStep
    {
        /// <summary>1. The child names the buddy.</summary>
        NameBuddy = 1,

        /// <summary>2. A grown-up sets the PIN.</summary>
        GrownUpGate = 2,

        /// <summary>3. Server, topic and sign-in, with a test.</summary>
        Server = 3,

        /// <summary>4. Permissions (the Android wizard arrives with milestone 4).</summary>
        Permissions = 4,

        /// <summary>5. A practice run.</summary>
        Practice = 5,
    }

    /// <summary>First run: name the buddy, then a grown-up sets the PIN, the server and the permissions (PLAN.md sections 4.3 and 9).</summary>
    public sealed partial class FirstRunViewModel : ScreenViewModel
    {
        private readonly SettingsService settings;
        private readonly ISecretStore secrets;
        private readonly IConnectionTester tester;
        private readonly IListenerControl listener;
        private readonly INavigator navigator;
        private readonly IBackgroundListener background;
        private CancellationTokenSource? testing;

        [ObservableProperty]
        private FirstRunStep step = FirstRunStep.NameBuddy;

        [ObservableProperty]
        private string buddyName = "";

        [ObservableProperty]
        private BuddyColour buddyColour = BuddyColour.Sky;

        [ObservableProperty]
        private string pin = "";

        [ObservableProperty]
        private string pinConfirm = "";

        [ObservableProperty]
        private string serverUrl = "";

        [ObservableProperty]
        private string topic = "";

        [ObservableProperty]
        private AuthMode auth = AuthMode.None;

        [ObservableProperty]
        private string username = "";

        [ObservableProperty]
        private string secret = "";

        [ObservableProperty]
        private string? testResult;

        [ObservableProperty]
        private bool isTesting;

        /// <summary>Creates first run.</summary>
        public FirstRunViewModel (
            SettingsService settings,
            ISecretStore secrets,
            IConnectionTester tester,
            IListenerControl listener,
            INavigator navigator,
            IBackgroundListener background)
        {
            this.settings = settings ?? throw new ArgumentNullException (nameof (settings));
            this.secrets = secrets ?? throw new ArgumentNullException (nameof (secrets));
            this.tester = tester ?? throw new ArgumentNullException (nameof (tester));
            this.listener = listener ?? throw new ArgumentNullException (nameof (listener));
            this.navigator = navigator ?? throw new ArgumentNullException (nameof (navigator));
            this.background = background ?? throw new ArgumentNullException (nameof (background));
        }

        /// <summary>The step as a number, for "Step 3 of 5".</summary>
        public int StepNumber => (int)Step;

        /// <summary>How many steps there are.</summary>
        public int StepCount => 5;

        /// <summary>The safety note, shown on the first step (PLAN.md section 8.10).</summary>
        public string SafetyNote => Words.SafetyNote;

        /// <summary>Why the next step is not available yet, in words that say how to fix it. Null when it is.</summary>
        public string? ValidationMessage => Validate (Step);

        /// <summary>Whether Next may be pressed.</summary>
        public bool CanGoNext => ValidationMessage is null;

        /// <summary>Whether Back may be pressed.</summary>
        public bool CanGoBack => Step > FirstRunStep.NameBuddy;

        /// <summary>The problem with the server address, if any.</summary>
        public string? ServerProblem => string.IsNullOrWhiteSpace (ServerUrl) ? null : NtfyEndpoint.Check (ServerUrl).Problem;

        /// <summary>Why the background listener cannot work yet, for the permissions step. Null when nothing is wrong.</summary>
        public string? PermissionsProblem => background.WhyNot;

        /// <summary>Whether the address is plain http on a private network, which is shown with an "unencrypted" badge.</summary>
        public bool ServerIsUnencrypted => NtfyEndpoint.Check (ServerUrl) is { IsValid: true, Unencrypted: true };

        // Every input that can change what Next allows also changes what the view should say about it.
        protected override void OnPropertyChanged (PropertyChangedEventArgs e)
        {
            base.OnPropertyChanged (e);

            if (e.PropertyName is nameof (Step) or nameof (BuddyName) or nameof (Pin) or nameof (PinConfirm) or nameof (ServerUrl)
                or nameof (Topic) or nameof (Auth) or nameof (Username) or nameof (Secret) or nameof (IsTesting)) {
                OnPropertyChanged (nameof (ValidationMessage));
                OnPropertyChanged (nameof (CanGoNext));
                OnPropertyChanged (nameof (CanGoBack));
                OnPropertyChanged (nameof (StepNumber));
                OnPropertyChanged (nameof (ServerProblem));
                OnPropertyChanged (nameof (ServerIsUnencrypted));
                NextCommand.NotifyCanExecuteChanged ();
                BackCommand.NotifyCanExecuteChanged ();
                TestConnectionCommand.NotifyCanExecuteChanged ();
            }
        }

        /// <summary>Goes to the next step, or finishes on the last one.</summary>
        [RelayCommand (CanExecute = nameof (CanGoNext))]
        private async Task NextAsync ()
        {
            if (Step < FirstRunStep.Practice) {
                Step++;
                return;
            }

            await FinishAsync ().ConfigureAwait (false);
        }

        [RelayCommand (CanExecute = nameof (CanGoBack))]
        private void Back () => Step--;

        /// <summary>The permissions step's "Later": nothing stops it, and the honest banner on Home says what is missing.</summary>
        [RelayCommand]
        private void Later ()
        {
            if (Step == FirstRunStep.Permissions)
                Step = FirstRunStep.Practice;
        }

        /// <summary>The practice step: a rehearsal before the real thing.</summary>
        [RelayCommand]
        private void TryPractice () => navigator.GoTo<PracticeViewModel> ();

        /// <summary>Tries the server, topic and sign-in, and says what happened in words that say how to fix it.</summary>
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
                // Superseded by a newer test, or the screen was left.
            } finally {
                if (!token.IsCancellationRequested)
                    IsTesting = false;
            }
        }

        private bool CanTest () => !IsTesting && NtfyEndpoint.Check (ServerUrl).IsValid && NtfyTopic.IsValid (Topic);

        private NtfyCredentials CredentialsForTest ()
            => Auth switch {
                AuthMode.Basic when Username.Length > 0 => NtfyCredentials.Basic (Username, Secret),
                AuthMode.Token when Secret.Length > 0 => NtfyCredentials.Bearer (Secret),
                _ => NtfyCredentials.None,
            };

        private async Task FinishAsync ()
        {
            var name = BuddyName.Trim ();

            // The password or token goes to the secret store and never into the settings file. Whichever mode is not in use is cleared, so a
            // secret from an earlier choice does not linger.
            secrets.Remove (SecretKeys.Password);
            secrets.Remove (SecretKeys.Token);
            if (Auth == AuthMode.Basic)
                secrets.Set (SecretKeys.Password, Secret);
            else if (Auth == AuthMode.Token)
                secrets.Set (SecretKeys.Token, Secret);

            settings.Save (settings.Current with {
                BuddyName = name,
                BuddyColour = BuddyColour,
                Pin = PinHasher.Create (Pin),
                ServerUrl = NtfyEndpoint.Check (ServerUrl).BaseUri!.ToString ().TrimEnd ('/'),
                Topic = Topic,
                Auth = Auth,
                Username = Auth == AuthMode.Basic ? Username : "",
                FirstRunComplete = true,
            });

            // The secrets are no longer needed here.
            Pin = "";
            PinConfirm = "";
            Secret = "";

            await listener.RestartAsync ().ConfigureAwait (false);
            navigator.GoHome ();
        }

        private string? Validate (FirstRunStep forStep)
            => forStep switch {
                FirstRunStep.NameBuddy => BuddyName.Trim ().Length is >= 1 and <= 16 ? null : Words.NameNeeded,
                FirstRunStep.GrownUpGate => !PinHasher.IsValidPin (Pin) ? Words.PinFormat : Pin != PinConfirm ? Words.PinMismatch : null,
                FirstRunStep.Server => ValidateServer (),
                _ => null,
            };

        private string? ValidateServer ()
        {
            var endpoint = NtfyEndpoint.Check (ServerUrl);
            if (!endpoint.IsValid)
                return endpoint.Problem;
            if (!NtfyTopic.IsValid (Topic))
                return Words.TopicFormat;
            if (Auth == AuthMode.Basic && Username.Trim ().Length == 0)
                return Words.UsernameNeeded;
            if (Auth != AuthMode.None && Secret.Length == 0)
                return Words.SecretNeeded;
            return null;
        }

        protected override void OnDisposed () => testing?.Cancel ();
    }
}
