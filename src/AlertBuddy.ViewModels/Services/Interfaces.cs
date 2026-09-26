using AlertBuddy.Core.Alerts;
using CommunityToolkit.Mvvm.ComponentModel;

namespace AlertBuddy.ViewModels.Services
{
    /// <summary>The sounds the app makes (PLAN.md section 8.8). The heads map each to a generated file.</summary>
    public enum Cue
    {
        /// <summary>The soft press boop.</summary>
        Boop,

        /// <summary>The friendly two rising notes of a warning.</summary>
        Warning,

        /// <summary>The rising, looping siren. Plays on the alarm audio stream where the platform has one.</summary>
        Alarm,

        /// <summary>The three ascending notes of an all clear.</summary>
        AllClear,

        /// <summary>The "it works" cheer for a test message.</summary>
        Cheer,

        /// <summary>A short, quieter version used by Practice mode.</summary>
        Practice,
    }

    /// <summary>
    /// Plays sounds. A thin adapter over the framework's audio (F8, F9) in <c>Shared</c>; view models see only this so tests can fake it.
    /// <see cref="IsSupported"/> is false where nothing can play, and the UI stays honest about it.
    /// </summary>
    public interface ISoundPlayer
    {
        /// <summary>Whether this platform can play anything at all.</summary>
        bool IsSupported { get; }

        /// <summary>Plays a cue once.</summary>
        void Play (Cue cue);

        /// <summary>Plays a cue over and over until <see cref="StopLoop"/>. The siren.</summary>
        void StartLoop (Cue cue);

        /// <summary>Stops a looping cue. Safe to call when nothing is looping.</summary>
        void StopLoop ();
    }

    /// <summary>Vibration (F13).</summary>
    public interface IHaptics
    {
        /// <summary>Whether this device can vibrate.</summary>
        bool IsSupported { get; }

        /// <summary>A light tap for a button press.</summary>
        void Tap ();

        /// <summary>The alarm pattern, repeating until <see cref="Stop"/>.</summary>
        void Alarm ();

        /// <summary>Stops a running pattern.</summary>
        void Stop ();
    }

    /// <summary>Keeps the screen on, for bedside mode (F12).</summary>
    public interface IKeepAwake
    {
        /// <summary>Whether the screen is being held on.</summary>
        bool Enabled { get; set; }
    }

    /// <summary>Shows and removes system notifications (F14). An alarm's notification stays until acknowledged or resolved.</summary>
    public interface IAlertNotifier
    {
        /// <summary>Shows or updates the notification for an alert.</summary>
        void Show (Alert alert);

        /// <summary>Removes the notification for an alert.</summary>
        void Clear (string alertId);
    }

    /// <summary>App lifecycle and the back button (F10, F11).</summary>
    public interface ILifecycle
    {
        /// <summary>The app came to the foreground.</summary>
        event Action? Resumed;

        /// <summary>The app went to the background.</summary>
        event Action? Paused;

        /// <summary>
        /// The back button or gesture. A handler returns true if it handled it, so the app stays open; if none does, the platform's normal
        /// behaviour (leaving the app) applies.
        /// </summary>
        event Func<bool>? BackPressed;
    }

    /// <summary>
    /// Whatever keeps the listener alive when the app is not open: the Android foreground service. Desktop has nothing to report and is
    /// simply "fine".
    /// </summary>
    public interface IBackgroundListener
    {
        /// <summary>Whether alerts will arrive with the app closed.</summary>
        bool CanListenInBackground { get; }

        /// <summary>Why not, in a phrase for the honest banner on Home, or null when nothing is wrong.</summary>
        string? WhyNot { get; }

        /// <summary>Starts the background listener.</summary>
        void Start ();

        /// <summary>Stops it.</summary>
        void Stop ();
    }

    /// <summary>
    /// Runs work on the UI thread. Core and the Android service raise events on background threads; a view model handles them through
    /// this and never touches a control. Faked with a synchronous version in tests.
    /// </summary>
    public interface IUiDispatcher
    {
        /// <summary>Queues an action for the UI thread.</summary>
        void Post (Action action);
    }

    /// <summary>Runs an action later, or repeatedly. Over a <see cref="TimeProvider"/> so tests move time by hand.</summary>
    public interface IScheduler
    {
        /// <summary>Runs <paramref name="action"/> once after <paramref name="delay"/>. Dispose the result to cancel it.</summary>
        IDisposable Schedule (TimeSpan delay, Action action);

        /// <summary>Runs <paramref name="action"/> every <paramref name="period"/>. Dispose the result to stop it.</summary>
        IDisposable Every (TimeSpan period, Action action);
    }

    /// <summary>
    /// Moves between screens. View-model first: the view host holds a registry from view model type to view and swaps pages when
    /// <see cref="Current"/> changes (PLAN.md section 7.5).
    /// </summary>
    public interface INavigator
    {
        /// <summary>The screen being shown.</summary>
        ObservableObject Current { get; }

        /// <summary>Raised when <see cref="Current"/> changes.</summary>
        event Action? CurrentChanged;

        /// <summary>Whether there is a screen to go back to.</summary>
        bool CanGoBack { get; }

        /// <summary>Shows a new instance of a registered screen.</summary>
        void GoTo<T> () where T : ObservableObject;

        /// <summary>Shows a screen that was built with arguments, such as the detail for one alert.</summary>
        void Show (ObservableObject viewModel);

        /// <summary>Returns to the previous screen. Does nothing at the root.</summary>
        void GoBack ();

        /// <summary>Returns to Home, dropping every screen above it.</summary>
        void GoHome ();
    }
}
