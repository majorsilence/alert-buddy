using Majorsilence.Forms.Drawing;

namespace AlertBuddy.Shared.Theme
{
    /// <summary>
    /// The two type families of PLAN.md section 8.2. Neither font file is bundled yet (the brief's own "verify licences when bundling"
    /// is still open), so these ask for the family by name and fall back to the platform's default sans-serif where it is missing --
    /// honest degradation, not a crash.
    /// </summary>
    public static class AlertFonts
    {
        private const string DisplayFamily = "Grandstander";
        private const string BodyFamily = "Atkinson Hyperlegible Next";

        /// <summary>Status sentences, buttons, headings. Weights 600 to 800; this asks for Bold, the closest GDI+ style has.</summary>
        public static Font Display (float size) => new (DisplayFamily, size, bold: true);

        /// <summary>Everything else.</summary>
        public static Font Body (float size, bool bold = false) => new (BodyFamily, size, bold: bold);
    }
}
