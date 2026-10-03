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

        private const int MaxRowWidth = 560;

        /// <summary>The height a wrapped paragraph needs at <paramref name="width"/>, from a per-character estimate that errs on the roomy side.</summary>
        internal static int ParagraphHeight (string text, int width)
        {
            var perLine = Math.Max (10, (int)(width / CharWidth));
            return Math.Max (1, (text.Length + perLine - 1) / perLine) * LineHeight;
        }

        private readonly List<Control> rows = [];
        private bool laying;
        private bool layAgain;

        private readonly bool isSection;

        /// <param name="isSection">True for a part of another column: it does not scroll, and takes the height its rows need.</param>
        public FormColumn (bool isSection = false)
        {
            this.isSection = isSection;
            AutoScroll = !isSection;
            Resize += (_, _) => Relayout ();
        }

        /// <summary>
        /// A part of this column that can be rebuilt on its own (the permission steps, which change when the person returns from system
        /// settings) without disturbing the fields around it.
        /// </summary>
        public FormColumn AddSection ()
        {
            var section = new FormColumn (isSection: true);
            section.HeightChanged += Relayout;
            Add (section);
            return section;
        }

        private event Action? HeightChanged;

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
            // Wraps like a paragraph: on a 360-wide phone "New PIN (4 digits, leave blank to keep it)" ran off the edge as one line.
            var label = new Label { AutoSize = false, Text = text, ForeColor = AlertPalette.OnGround };
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
            // A section's height changing relays out its parent, which sets the section's width, which relays the section out again.
            if (laying) {
                layAgain = true;
                return;
            }

            laying = true;
            try {
                do {
                    layAgain = false;
                    LayRowsOut ();
                } while (layAgain);
            } finally {
                laying = false;
            }
        }

        private void LayRowsOut ()
        {
            var available = Math.Max (160, Width - Inset * 2 - 16);

            // A form is read down a column, not across a tablet: rows stop at a readable width and the column sits in the middle.
            var width = Math.Min (available, MaxRowWidth);
            var left = Inset + (available - width) / 2;
            var top = Inset;
            foreach (var row in rows) {
                // An empty message (no problem, no test result yet) takes no room.
                if (row is Label { AutoSize: false } message)
                    message.Visible = message.Text.Length > 0;

                if (!row.Visible)
                    continue;

                top += row.Tag is int extra ? extra : 0;
                row.Left = left;
                row.Top = top;

                if (row is FormColumn) {
                    // A section's own inset lines its rows up with this column's, so it spans the column and starts at its edge.
                    row.Left = 0;
                    row.Width = Math.Max (160, Width - 16);
                } else if (row is Label { AutoSize: false } paragraph) {
                    paragraph.Width = width;
                    paragraph.Height = ParagraphHeight (paragraph.Text, width);
                } else if (row is Label) {
                    // keeps its own size
                } else if (row is ChunkyButton or HoldButton) {
                    row.Width = Math.Min (width, 320);
                } else {
                    row.Width = width;
                }

                top += row.Height + Gap;
            }

            if (isSection && Height != top) {
                Height = top;
                HeightChanged?.Invoke ();
            }
        }
    }
}
