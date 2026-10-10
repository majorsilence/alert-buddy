using AlertBuddy.Core.Localization;
using System.Globalization;
using System.Text.RegularExpressions;
using AlertBuddy.Core.Alerts;
using AlertBuddy.Core.Ntfy;

namespace AlertBuddy.Core.Interpretation
{
    /// <summary>What a message means to the app.</summary>
    public enum InterpretedKind
    {
        /// <summary>A warning or an alarm: something needs a look.</summary>
        Alert,

        /// <summary>A calm message. If its source has an open alert this is the all clear.</summary>
        Clear,

        /// <summary>A test message: cheer, and create no alert.</summary>
        Test,
    }

    /// <summary>A message read as an alert event, ready for the <see cref="Store.AlertStore"/>.</summary>
    /// <param name="MessageId">The ntfy message id, for de-duplication and resuming.</param>
    /// <param name="Time">When the server accepted the message.</param>
    /// <param name="Kind">Alert, all clear or test.</param>
    /// <param name="Level">Calm, warning or alarm, from the priority.</param>
    /// <param name="Source">The thing alerting: the part of the title before the separator, or the whole title.</param>
    /// <param name="Title">The title with leading emoji removed.</param>
    /// <param name="Body">The message text.</param>
    /// <param name="Temperature">The temperature in degrees Celsius, when the body carried one.</param>
    public sealed record InterpretedMessage (
        string MessageId,
        DateTimeOffset Time,
        InterpretedKind Kind,
        AlertLevel Level,
        string Source,
        string Title,
        string Body,
        double? Temperature);

    /// <summary>Reads <see cref="NtfyMessage"/>s as alerts according to <see cref="InterpretationSettings"/> (PLAN.md sections 5.1 and 5.4).</summary>
    public sealed class AlertInterpreter
    {
        // A pattern is user input. It must not be able to hang a listener that runs for weeks, so every match has a deadline.
        private static readonly TimeSpan MatchTimeout = TimeSpan.FromMilliseconds (250);

        private readonly InterpretationSettings settings;
        private readonly Regex testTitle;
        private readonly Regex temperature;

        /// <summary>Problems found in the settings' patterns, for the settings screen to show. Empty when all is well.</summary>
        public IReadOnlyList<string> Problems { get; }

        /// <summary>Creates an interpreter. A pattern that does not compile falls back to its default and is reported in <see cref="Problems"/>.</summary>
        public AlertInterpreter (InterpretationSettings? settings = null)
        {
            this.settings = settings ?? InterpretationSettings.Default;

            var problems = new List<string> ();
            testTitle = Compile (this.settings.TestTitlePattern, InterpretationSettings.DefaultTestTitlePattern, "test title", problems);
            temperature = Compile (this.settings.TemperaturePattern, InterpretationSettings.DefaultTemperaturePattern, "temperature", problems);
            Problems = problems;
        }

        /// <summary>Reads one message.</summary>
        public InterpretedMessage Interpret (NtfyMessage message)
        {
            ArgumentNullException.ThrowIfNull (message);

            var title = (message.Title ?? "").Trim ();
            if (settings.StripLeadingEmoji)
                title = EmojiStripper.StripLeading (title).Trim ();

            // ntfy gives a message with no title the topic as its title on the phone; here the topic is at least stable, and a card
            // with no name at all would be worse.
            if (title.Length == 0)
                title = message.Topic;

            var source = SourceOf (title);
            var body = message.Message.Trim ();
            var level = LevelOf (message.Priority);

            var kind = IsTest (title) ? InterpretedKind.Test
                : level == AlertLevel.Calm ? InterpretedKind.Clear
                : InterpretedKind.Alert;

            return new InterpretedMessage (message.Id, message.Time, kind, level, source, title, body, TemperatureOf (body));
        }

        private AlertLevel LevelOf (int priority)
            => priority >= settings.AlarmPriority ? AlertLevel.Alarm
             : priority >= settings.WarningPriority ? AlertLevel.Warning
             : AlertLevel.Calm;

        private string SourceOf (string title)
        {
            var separator = settings.SourceSeparator;
            if (string.IsNullOrEmpty (separator))
                return title;

            var index = title.IndexOf (separator, StringComparison.Ordinal);
            var source = index > 0 ? title[..index].Trim () : title;
            return source.Length > 0 ? source : title;
        }

        private bool IsTest (string title)
        {
            try {
                return testTitle.IsMatch (title);
            } catch (RegexMatchTimeoutException) {
                return false;
            }
        }

        private double? TemperatureOf (string body)
        {
            try {
                var match = temperature.Match (body);
                if (!match.Success || match.Groups.Count < 2)
                    return null;

                // "41,2" is a decimal comma; the number is always read in the invariant culture so a phone set to a different
                // language does not change what a temperature means.
                var text = match.Groups[1].Value.Replace (',', '.');
                return double.TryParse (text, NumberStyles.Float, CultureInfo.InvariantCulture, out var value) ? value : null;
            } catch (RegexMatchTimeoutException) {
                return null;
            }
        }

        private static Regex Compile (string pattern, string fallback, string name, List<string> problems)
        {
            try {
                return new Regex (pattern, RegexOptions.CultureInvariant, MatchTimeout);
            } catch (ArgumentException ex) {
                problems.Add (Loc.F ("The {0} pattern is not valid ({1}). The default is used instead.", Loc.T (name), ex.Message));
                return new Regex (fallback, RegexOptions.CultureInvariant, MatchTimeout);
            }
        }
    }
}
