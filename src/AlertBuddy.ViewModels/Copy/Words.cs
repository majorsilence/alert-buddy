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
        public static string AllQuiet (string buddy) => $"All quiet. {buddy} is keeping watch.";

        /// <summary>Home, with nothing to say because the house cannot be heard.</summary>
        public static string CannotHear (string buddy) => $"{buddy} can't hear the house right now.";

        /// <summary>Home, one warning.</summary>
        public static string GettingWarm (string source) => $"The {Room (source)} is getting warm.";

        /// <summary>Home, several warnings.</summary>
        public static string SeveralWarm (int count) => $"{count} places are getting warm.";

        /// <summary>A warning about something that is not a temperature: the app understands any ntfy topic, and "too hot" would be untrue.</summary>
        public static string NeedsALook (string source) => $"The {Room (source)} needs a look.";

        /// <summary>Several warnings, not all of them about heat.</summary>
        public static string SeveralNeedALook (int count) => $"{count} places need a look.";

        /// <summary>The all clear for something that was not a temperature.</summary>
        public static string AllClearNeutral (string source) => $"All clear. The {Room (source)} is fine again.";

        /// <summary>The alarm takeover, first line.</summary>
        public const string TellAGrownUpNow = "Tell a grown-up now.";

        /// <summary>The alarm takeover, second line.</summary>
        public static string TooHot (string source) => $"The {Room (source)} is too hot.";

        /// <summary>The child's button.</summary>
        public const string ToldAGrownUp = "I told a grown-up";

        /// <summary>After the child's button.</summary>
        public const string ThankYou = "Thank you. A grown-up is on it.";

        /// <summary>The grown-up's button. It keeps this one name everywhere.</summary>
        public const string GotIt = "Got it";

        /// <summary>The all clear.</summary>
        public static string AllClear (string source) => $"All clear. The {Room (source)} is cool again.";

        /// <summary>Practice mode's banner.</summary>
        public const string PracticeBanner = "Practice. Nothing is really hot.";

        /// <summary>The Alert Book with nothing in it: an invitation, not a dead end.</summary>
        public const string AlertBookEmpty = "No alerts yet. When something needs a look, it shows up here.";

        /// <summary>The honest banner when the app cannot listen in the background: it says which thing is wrong, not just that something is.</summary>
        /// <param name="buddy">The buddy's name.</param>
        /// <param name="reason">What is wrong, as a sentence that ends with a full stop.</param>
        public static string CannotListenInBackground (string buddy, string reason) => $"{buddy} may miss an alert. {reason} A grown-up can fix this in settings.";

        /// <summary>The safety note, on About and on first run.</summary>
        public const string SafetyNote = "Alert Buddy is a helper. It does not replace smoke or heat alarms.";

        // ---- the grown-up gate ----

        /// <summary>The gate's first step.</summary>
        public const string GateHold = "Press and hold. This part is for grown-ups.";

        /// <summary>The PIN pad's prompt.</summary>
        public const string GateEnterPin = "Enter the PIN.";

        /// <summary>A wrong PIN.</summary>
        public const string GatePinWrong = "That PIN didn't match. Try again.";

        /// <summary>Too many wrong PINs.</summary>
        public const string GateLocked = "Too many tries. Wait a little, then try again.";

        // ---- first run and settings: what is wrong and how to fix it ----

        /// <summary>The buddy needs a name.</summary>
        public const string NameNeeded = "Give your buddy a name, up to 16 letters.";

        /// <summary>A PIN is four digits.</summary>
        public const string PinFormat = "The PIN is four numbers.";

        /// <summary>The two PINs differ.</summary>
        public const string PinMismatch = "The two PINs don't match.";

        /// <summary>A topic name is not usable.</summary>
        public const string TopicFormat = "A topic is letters, numbers, - and _, up to 64 characters.";

        /// <summary>A user name is needed for password sign-in.</summary>
        public const string UsernameNeeded = "Enter the user name.";

        /// <summary>A password or token is needed.</summary>
        public const string SecretNeeded = "Enter the password or token.";

        /// <summary>The connection test is running.</summary>
        public const string Testing = "Testing the connection.";

        // ---- connection (one sentence per state) ----

        /// <summary>Live.</summary>
        public static string Listening (TimeSpan? sinceLastHeard)
            => sinceLastHeard is { } ago ? $"Listening. Last heard {TimeAgo (ago)} ago." : "Listening.";

        /// <summary>The first connection is being made.</summary>
        public const string Connecting = "Connecting to the house.";

        /// <summary>Reconnecting or offline.</summary>
        public const string CannotReachTheHouse = "Can't reach the house. Trying again.";

        /// <summary>A refused sign-in.</summary>
        public const string SignInRefused = "The server didn't accept the sign-in. Ask a grown-up to check it in settings.";

        /// <summary>A topic that does not exist.</summary>
        public const string TopicNotFound = "The server doesn't know this topic. Ask a grown-up to check it in settings.";

        /// <summary>A certificate problem.</summary>
        public const string CertificateProblem = "The secure connection failed. Ask a grown-up to check the server's certificate.";

        /// <summary>An address that does not work.</summary>
        public const string AddressProblem = "The server address doesn't work. Ask a grown-up to check it in settings.";

        /// <summary>Nothing has been set up yet.</summary>
        public const string NotSetUp = "Not set up yet. A grown-up can do it in settings.";

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

        /// <summary>How long ago, in a few plain words: "a few seconds", "3 min", "2 h".</summary>
        public static string TimeAgo (TimeSpan age)
        {
            if (age < TimeSpan.FromSeconds (45))
                return "a few seconds";
            if (age < TimeSpan.FromMinutes (60))
                return $"{Math.Max (1, (int)Math.Round (age.TotalMinutes))} min";
            if (age < TimeSpan.FromHours (48))
                return $"{(int)Math.Round (age.TotalHours)} h";
            return $"{(int)Math.Round (age.TotalDays)} days";
        }

        /// <summary>A temperature as a child reads it: whole degrees.</summary>
        public static string Degrees (double celsius) => $"{Math.Round (celsius):0} degrees";
    }
}
