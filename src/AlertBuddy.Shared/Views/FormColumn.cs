using System.Drawing;
using AlertBuddy.Shared.Theme;
using AlertBuddy.Shared.Controls;
using Majorsilence.Forms;

namespace AlertBuddy.Shared.Views
{
    /// <summary>
    /// A scrolling single column of labelled fields. Settings and First run are forms of labelled fields, so this is the one place that
    /// knows how they are spaced; the stacking, the readable maximum width, wrapping and scrolling are the framework's
    /// <see cref="StackPanel"/> (majorsilence/Majorsilence.Forms#396).
    /// </summary>
    internal sealed class FormColumn : StackPanel
    {
        private const int Gap = 10;
        private const int Inset = 8;
        private const float CharWidth = 9f;
        private const int LineHeight = 26;
        private const int MaxRowWidth = 560;
        private const int ButtonWidth = 320;

        /// <summary>The height a wrapped paragraph needs at <paramref name="width"/>, from a per-character estimate that errs on the roomy side. For views that place their own paragraphs by hand.</summary>
        internal static int ParagraphHeight (string text, int width)
        {
            var perLine = Math.Max (10, (int)(width / CharWidth));
            return Math.Max (1, (text.Length + perLine - 1) / perLine) * LineHeight;
        }

        // Messages that are empty until there is something to say (a problem, a test result): they take no room while empty.
        private readonly List<Label> messages = [];

        /// <param name="isSection">True for a part of another column: it does not scroll, and takes the height its rows need.</param>
        public FormColumn (bool isSection = false)
        {
            Spacing = Gap;

            if (isSection) {
                AutoSize = true;
                AutoSizeMode = AutoSizeMode.GrowAndShrink;
                return;
            }

            AutoScroll = true;
            MaximumContentWidth = MaxRowWidth;
            Padding = new Padding (Inset);
        }

        /// <summary>A part of this column that can be rebuilt on its own (the permission steps, which change when the person returns from system settings) without disturbing the fields around it.</summary>
        public FormColumn AddSection ()
        {
            var section = new FormColumn (isSection: true);
            Add (section);
            return section;
        }

        /// <summary>A heading for a group of fields.</summary>
        public Label AddHeading (string text)
        {
            var label = new Label { AutoSize = true, Text = text, ForeColor = AlertPalette.OnGround, Font = AlertFonts.Display (20) };
            Add (label, extraTop: 14);
            return label;
        }

        /// <summary>A short label above a field.</summary>
        public Label AddLabel (string text)
        {
            var label = new Label { AutoSize = true, Text = text, ForeColor = AlertPalette.OnGround };
            Add (label);
            return label;
        }

        /// <summary>A wrapping paragraph. Empty, it takes no room.</summary>
        public Label AddParagraph (string text, Color? colour = null)
        {
            var label = new Label { AutoSize = true, Text = text, ForeColor = colour ?? AlertPalette.OnGround };
            messages.Add (label);
            Add (label);
            return label;
        }

        /// <summary>Adds a control at the column's full width (buttons keep their own, up to a comfortable size).</summary>
        public T Add<T> (T control, int extraTop = 0) where T : Control
        {
            control.Margin = new Padding (0, extraTop, 0, 0);

            if (control is ChunkyButton or HoldButton) {
                control.Width = ButtonWidth;
                SetAlignment (control, StackAlignment.Start);
            }

            Controls.Add (control);
            Relayout ();
            return control;
        }

        /// <summary>How many rows there are, so a screen can later <see cref="TrimTo"/> back to this point.</summary>
        public int RowCount => Controls.Count;

        /// <summary>Removes and disposes every row after the first <paramref name="count"/>, for a part of the form that is rebuilt.</summary>
        public void TrimTo (int count)
        {
            while (Controls.Count > count) {
                var row = Controls[^1];
                Controls.Remove (row);
                if (row is Label label)
                    messages.Remove (label);
                row.Dispose ();
            }
        }

        /// <summary>Lays the rows out again, after a row's text or visibility changed.</summary>
        public void Relayout ()
        {
            foreach (var message in messages)
                message.Visible = message.Text.Length > 0;

            PerformLayout ();
        }
    }
}
