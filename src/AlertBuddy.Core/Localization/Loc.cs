using System.Globalization;

namespace AlertBuddy.Core.Localization
{
    /// <summary>The language the app speaks: follow the device, or one of the two it is written in.</summary>
    public enum AppLanguage
    {
        /// <summary>Follow the device's language: French on a French device, English on anything else.</summary>
        System,

        /// <summary>English.</summary>
        English,

        /// <summary>French.</summary>
        French,
    }

    /// <summary>
    /// The app's words in the language in use. The English sentence is the key: <see cref="T"/> returns it as it is for English and looks the
    /// French up in <see cref="French"/>, falling back to the English where there is none, so a missing translation shows a readable
    /// sentence rather than a blank or a code. A test fails for any sentence in the source that the table lacks.
    /// </summary>
    public static class Loc
    {
        private static volatile bool french;

        /// <summary>Raised after the language in use changes, so a screen already on show can be rebuilt.</summary>
        public static event Action? Changed;

        /// <summary>Whether French is in use.</summary>
        public static bool IsFrench => french;

        /// <summary>The language code of the language in use: "en" or "fr".</summary>
        public static string Code => french ? "fr" : "en";

        /// <summary>The culture dates and numbers are written in.</summary>
        public static CultureInfo Culture => french ? CultureInfo.GetCultureInfo ("fr-FR") : CultureInfo.GetCultureInfo ("en-GB");

        /// <summary>
        /// Chooses the language. <paramref name="deviceLanguage"/> is the device's language tag ("fr-CA", "en-US"), used when the person left it on
        /// <see cref="AppLanguage.System"/>; anything that is not French is English.
        /// </summary>
        public static void Use (AppLanguage preference, string? deviceLanguage = null)
        {
            var wantFrench = preference switch {
                AppLanguage.French => true,
                AppLanguage.English => false,
                _ => deviceLanguage is not null && deviceLanguage.StartsWith ("fr", StringComparison.OrdinalIgnoreCase),
            };

            if (wantFrench == french)
                return;

            french = wantFrench;
            Changed?.Invoke ();
        }

        /// <summary>A sentence in the language in use.</summary>
        public static string T (string english)
            => french && FrenchTable.Words.TryGetValue (english, out var translated) ? translated : english;

        /// <summary>A sentence with values in it ("Step {0} of {1}"), in the language in use, with numbers written for its culture.</summary>
        public static string F (string english, params object[] values) => string.Format (Culture, T (english), values);
    }
}
