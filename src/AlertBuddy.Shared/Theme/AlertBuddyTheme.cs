using AlertBuddy.Core.Settings;
using Majorsilence.Forms;

namespace AlertBuddy.Shared.Theme
{
    /// <summary>
    /// Registers and applies the two CSS themes for standard controls (PLAN.md section 8.7). Custom controls are not themed by CSS; they
    /// read <see cref="AlertPalette"/> directly and repaint on <see cref="Majorsilence.Forms.Theme.ThemeChanged"/>.
    /// </summary>
    public static class AlertBuddyTheme
    {
        private const string Day = "AlertBuddyDay";
        private const string Night = "AlertBuddyNight";

        // Draft (PLAN.md section 8.7): uses only supported tokens, selectors and properties.
        private const string DayCss = """
            @theme "AlertBuddyDay" extends Light;
            :root {
              --accent-color: #232E7A;
              --accent-color-2: #232E7A;
              --background-color: #F6F2FF;
              --foreground-color: #2B1B4D;
              --foreground-color-on-accent: #F6F2FF;
              --border-low-color: #2B1B4D;
              --text-selection-background-color: #FFD84D;
              --warning-highlight-color: #C8202E;
              --font-size: 18px;
              --ui-font: "Atkinson Hyperlegible Next", "Atkinson Hyperlegible", "Noto Sans", sans-serif;
            }
            Form    { background-color: #F6F2FF; color: #2B1B4D; font-size: 18px; }
            TextBox { background-color: #FFFFFF; color: #2B1B4D; border: 3px solid #2B1B4D; border-radius: 14px; }
            ScrollBar::thumb { background-color: #2B1B4D; border-radius: 6px; }
            """;

        // Blueberry ground with Paper text and outlines, the Night sibling of the theme above.
        private const string NightCss = """
            @theme "AlertBuddyNight" extends Dark;
            :root {
              --accent-color: #FFD84D;
              --accent-color-2: #FFD84D;
              --background-color: #232E7A;
              --foreground-color: #F6F2FF;
              --foreground-color-on-accent: #2B1B4D;
              --border-low-color: #F6F2FF;
              --text-selection-background-color: #FFD84D;
              --warning-highlight-color: #FF8A3D;
              --font-size: 18px;
              --ui-font: "Atkinson Hyperlegible Next", "Atkinson Hyperlegible", "Noto Sans", sans-serif;
            }
            Form    { background-color: #232E7A; color: #F6F2FF; font-size: 18px; }
            TextBox { background-color: #2B1B4D; color: #F6F2FF; border: 3px solid #F6F2FF; border-radius: 14px; }
            ScrollBar::thumb { background-color: #F6F2FF; border-radius: 6px; }
            """;

        private static bool registered;
        private static bool bedside;
        private static LookPreference chosen;

        /// <summary>Registers both themes, once, and applies the one <paramref name="look"/> resolves to for a system in Day right now.</summary>
        /// <remarks>
        /// Auto should follow the system's own light/dark setting; nothing in this repo reads that yet on desktop, so Auto currently
        /// resolves to Day until that plumbing exists.
        /// </remarks>
        public static void Apply (LookPreference look)
        {
            chosen = look;
            Show ();
        }

        /// <summary>Bedside mode forces Night while it is on, and puts back the grown-up's choice when it is off (PLAN.md section 8.7).</summary>
        public static void SetBedside (bool on)
        {
            bedside = on;
            Show ();
        }

        private static void Show ()
        {
            if (!registered) {
                Majorsilence.Forms.Theme.RegisterThemeCss (DayCss);
                Majorsilence.Forms.Theme.RegisterThemeCss (NightCss);
                registered = true;
            }

            var night = bedside || chosen == LookPreference.Night;
            AlertPalette.Night = night;
            Majorsilence.Forms.Theme.ApplyTheme (night ? Night : Day);
        }
    }
}
