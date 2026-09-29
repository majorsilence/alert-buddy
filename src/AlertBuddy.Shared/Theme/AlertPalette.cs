using System.Drawing;
using AlertBuddy.ViewModels.Screens;

namespace AlertBuddy.Shared.Theme
{
    /// <summary>
    /// The six state colours plus one light (PLAN.md section 8.2). Colour carries meaning: these are used only for state, never for
    /// decoration. Custom controls read this directly; standard controls are coloured by the CSS theme instead (<see cref="AlertBuddyTheme"/>).
    /// </summary>
    public static class AlertPalette
    {
        /// <summary>Night ground. The house at night.</summary>
        public static readonly Color Blueberry = Color.FromArgb (0x23, 0x2E, 0x7A);

        /// <summary>Day ground, text on Blueberry. Lavender-tinted white, not cream.</summary>
        public static readonly Color Paper = Color.FromArgb (0xF6, 0xF2, 0xFF);

        /// <summary>Text and outlines on light surfaces. A tinted dark, not black.</summary>
        public static readonly Color GrapeInk = Color.FromArgb (0x2B, 0x1B, 0x4D);

        /// <summary>Calm, all clear.</summary>
        public static readonly Color Mint = Color.FromArgb (0x6F, 0xD3, 0xA0);

        /// <summary>Warning.</summary>
        public static readonly Color Tangerine = Color.FromArgb (0xFF, 0x8A, 0x3D);

        /// <summary>Alarm.</summary>
        public static readonly Color Cherry = Color.FromArgb (0xC8, 0x20, 0x2E);

        /// <summary>The lamp: beacon light, focus ring, text selection. Not a state.</summary>
        public static readonly Color Butter = Color.FromArgb (0xFF, 0xD8, 0x4D);

        /// <summary>Dim blue-grey, the beacon's Asleep lamp.</summary>
        public static readonly Color Asleep = Color.FromArgb (0x8A, 0x92, 0xB8);

        /// <summary>
        /// The colour a beacon mood is told by. State is never colour alone (PLAN.md section 8.11): every state also has a face, an icon
        /// shape and words, drawn by the control itself.
        /// </summary>
        public static Color For (BeaconMood mood) => mood switch {
            BeaconMood.Asleep => Asleep,
            BeaconMood.Watching => Butter,
            BeaconMood.Warning => Tangerine,
            BeaconMood.Alarm => Cherry,
            BeaconMood.Reassured => Tangerine,
            BeaconMood.AllClear => Mint,
            _ => Butter,
        };
    }
}
