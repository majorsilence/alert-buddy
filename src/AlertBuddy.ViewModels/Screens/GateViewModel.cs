using AlertBuddy.Core.Security;
using AlertBuddy.Core.Settings;
using AlertBuddy.ViewModels.Copy;
using AlertBuddy.ViewModels.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AlertBuddy.ViewModels.Screens
{
    /// <summary>Where the gate is.</summary>
    public enum GatePhase
    {
        /// <summary>Waiting for the press-and-hold.</summary>
        Hold,

        /// <summary>Waiting for the PIN.</summary>
        Pin,

        /// <summary>Closed after too many wrong PINs.</summary>
        Locked,
    }

    /// <summary>
    /// The grown-up gate (PLAN.md section 4.3): press and hold about two seconds, then a PIN pad. It is a gate against a curious child, not
    /// security. The hold is timed by the view's hold button, which calls <see cref="HoldCompletedCommand"/> when its ring is full.
    /// </summary>
    public sealed partial class GateViewModel : ScreenViewModel
    {
        private readonly SettingsService settings;
        private readonly INavigator navigator;
        private readonly IScheduler scheduler;
        private readonly GateLock gateLock;
        private readonly Action onUnlocked;
        private string entered = "";

        [ObservableProperty]
        private GatePhase phase = GatePhase.Hold;

        [ObservableProperty]
        private int enteredCount;

        [ObservableProperty]
        private string message = Words.GateHold;

        /// <summary>Creates the gate in front of one action.</summary>
        /// <param name="onUnlocked">What to do once the PIN is right. Runs after the gate has been closed.</param>
        /// <param name="holdDone">The control that opened the gate was itself a press-and-hold, so start on the PIN pad.</param>
        public GateViewModel (SettingsService settings, INavigator navigator, IScheduler scheduler, GateLock gateLock, Action onUnlocked, bool holdDone = false)
        {
            this.settings = settings ?? throw new ArgumentNullException (nameof (settings));
            this.navigator = navigator ?? throw new ArgumentNullException (nameof (navigator));
            this.scheduler = scheduler ?? throw new ArgumentNullException (nameof (scheduler));
            this.gateLock = gateLock ?? throw new ArgumentNullException (nameof (gateLock));
            this.onUnlocked = onUnlocked ?? throw new ArgumentNullException (nameof (onUnlocked));

            if (gateLock.IsLocked)
                EnterLocked ();
            else if (holdDone && settings.Current.Pin is not null) {
                Phase = GatePhase.Pin;
                Message = Words.GateEnterPin;
            }
        }

        /// <summary>The press-and-hold finished.</summary>
        [RelayCommand]
        private void HoldCompleted ()
        {
            if (Phase != GatePhase.Hold)
                return;

            // Before first run has set a PIN there is nothing to ask for.
            if (settings.Current.Pin is null) {
                Unlock ();
                return;
            }

            if (gateLock.IsLocked) {
                EnterLocked ();
                return;
            }

            Phase = GatePhase.Pin;
            Message = Words.GateEnterPin;
        }

        /// <summary>A PIN pad key. Anything that is not a single digit is ignored, so a stray call can never enter a character.</summary>
        [RelayCommand]
        private void PressDigit (string? digit)
        {
            if (Phase != GatePhase.Pin || digit is not { Length: 1 } || !char.IsAsciiDigit (digit[0]))
                return;

            entered += digit;
            EnteredCount = entered.Length;

            if (entered.Length == 4)
                Verify ();
        }

        [RelayCommand]
        private void Backspace ()
        {
            if (Phase != GatePhase.Pin || entered.Length == 0)
                return;

            entered = entered[..^1];
            EnteredCount = entered.Length;
        }

        [RelayCommand]
        private void Cancel () => navigator.GoBack ();

        private void Verify ()
        {
            var pin = entered;
            entered = "";
            EnteredCount = 0;

            if (PinHasher.Verify (pin, settings.Current.Pin)) {
                gateLock.RegisterSuccess ();
                Unlock ();
                return;
            }

            if (gateLock.RegisterFailure ()) {
                EnterLocked ();
                return;
            }

            Message = Words.GatePinWrong;
        }

        private void EnterLocked ()
        {
            Phase = GatePhase.Locked;
            Message = Words.GateLocked;
            entered = "";
            EnteredCount = 0;

            // When the wait is over the gate is offered again, from the start: another hold, then the PIN.
            Own (scheduler.Schedule (GateLock.LockedFor, () => {
                if (!IsDisposed && Phase == GatePhase.Locked && !gateLock.IsLocked) {
                    Phase = GatePhase.Hold;
                    Message = Words.GateHold;
                }
            }));
        }

        private void Unlock ()
        {
            // The gate closes first, so what it unlocks opens on top of the screen the child came from and not on top of the gate.
            navigator.GoBack ();
            onUnlocked ();
        }
    }
}
