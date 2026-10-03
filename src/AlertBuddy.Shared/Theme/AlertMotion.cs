using AlertBuddy.Core.Settings;
using Majorsilence.Forms;

namespace AlertBuddy.Shared.Theme
{
    /// <summary>
    /// Whether the app should hold still: the grown-up's setting when they made one, otherwise the system's own reduced-motion
    /// preference (PLAN.md section 8.9). Global for the same reason the theme is: every moving control asks the same question, and
    /// none of them should know where the answer comes from.
    /// </summary>
    public static class AlertMotion
    {
        /// <summary>True when nothing should rotate, sweep or shake.</summary>
        public static bool Reduce { get; private set; }

        private static SettingsService? followed;
        private static bool hooked;

        /// <summary>Resolves a setting against the system preference: an explicit choice wins, "follow the device" (null) asks the system.</summary>
        public static bool Resolve (bool? setting, bool systemPrefersReduced) => setting ?? systemPrefersReduced;

        /// <summary>Follows <paramref name="settings"/> and the system preference from now on, replacing any earlier subscription.</summary>
        public static void Follow (SettingsService settings)
        {
            if (followed is not null)
                followed.Changed -= Refresh;

            followed = settings;
            settings.Changed += Refresh;

            if (!hooked) {
                SystemInformation.PrefersReducedMotionChanged += (_, _) => Refresh ();
                hooked = true;
            }

            Refresh ();
        }

        private static void Refresh ()
            => Reduce = Resolve (followed?.Current.ReduceMotion, SystemInformation.PrefersReducedMotion);
    }
}
