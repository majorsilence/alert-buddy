using Majorsilence.Forms;

namespace AlertBuddy.Shared.Views
{
    /// <summary>
    /// Gives a control a stable <c>Name</c> (what an automation client finds it by) and the words an accessibility bridge says for it. The
    /// views are custom-painted, so the text on screen is not what a screen reader sees: a text box's label is a separate control, and a
    /// glyph button reads as its glyph, so both need saying outright.
    /// </summary>
    internal static class AccessibleNames
    {
        /// <summary>Names <paramref name="control"/>; <paramref name="label"/> defaults to its text, <paramref name="description"/> is optional extra.</summary>
        public static T Named<T> (this T control, string name, string? label = null, string? description = null) where T : Control
        {
            control.Name = name;

            if (label is not null)
                control.AccessibleName = label;

            if (description is not null)
                control.AccessibleDescription = description;

            control.AccessibleRole = control switch {
                Controls.ChunkyButton or Controls.HoldButton or Button => AccessibleRole.PushButton,
                TextBox => AccessibleRole.Text,
                CheckBox => AccessibleRole.CheckButton,
                ComboBox => AccessibleRole.ComboBox,
                NumericUpDown => AccessibleRole.SpinButton,
                _ => control.AccessibleRole,
            };

            return control;
        }

        /// <summary>The extra line for a button that has to be held, which a screen reader would otherwise present as an ordinary press.</summary>
        public const string HoldHint = "Press and hold for two seconds.";
    }
}
