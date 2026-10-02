using System.Collections.ObjectModel;
using AlertBuddy.ViewModels.Services;

namespace AlertBuddy.ViewModels.Screens
{
    /// <summary>
    /// The permission steps as rows a screen can show, kept up to date: First run and Settings both list them. Asking the platform again
    /// keeps a row's identity while its state changes, so a view can update in place.
    /// </summary>
    public sealed class PermissionRows
    {
        private readonly IPermissionGuide? guide;

        /// <summary>Creates the rows for <paramref name="guide"/>, or none where there is nothing to allow.</summary>
        public PermissionRows (IPermissionGuide? guide)
        {
            this.guide = guide;
            Refresh ();
        }

        /// <summary>The steps, in order. Empty on a platform with nothing to allow.</summary>
        public ObservableCollection<PermissionRowViewModel> Items { get; } = [];

        /// <summary>Asks the platform again which steps are done, and updates the rows.</summary>
        public void Refresh ()
        {
            if (guide is null)
                return;

            var items = guide.Items;
            for (var i = 0; i < items.Count; i++) {
                if (i < Items.Count && Items[i].Kind == items[i].Kind)
                    Items[i].Update (items[i]);
                else if (i < Items.Count)
                    Items[i] = new PermissionRowViewModel (items[i], guide);
                else
                    Items.Add (new PermissionRowViewModel (items[i], guide));
            }

            while (Items.Count > items.Count)
                Items.RemoveAt (Items.Count - 1);
        }
    }
}
