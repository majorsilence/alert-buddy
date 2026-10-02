using System.Reflection;
using Majorsilence.Forms.Drawing;
using Majorsilence.Forms.Drawing.Text;

namespace AlertBuddy.Shared.Theme
{
    /// <summary>
    /// The two type families of PLAN.md section 8.2, bundled so every device draws the same letters: Grandstander (Bold, for the buddy's
    /// words, buttons and headings) and Atkinson Hyperlegible Next (Regular and Bold, for everything else). Both are SIL Open Font
    /// License 1.1; the licences are embedded beside the fonts. Grandstander's file is a static Bold cut from the upstream variable font,
    /// because the variable file's default instance is Thin.
    /// </summary>
    public static class AlertFonts
    {
        private const string DisplayFamily = "Grandstander";
        private const string BodyFamily = "Atkinson Hyperlegible Next";

        private static readonly string[] Files = [
            "Grandstander-Bold.ttf",
            "AtkinsonHyperlegibleNext-Regular.ttf",
            "AtkinsonHyperlegibleNext-Bold.ttf",
        ];

        // Kept for the life of the app: disposing a collection unregisters its fonts.
        private static readonly object Gate = new ();
        private static PrivateFontCollection? collection;

        /// <summary>
        /// Registers the bundled fonts. Call once at start-up, before the theme is loaded so CSS can name the families. Safe to call again.
        /// A font that is missing or unreadable is skipped: the family then falls back to the platform's sans-serif, never a crash.
        /// </summary>
        public static void Register ()
        {
            lock (Gate) {
                if (collection is not null)
                    return;

                var fonts = new PrivateFontCollection ();
                var assembly = typeof (AlertFonts).Assembly;
                foreach (var file in Files) {
                    try {
                        using var stream = assembly.GetManifestResourceStream ($"AlertBuddy.Shared.Fonts.{file}");
                        if (stream is null)
                            continue;

                        var bytes = new byte[stream.Length];
                        stream.ReadExactly (bytes);
                        fonts.AddMemoryFont (bytes);
                    } catch (Exception ex) when (ex is IOException or InvalidOperationException or ArgumentException) {
                        // Skipped on purpose; see above.
                    }
                }

                collection = fonts;
            }
        }

        /// <summary>Status sentences, buttons, headings. Bold is the only weight bundled.</summary>
        public static Font Display (float size)
        {
            Register ();
            return new Font (DisplayFamily, size, bold: true);
        }

        /// <summary>Everything else.</summary>
        public static Font Body (float size, bool bold = false)
        {
            Register ();
            return new Font (BodyFamily, size, bold: bold);
        }
    }
}
