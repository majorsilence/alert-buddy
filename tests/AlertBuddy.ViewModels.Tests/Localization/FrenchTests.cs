using AlertBuddy.Core.Localization;
using AlertBuddy.Core.Settings;
using AlertBuddy.ViewModels.Copy;
using AlertBuddy.ViewModels.Screens;
using Xunit;

namespace AlertBuddy.ViewModels.Tests.Localization
{
    // The language is process-wide, so these run alone (see the same collection in the Core tests).
    [CollectionDefinition ("Language", DisableParallelization = true)]
    public class LanguageCollection;

    [Collection ("Language")]
    public sealed class FrenchTests : IDisposable
    {
        private const string Nbsp = " ";

        public FrenchTests () => Loc.Use (AppLanguage.English);

        public void Dispose () => Loc.Use (AppLanguage.English);

        [Fact]
        public void TheBuddySpeaksFrench_AndANamedPlaceNeedsNoArticle ()
        {
            Loc.Use (AppLanguage.French);

            Assert.Equal ("Tout est calme. Pip veille.", Words.AllQuiet ("Pip"));
            Assert.Equal ($"Atelier{Nbsp}: il fait trop chaud.", Words.TooHot ("Atelier"));
            Assert.Equal ("Préviens un adulte tout de suite.", Words.TellAGrownUpNow);
            Assert.Equal ("3 endroits commencent à chauffer.", Words.SeveralWarm (3));
        }

        [Fact]
        public void English_StillLowerCasesThePlace_AfterThe ()
        {
            Assert.Equal ("The workshop is too hot.", Words.TooHot ("Workshop"));
        }

        [Fact]
        public void TheAnnouncement_IsSaidInFrench ()
        {
            Loc.Use (AppLanguage.French);

            Assert.Equal ($"Alerte. Atelier{Nbsp}: il fait trop chaud. Préviens un adulte tout de suite.", Words.AlarmAnnouncement ("Atelier", hot: true));
            Assert.Equal ($"Alerte. Atelier{Nbsp}: il faut aller voir. Préviens un adulte tout de suite.", Words.AlarmAnnouncement ("Atelier", hot: false));
        }

        [Fact]
        public void TimesAndDegrees_AreInFrench ()
        {
            Loc.Use (AppLanguage.French);

            Assert.Equal ("quelques secondes", Words.TimeAgo (TimeSpan.FromSeconds (10)));
            Assert.Equal ("5 min", Words.TimeAgo (TimeSpan.FromMinutes (5)));
            Assert.Equal ("3 jours", Words.TimeAgo (TimeSpan.FromDays (3)));
            Assert.Equal ("41 degrés", Words.Degrees (41.2));
            Assert.Equal ("À l’écoute. Dernier signal il y a 5 min.", Words.Listening (TimeSpan.FromMinutes (5)));
        }

        [Fact]
        public async Task SavingTheLanguageInSettings_ChangesTheAppAtOnce_AndIsRemembered ()
        {
            await using var rig = new AppRig (new AppSettings { ServerUrl = "https://ntfy.example.com", Topic = "home-alerts", FirstRunComplete = true });
            rig.GoLive ();
            Assert.Equal ("All quiet. Pip is keeping watch.", rig.Main.StatusText);
            rig.Main.OpenSettingsCommand.Execute (null);
            rig.Current<GateViewModel> ().HoldCompletedCommand.Execute (null);       // no PIN is set, so the hold alone opens it
            var vm = rig.Current<SettingsViewModel> ();
            Assert.Equal (AppLanguage.System, vm.Language);

            vm.Language = AppLanguage.French;
            vm.SaveCommand.Execute (null);

            Assert.Equal (AppLanguage.French, rig.App.Settings.Current.Language);
            Assert.True (Loc.IsFrench);
            Assert.Equal ("Tout est calme. Pip veille.", rig.Main.StatusText);       // Home says it in French without a message having to arrive
        }

        [Fact]
        public void TheDevicesLanguage_IsFollowedUntilOneIsChosen ()
        {
            Loc.Use (AppLanguage.System, "fr-CA");
            Assert.True (Loc.IsFrench);

            Loc.Use (AppLanguage.English, "fr-CA");
            Assert.False (Loc.IsFrench);
        }
    }
}
