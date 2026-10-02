using AlertBuddy.Shared.Controls;
using AlertBuddy.ViewModels.Screens;
using Majorsilence.Forms.Mvvm;

namespace AlertBuddy.Shared.Views
{
    /// <summary>Draws the permission steps into a form section. First run and Settings show the same rows, so the drawing is in one place.</summary>
    internal static class PermissionStepsView
    {
        /// <summary>
        /// Replaces what is in <paramref name="section"/> with one block per step: its name and state in words, why it matters, and the
        /// button. The buttons are bound in <paramref name="scope"/>, which the caller disposes before drawing again.
        /// </summary>
        public static void Fill (FormColumn section, IEnumerable<PermissionRowViewModel> rows, BindingScope scope)
        {
            section.TrimTo (0);
            foreach (var row in rows) {
                section.AddLabel (row.StatusText.Length > 0 ? $"{row.Title}: {row.StatusText}" : row.Title);
                section.AddParagraph (row.Why);
                var button = section.Add (new ChunkyButton { Text = row.ButtonText, Height = 56 });
                scope.Add (button.BindCommand (row.OpenCommand));
            }

            section.Relayout ();
        }
    }
}
