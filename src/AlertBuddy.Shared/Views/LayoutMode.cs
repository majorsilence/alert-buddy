namespace AlertBuddy.Shared.Views
{
    /// <summary>The three widths a screen is laid out for (PLAN.md section 7.3).</summary>
    public enum LayoutMode
    {
        /// <summary>Under 600 logical pixels: one column.</summary>
        Compact,

        /// <summary>600 to 900: one column, kept to a readable width and centred.</summary>
        Medium,

        /// <summary>Over 900, or landscape: two panes, the buddy on the left and the list on the right.</summary>
        Expanded,
    }

    /// <summary>Chooses a <see cref="LayoutMode"/> from a window's size in logical pixels.</summary>
    public static class LayoutModes
    {
        /// <summary>The widest a single column gets before it is centred instead of stretched.</summary>
        public const int ColumnMax = 640;

        /// <summary>The mode for a window of <paramref name="width"/> by <paramref name="height"/>.</summary>
        /// <remarks>Landscape counts as expanded only once there is room for two panes: a 500-wide landscape window is still compact.</remarks>
        public static LayoutMode For (int width, int height)
            => width > 900 || (width >= 600 && width > height) ? LayoutMode.Expanded
             : width >= 600 ? LayoutMode.Medium
             : LayoutMode.Compact;
    }
}
