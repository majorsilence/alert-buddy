using System.Drawing;
using AlertBuddy.Shared.Theme;
using AlertBuddy.Shared.Controls;
using Majorsilence.Forms;

namespace AlertBuddy.Shared.Views
{
    /// <summary>
    /// A scrolling single column that stacks what is added to it, top to bottom, at the column's width. Settings and First run are forms
    /// of labelled fields, so this is the one place that knows how they are spaced.
    /// </summary>
    internal sealed class FormColumn : Panel
    {
        private const int Gap = 10;
        private const int Inset = 8;
        private const float CharWidth = 9f;
        private const int LineHeight = 26;

        private readonly List<Control> rows = [];

        public FormColumn ()
        {
            AutoScroll = true;
            Resize += (_, _) => Relayout ();
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

        /// <summary>A wrapping paragraph. Its height follows its text and the column's width.</summary>
        public Label AddParagraph (string text, Color? colour = null)
        {
            var label = new Label { AutoSize = false, Text = text, ForeColor = colour ?? AlertPalette.OnGround };
            Add (label);
            return label;
        }

        /// <summary>Adds a control at the column's full width.</summary>
        public T Add<T> (T control, int extraTop = 0) where T : Control
        {
            control.Tag = extraTop;
            rows.Add (control);
            Controls.Add (control);
            Relayout ();
            return control;
        }

        /// <summary>How many rows there are, so a screen can later <see cref="TrimTo"/> back to this point.</summary>
        public int RowCount => rows.Count;

        /// <summary>Removes and disposes every row after the first <paramref name="count"/>, for a part of the form that is rebuilt.</summary>
        public void TrimTo (int count)
        {
            while (rows.Count > count) {
                var row = rows[^1];
                rows.RemoveAt (rows.Count - 1);
                Controls.Remove (row);
                row.Dispose ();
            }
        }

        /// <summary>Stacks the rows again, after a row's text or visibility changed.</summary>
        public void Relayout ()
        {
            var width = Math.Max (160, Width - Inset * 2 - 16);
            var top = Inset;
            foreach (var row in rows) {
                // An empty message (no problem, no test result yet) takes no room.
                if (row is Label { AutoSize: false } message)
                    message.Visible = message.Text.Length > 0;

                if (!row.Visible)
                    continue;

                top += row.Tag is int extra ? extra : 0;
                row.Left = Inset;
                row.Top = top;

                if (row is Label { AutoSize: false } paragraph) {
                    paragraph.Width = width;
                    var perLine = Math.Max (10, (int)(width / CharWidth));
                    paragraph.Height = Math.Max (1, (paragraph.Text.Length + perLine - 1) / perLine) * LineHeight;
                } else if (row is Label) {
                    // keeps its own size
                } else if (row is ChunkyButton or HoldButton) {
                    row.Width = Math.Min (width, 320);
                } else {
                    row.Width = width;
                }

                top += row.Height + Gap;
            }
        }
    }
}
