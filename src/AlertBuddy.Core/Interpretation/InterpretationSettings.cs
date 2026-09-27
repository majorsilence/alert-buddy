namespace AlertBuddy.Core.Interpretation
{
    /// <summary>
    /// How ntfy messages are read as alerts (PLAN.md section 5.4). Every field has a sensible default so the app works with any
    /// server's conventions, and every one is a grown-up setting with a reset button.
    /// </summary>
    public sealed record InterpretationSettings
    {
        /// <summary>Priority at or above which a message is an alarm. ntfy's maximum, 5, by default.</summary>
        public int AlarmPriority { get; init; } = 5;

        /// <summary>Priority at or above which (and below the alarm priority) a message is a warning.</summary>
        public int WarningPriority { get; init; } = 4;

        /// <summary>Remove leading emoji and whitespace from titles. Colour emoji may render as boxes on some heads, and the app draws its own icons.</summary>
        public bool StripLeadingEmoji { get; init; } = true;

        /// <summary>Text between the source and the rest of the title: "Workshop: temperature warning" has the source "Workshop".</summary>
        public string SourceSeparator { get; init; } = ": ";

        /// <summary>A title matching this regular expression marks a test message, which cheers and creates no alert.</summary>
        public string TestTitlePattern { get; init; } = DefaultTestTitlePattern;

        /// <summary>
        /// Finds the temperature in a message body: the first number followed by an optional space and C, degrees C, or the word
        /// degrees. The first capture group is the number.
        /// </summary>
        public string TemperaturePattern { get; init; } = DefaultTemperaturePattern;

        /// <summary>The default for <see cref="TestTitlePattern"/>.</summary>
        public const string DefaultTestTitlePattern = @"(?i)\btest\b";

        /// <summary>The default for <see cref="TemperaturePattern"/>.</summary>
        public const string DefaultTemperaturePattern = @"(?<![\w.,])(-?\d+(?:[.,]\d+)?)\s?(?:°\s?C|degrees?|Celsius|C)(?![A-Za-z])";

        /// <summary>The settings a grown-up gets back from "reset".</summary>
        public static InterpretationSettings Default { get; } = new ();
    }
}
