using AlertBuddy.Core.Settings;
using AlertBuddy.ViewModels.Screens;
using AlertBuddy.ViewModels.Services;
using Xunit;

namespace AlertBuddy.ViewModels.Tests.Screens
{
    public class PermissionsTests
    {
        [Fact]
        public async Task FirstRun_ListsTheSteps_WithWordsForTheirState ()
        {
            await using var rig = new AppRig (new AppSettings ());
            var first = rig.Current<FirstRunViewModel> ();

            Assert.Equal (["Notifications", "Alarm volume"], first.Permissions.Select (p => p.Title));
            Assert.Equal (["Not yet", ""], first.Permissions.Select (p => p.StatusText));
            Assert.Equal (["Allow", "Play a test sound"], first.Permissions.Select (p => p.ButtonText));
        }

        [Fact]
        public async Task PressingAStep_AsksThePlatformForThatOne ()
        {
            await using var rig = new AppRig (new AppSettings ());
            var first = rig.Current<FirstRunViewModel> ();

            first.Permissions[0].OpenCommand.Execute (null);

            Assert.Equal ([PermissionKind.Notifications], rig.Permissions.Opened);
        }

        [Fact]
        public async Task WhenThePersonComesBackFromSystemSettings_TheStepsShowWhatTheyDid ()
        {
            await using var rig = new AppRig (new AppSettings ());
            var first = rig.Current<FirstRunViewModel> ();
            Assert.Equal ("Not yet", first.Permissions[0].StatusText);

            rig.Permissions.Current[0] = rig.Permissions.Current[0] with { Granted = true };
            rig.Lifecycle.Resume ();

            Assert.Equal ("Done", first.Permissions[0].StatusText);
            Assert.Equal ("Change", first.Permissions[0].ButtonText);
        }

        [Fact]
        public async Task ArrivingAtTheStep_AsksAgain ()
        {
            await using var rig = new AppRig (new AppSettings ());
            var first = rig.Current<FirstRunViewModel> ();
            Assert.Equal ("Not yet", first.Permissions[0].StatusText);

            // Allowed on some other screen, with no resume in between.
            rig.Permissions.Current[0] = rig.Permissions.Current[0] with { Granted = true };
            first.Step = FirstRunStep.Permissions;

            Assert.Equal ("Done", first.Permissions[0].StatusText);
        }

        [Fact]
        public async Task OnceTheScreenIsLeft_ComingBackToTheAppDoesNotTouchIt ()
        {
            await using var rig = new AppRig (new AppSettings ());
            var first = rig.Current<FirstRunViewModel> ();
            var row = first.Permissions[0];
            first.Dispose ();

            rig.Permissions.Current[0] = rig.Permissions.Current[0] with { Granted = true };
            rig.Lifecycle.Resume ();

            Assert.Equal ("Not yet", row.StatusText);
        }

        [Fact]
        public async Task WithNothingToAllow_TheListIsEmpty ()
        {
            await using var rig = new AppRig (new AppSettings (), beforeCreate: _ => { });
            rig.Permissions.Current.Clear ();
            var first = rig.Current<FirstRunViewModel> ();
            first.RefreshPermissions ();

            Assert.Empty (first.Permissions);
        }

        [Fact]
        public async Task Settings_OffersTheSameSteps_AndFollowsTheSystem ()
        {
            await using var rig = new AppRig ();
            rig.Navigator.GoTo<SettingsViewModel> ();
            var settings = rig.Current<SettingsViewModel> ();
            Assert.Equal (["Notifications", "Alarm volume"], settings.Permissions.Select (p => p.Title));
            Assert.Equal ("Not yet", settings.Permissions[0].StatusText);

            rig.Permissions.Current[0] = rig.Permissions.Current[0] with { Granted = true };
            var before = settings.PermissionsRefreshed;
            rig.Lifecycle.Resume ();

            Assert.Equal ("Done", settings.Permissions[0].StatusText);
            Assert.Equal (before + 1, settings.PermissionsRefreshed);
        }

        [Fact]
        public async Task AskingAgainAboutPermissions_DoesNotWipeTheSavedMessage ()
        {
            await using var rig = new AppRig ();
            rig.Navigator.GoTo<SettingsViewModel> ();
            var settings = rig.Current<SettingsViewModel> ();
            await settings.SaveCommand.ExecuteAsync (null);
            Assert.Equal ("Saved.", settings.SavedMessage);

            rig.Lifecycle.Resume ();

            Assert.Equal ("Saved.", settings.SavedMessage);
        }
    }
}
