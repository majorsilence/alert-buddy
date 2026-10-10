using AlertBuddy.Core.Interpretation;
using AlertBuddy.Core.Localization;
using AlertBuddy.Core.Security;
using AlertBuddy.Core.Store;

namespace AlertBuddy.Core.Settings
{
    /// <summary>How the app signs in to the server.</summary>
    public enum AuthMode
    {
        /// <summary>Not at all.</summary>
        None,

        /// <summary>A user name and password. The password lives in the secret store.</summary>
        Basic,

        /// <summary>An access token. The token lives in the secret store.</summary>
        Token,
    }

    /// <summary>Day, Night, or follow the system.</summary>
    public enum LookPreference
    {
        /// <summary>Follow the system setting.</summary>
        Auto,

        /// <summary>The lavender-white ground.</summary>
        Day,

        /// <summary>The Blueberry ground.</summary>
        Night,
    }

    /// <summary>The colours a child may give their buddy. None is one of the four state colours, which are used only for state.</summary>
    public enum BuddyColour
    {
        /// <summary>A sky blue.</summary>
        Sky,

        /// <summary>A plum.</summary>
        Plum,

        /// <summary>A slate grey-blue.</summary>
        Slate,

        /// <summary>A warm sand.</summary>
        Sand,
    }

    /// <summary>The sounds a grown-up may choose for the alarm, and for Practice. Named for the familiar building-alarm signals.</summary>
    public enum AlarmTone
    {
        /// <summary>The app's own rising whoop, looped. The default.</summary>
        Whoop,

        /// <summary>Three short bursts, a pause, and again (the "temporal 3" pattern of ISO 8201).</summary>
        Code3,

        /// <summary>A steady beat of 120 short tones a minute.</summary>
        MarchTime,

        /// <summary>One unbroken tone.</summary>
        Continuous,

        /// <summary>An attention chime followed by a spoken instruction, repeated. The words come from the device's voice.</summary>
        VoiceEvacuation,
    }

    /// <summary>The kind of voice that speaks an alert aloud. The platform's own voice is lowered or raised in pitch; a particular installed voice cannot yet be picked (majorsilence/Majorsilence.Forms#456).</summary>
    public enum VoiceType
    {
        /// <summary>A lower voice, nearer a man's. The default for the spoken alarm.</summary>
        Deep,

        /// <summary>The voice as the device has it.</summary>
        Standard,

        /// <summary>A higher voice.</summary>
        Light,
    }

    /// <summary>
    /// Everything a grown-up can configure (PLAN.md sections 4.3 and 7.4). A JSON file in the app data directory. Secrets are not in it:
    /// the password and the token live in the <see cref="ISecretStore"/>.
    /// </summary>
    public sealed record AppSettings
    {
        /// <summary>The ntfy server, for example <c>https://ntfy.example.com</c>.</summary>
        public string ServerUrl { get; init; } = "";

        /// <summary>The topic to listen to, for example <c>home-alerts</c>.</summary>
        public string Topic { get; init; } = "";

        /// <summary>How to sign in.</summary>
        public AuthMode Auth { get; init; } = AuthMode.None;

        /// <summary>The user name when <see cref="Auth"/> is <see cref="AuthMode.Basic"/>.</summary>
        public string Username { get; init; } = "";

        /// <summary>The buddy's name, chosen by the child and used in the speech bubble.</summary>
        public string BuddyName { get; init; } = "Pip";

        /// <summary>The buddy's colour.</summary>
        public BuddyColour BuddyColour { get; init; } = BuddyColour.Sky;

        /// <summary>Day, Night or Auto.</summary>
        public LookPreference Look { get; init; } = LookPreference.Auto;

        /// <summary>Overrides the system's reduced-motion setting: true forces it on, false forces it off, null follows the system.</summary>
        public bool? ReduceMotion { get; init; }

        /// <summary>How long a repeated alarm stays quiet after the child acknowledges it.</summary>
        public TimeSpan SilenceWindow { get; init; } = TimeSpan.FromMinutes (10);

        /// <summary>How far back to ask the server for history on first connect and after a long gap. ntfy caches 12 hours by default.</summary>
        public TimeSpan ReplayWindow { get; init; } = TimeSpan.FromHours (12);

        /// <summary>When warnings and all clears are quiet.</summary>
        public NightPolicy Night { get; init; } = new ();

        /// <summary>How messages are read as alerts.</summary>
        public InterpretationSettings Interpretation { get; init; } = new ();

        /// <summary>The grown-up PIN, stored only as a salted hash. Null until first run sets it.</summary>
        public PinCredential? Pin { get; init; }

        /// <summary>Whether first run has finished, so the app opens on Home rather than on the setup steps.</summary>
        public bool FirstRunComplete { get; init; }

        /// <summary>Whether sounds play at all. Practice mode and the alarm respect it; a grown-up can silence the app on a tablet in a meeting.</summary>
        public bool SoundsEnabled { get; init; } = true;

        /// <summary>The sound the alarm makes. Warnings and all clears keep their own friendly cues.</summary>
        public AlarmTone AlarmTone { get; init; } = AlarmTone.Whoop;

        /// <summary>
        /// The sound Practice plays for each step, quietly. Null follows the alarm: the real warning sound, the alarm tone, and the all clear
        /// sound, so Practice rehearses what will happen. <see cref="PracticeGentle"/> picks the short practice cue instead.
        /// </summary>
        public AlarmTone? PracticeTone { get; init; }

        /// <summary>Whether Practice plays only its short, gentle cue.</summary>
        public bool PracticeGentle { get; init; }

        /// <summary>The installed voice that speaks, by its platform id; null lets the platform choose, with the <see cref="Voice"/> pitch applied.</summary>
        public string? VoiceId { get; init; }

        /// <summary>The language of the app: follow the device, or English, or French.</summary>
        public AppLanguage Language { get; init; } = AppLanguage.System;

        /// <summary>The voice that speaks the alarm announcement and reads alerts aloud.</summary>
        public VoiceType Voice { get; init; } = VoiceType.Deep;

        /// <summary>Whether a new warning or alarm is also read aloud, for a child still learning to read. Off until a grown-up turns it on.</summary>
        public bool ReadAloud { get; init; }
    }
}
