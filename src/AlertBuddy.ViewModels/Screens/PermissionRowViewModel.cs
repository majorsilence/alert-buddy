using AlertBuddy.Core.Localization;
using AlertBuddy.ViewModels.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AlertBuddy.ViewModels.Screens
{
    /// <summary>One step of the permissions wizard: what it is, why, whether it worked, and the button that does it.</summary>
    public sealed partial class PermissionRowViewModel : ObservableObject
    {
        private readonly IPermissionGuide guide;

        [ObservableProperty]
        private string title = "";

        [ObservableProperty]
        private string why = "";

        [ObservableProperty]
        private bool? granted;

        /// <summary>Creates the row for an item.</summary>
        public PermissionRowViewModel (PermissionItem item, IPermissionGuide guide)
        {
            this.guide = guide ?? throw new ArgumentNullException (nameof (guide));
            Kind = item.Kind;
            Update (item);
        }

        /// <summary>Which step this is.</summary>
        public PermissionKind Kind { get; }

        /// <summary>
        /// "Done" or "Not yet", in words and not only colour (PLAN.md section 8.11); empty for a check that is not a permission, so
        /// the screen never claims something it cannot know.
        /// </summary>
        public string StatusText => Granted switch { true => Loc.T ("Done"), false => Loc.T ("Not yet"), null => "" };

        /// <summary>The button's words: "Allow" until it is done, then "Change" so a grown-up can still get to the setting.</summary>
        public string ButtonText => Kind == PermissionKind.AlarmVolume ? Loc.T ("Play a test sound") : Granted == true ? Loc.T ("Change") : Loc.T ("Allow");

        /// <summary>Shows a newer state of the same step.</summary>
        public void Update (PermissionItem item)
        {
            Title = item.Title;
            Why = item.Why;
            Granted = item.Granted;
            OnPropertyChanged (nameof (StatusText));
            OnPropertyChanged (nameof (ButtonText));
        }

        [RelayCommand]
        private void Open () => guide.Open (Kind);
    }
}
