using AlertBuddy.Core.Localization;

namespace AlertBuddy.ViewModels.Copy
{
    /// <summary>
    /// Every sentence the app says, in one table so it can be translated (PLAN.md section 13, question 3) and so the voice stays one
    /// voice: plain, warm, short, active. Sentence case, plain verbs, name the room, say what to do, one exclamation mark at most in the
    /// whole app. Errors say what happened and how to fix it. An action keeps one name through the flow.
    /// </summary>
    public static class Words
    {
        // ---- the buddy speaks (PLAN.md section 8.10) ----

        /// <summary>Home, calm.</summary>
        public static string AllQuiet (string buddy) => Loc.F ("All quiet. {0} is keeping watch.", buddy);

        /// <summary>Home, with nothing to say because the house cannot be heard.</summary>
        public static string CannotHear (string buddy) => Loc.F ("{0} can't hear the house right now.", buddy);

        /// <summary>Home, one warning.</summary>
        public static string GettingWarm (string source) => Loc.F ("The {0} is getting warm.", Place (source));

        /// <summary>Home, several warnings.</summary>
        public static string SeveralWarm (int count) => Loc.F ("{0} places are getting warm.", count);

        /// <summary>A warning about something that is not a temperature: the app understands any ntfy topic, and "too hot" would be untrue.</summary>
        public static string NeedsALook (string source) => Loc.F ("The {0} needs a look.", Place (source));

        /// <summary>Several warnings, not all of them about heat.</summary>
        public static string SeveralNeedALook (int count) => Loc.F ("{0} places need a look.", count);

        /// <summary>The all clear for something that was not a temperature.</summary>
        public static string AllClearNeutral (string source) => Loc.F ("All clear. The {0} is fine again.", Place (source));

        /// <summary>The alarm takeover, first line.</summary>
        public static string TellAGrownUpNow => Loc.T ("Tell a grown-up now.");

        /// <summary>The alarm takeover, second line.</summary>
        public static string TooHot (string source) => Loc.F ("The {0} is too hot.", Place (source));

        /// <summary>The child's button.</summary>
        public static string ToldAGrownUp => Loc.T ("I told a grown-up");

        /// <summary>After the child's button.</summary>
        public static string ThankYou => Loc.T ("Thank you. A grown-up is on it.");

        /// <summary>The grown-up's button. It keeps this one name everywhere.</summary>
        public static string GotIt => Loc.T ("Got it");

        /// <summary>The all clear.</summary>
        public static string AllClear (string source) => Loc.F ("All clear. The {0} is cool again.", Place (source));

        /// <summary>What a screen reader says for the buddy: its state in words, because the picture is not read.</summary>
        public static string BeaconDescription (Screens.BeaconMood mood) => mood switch {
            Screens.BeaconMood.Asleep => Loc.T ("Asleep. Not listening right now."),
            Screens.BeaconMood.Watching => Loc.T ("Watching. All quiet."),
            Screens.BeaconMood.Warning => Loc.T ("Something needs a look."),
            Screens.BeaconMood.Alarm => Loc.T ("Alarm. Tell a grown-up now."),
            Screens.BeaconMood.Reassured => Loc.T ("A grown-up is on it."),
            _ => "",
        };

        /// <summary>The invented place a voice sample is about.</summary>
        public static string VoiceSampleSource => Loc.T ("Sample room");

        /// <summary>The takeover's stopwatch, before the time.</summary>
        public static string AlarmSounding => Loc.T ("Sounding for");

        /// <summary>What the voice says after the tone: which place, and what to do.</summary>
        public static string AlarmAnnouncement (string source) => Loc.F ("Alert. {0}. {1}", source, TellAGrownUpNow);

        /// <summary>Shown when a sound is asked for and a grown-up has switched sounds off.</summary>
        public static string SoundsAreOff => Loc.T ("Sounds are switched off.");

        /// <summary>Practice mode's banner.</summary>
        public static string PracticeBanner => Loc.T ("Practice. Nothing is really hot.");

        /// <summary>The Alert Book with nothing in it: an invitation, not a dead end.</summary>
        public static string AlertBookEmpty => Loc.T ("No alerts yet. When something needs a look, it shows up here.");

        /// <summary>The honest banner when the app cannot listen in the background: it says which thing is wrong, not just that something is.</summary>
        /// <param name="buddy">The buddy's name.</param>
        /// <param name="reason">What is wrong, as a sentence that ends with a full stop.</param>
        public static string CannotListenInBackground (string buddy, string reason) => Loc.F ("{0} may miss an alert. {1} A grown-up can fix this in settings.", buddy, reason);

        /// <summary>The safety note, on About and on first run.</summary>
        public static string SafetyNote => Loc.T ("Alert Buddy is a helper. It does not replace smoke or heat alarms.");

        // ---- the grown-up gate ----

        /// <summary>The gate's first step.</summary>
        public static string GateHold => Loc.T ("Press and hold. This part is for grown-ups.");

        /// <summary>The PIN pad's prompt.</summary>
        public static string GateEnterPin => Loc.T ("Enter the PIN.");

        /// <summary>A wrong PIN.</summary>
        public static string GatePinWrong => Loc.T ("That PIN didn't match. Try again.");

        /// <summary>Too many wrong PINs.</summary>
        public static string GateLocked => Loc.T ("Too many tries. Wait a little, then try again.");

        // ---- first run and settings: what is wrong and how to fix it ----

        /// <summary>The buddy needs a name.</summary>
        public static string NameNeeded => Loc.T ("Give your buddy a name, up to 16 letters.");

        /// <summary>A PIN is four digits.</summary>
        public static string PinFormat => Loc.T ("The PIN is four numbers.");

        /// <summary>The two PINs differ.</summary>
        public static string PinMismatch => Loc.T ("The two PINs don't match.");

        /// <summary>A topic name is not usable.</summary>
        public static string TopicFormat => Loc.T ("A topic is letters, numbers, - and _, up to 64 characters.");

        /// <summary>A user name is needed for password sign-in.</summary>
        public static string UsernameNeeded => Loc.T ("Enter the user name.");

        /// <summary>A password or token is needed.</summary>
        public static string SecretNeeded => Loc.T ("Enter the password or token.");

        /// <summary>The connection test is running.</summary>
        public static string Testing => Loc.T ("Testing the connection.");

        // ---- connection (one sentence per state) ----

        /// <summary>Live.</summary>
        public static string Listening (TimeSpan? sinceLastHeard)
            => sinceLastHeard is { } ago ? Loc.F ("Listening. Last heard {0} ago.", TimeAgo (ago)) : Loc.T ("Listening.");

        /// <summary>The first connection is being made.</summary>
        public static string Connecting => Loc.T ("Connecting to the house.");

        /// <summary>Reconnecting or offline.</summary>
        public static string CannotReachTheHouse => Loc.T ("Can't reach the house. Trying again.");

        /// <summary>A refused sign-in.</summary>
        public static string SignInRefused => Loc.T ("The server didn't accept the sign-in. Ask a grown-up to check it in settings.");

        /// <summary>A topic that does not exist.</summary>
        public static string TopicNotFound => Loc.T ("The server doesn't know this topic. Ask a grown-up to check it in settings.");

        /// <summary>A certificate problem.</summary>
        public static string CertificateProblem => Loc.T ("The secure connection failed. Ask a grown-up to check the server's certificate.");

        /// <summary>An address that does not work.</summary>
        public static string AddressProblem => Loc.T ("The server address doesn't work. Ask a grown-up to check it in settings.");

        /// <summary>Nothing has been set up yet.</summary>
        public static string NotSetUp => Loc.T ("Not set up yet. A grown-up can do it in settings.");

        // ---- helpers ----

        /// <summary>
        /// A source as it reads in a sentence: "Workshop" becomes "workshop" in "The workshop is getting warm". Only the first letter, and
        /// only when the second is not a capital, so "NAS" and "TV room" keep their names.
        /// </summary>
        public static string Room (string source)
        {
            if (string.IsNullOrEmpty (source) || !char.IsUpper (source[0]) || (source.Length > 1 && char.IsUpper (source[1])))
                return source;

            return char.ToLowerInvariant (source[0]) + source[1..];
        }

        /// <summary>
        /// A source as it fills a sentence: lower-cased after "The" in English ("The workshop is too hot"), and left as it was named in French, where
        /// the sentence puts it first ("Workshop : il fait trop chaud") and so needs no article.
        /// </summary>
        public static string Place (string source) => Loc.IsFrench ? source : Room (source);

        /// <summary>How long ago, in a few plain words: "a few seconds", "3 min", "2 h".</summary>
        public static string TimeAgo (TimeSpan age)
        {
            if (age < TimeSpan.FromSeconds (45))
                return Loc.T ("a few seconds");
            if (age < TimeSpan.FromMinutes (60))
                return Loc.F ("{0} min", Math.Max (1, (int)Math.Round (age.TotalMinutes)));
            if (age < TimeSpan.FromHours (48))
                return Loc.F ("{0} h", (int)Math.Round (age.TotalHours));
            return Loc.F ("{0} days", (int)Math.Round (age.TotalDays));
        }

        /// <summary>A temperature as a child reads it: whole degrees.</summary>
        public static string Degrees (double celsius) => Loc.F ("{0} degrees", Math.Round (celsius).ToString ("0"));
    }
}
