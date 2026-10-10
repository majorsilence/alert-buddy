using AlertBuddy.Core.Settings;
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

        /// <summary>Three bursts, a pause, repeated.</summary>
        Code3,

        /// <summary>A steady 120-a-minute beat.</summary>
        MarchTime,

        /// <summary>One unbroken tone, looped.</summary>
        Continuous,

        /// <summary>The attention chime that opens a voice evacuation message; the spoken words come from <see cref="ISpeaker"/>.</summary>
        VoiceEvacuation,
    }

    /// <summary>
    /// Plays sounds. A thin adapter over the framework's audio (F8, F9) in <c>Shared</c>; view models see only this so tests can fake it.
    /// <see cref="IsSupported"/> is false where nothing can play, and the UI stays honest about it.
    /// </summary>
    public interface ISoundPlayer
    {
        /// <summary>Whether this platform can play anything at all.</summary>
        bool IsSupported { get; }

        /// <summary>Plays a cue once. <paramref name="volume"/> runs from 0 to 1; Practice plays the real tones quietly.</summary>
        void Play (Cue cue, double volume = 1);

        /// <summary>Plays a cue over and over until <see cref="StopLoop"/>. The siren. <paramref name="volume"/> runs from 0 to 1; Practice loops quietly.</summary>
        void StartLoop (Cue cue, double volume = 1);

        /// <summary>Stops a looping cue. Safe to call when nothing is looping.</summary>
        void StopLoop ();
    }

    /// <summary>What a platform says about a voice's sex. Android and macOS do not say.</summary>
    public enum VoiceSex
    {
        /// <summary>Not said.</summary>
        Unknown,

        /// <summary>A man's voice.</summary>
        Male,

        /// <summary>A woman's voice.</summary>
        Female,
    }

    /// <summary>An installed voice, as a grown-up picks it in Settings.</summary>
    /// <param name="Id">What the platform calls it.</param>
    /// <param name="Name">A name to show.</param>
    /// <param name="Locale">Its language as a tag such as "en-GB", or empty.</param>
    /// <param name="Sex">Its sex where the platform says.</param>
    /// <param name="RequiresNetwork">Whether it needs a connection to speak. An alarm must be heard with none, so Settings leaves these out.</param>
    public sealed record VoiceOption (string Id, string Name, string Locale, VoiceSex Sex, bool RequiresNetwork = false);

    /// <summary>Reads a line aloud (F15). Optional: a head with no voice supplies none and the setting is not offered.</summary>
    public interface ISpeaker
    {
        /// <summary>Whether this device can speak at all.</summary>
        bool IsSupported { get; }

        /// <summary>The voices installed on this device, or none when the device cannot list them.</summary>
        Task<IReadOnlyList<VoiceOption>> ListVoicesAsync ();

        /// <summary>
        /// Says a line and returns at once. Never throws: a device that cannot speak just stays quiet. <paramref name="volume"/> runs from 0 to 1.
        /// <paramref name="voiceId"/> is an installed voice from <see cref="ListVoicesAsync"/>; with none, the device's own voice is used and
        /// <paramref name="voice"/> sets its pitch.
        /// </summary>
        void Speak (string text, VoiceType voice = VoiceType.Standard, double volume = 1, string? voiceId = null);
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

    /// <summary>What a grown-up may need to allow so an alarm is heard (PLAN.md section 6.2's onboarding wizard).</summary>
    public enum PermissionKind
    {
        /// <summary>Showing notifications at all.</summary>
        Notifications,

        /// <summary>Letting an alarm take over the screen.</summary>
        FullScreenAlarm,

        /// <summary>Checking the alarm volume, with a test sound. Not a permission: it has no granted state.</summary>
        AlarmVolume,

        /// <summary>Not being stopped by the battery manager while the screen is off.</summary>
        BatteryOptimisation,

        /// <summary>Letting an alarm ring through Do Not Disturb.</summary>
        DoNotDisturb,
    }

    /// <summary>One thing the wizard can ask about.</summary>
    /// <param name="Kind">Which one.</param>
    /// <param name="Title">What it is, in a few words.</param>
    /// <param name="Why">Why it matters, in a sentence a grown-up can act on.</param>
    /// <param name="Granted">Whether it is done, or null for a step that is a check rather than a permission.</param>
    public sealed record PermissionItem (PermissionKind Kind, string Title, string Why, bool? Granted);

    /// <summary>
    /// What the platform lets a grown-up allow, and the way to its settings. Android has several; desktop and the browser have none and
    /// pass nothing. The answers can change while the app is away (the person went to system settings), so the view model asks again when
    /// the app comes back.
    /// </summary>
    public interface IPermissionGuide
    {
        /// <summary>The steps, in the order to do them, with their present state.</summary>
        IReadOnlyList<PermissionItem> Items { get; }

        /// <summary>Asks for it, or opens the system screen where it is allowed. Never throws.</summary>
        void Open (PermissionKind kind);
    }
}
