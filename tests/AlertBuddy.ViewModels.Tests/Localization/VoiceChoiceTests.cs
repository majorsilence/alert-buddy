using AlertBuddy.Core.Localization;
using AlertBuddy.Core.Settings;
using AlertBuddy.ViewModels.Screens;
using AlertBuddy.ViewModels.Services;
using Xunit;

namespace AlertBuddy.ViewModels.Tests.Localization
{
    [Collection ("Language")]
    public sealed class VoiceChoiceTests : IDisposable
    {
        public VoiceChoiceTests () => Loc.Use (AppLanguage.English);

        public void Dispose () => Loc.Use (AppLanguage.English);

        private static AppSettings Configured () => new () { ServerUrl = "https://ntfy.example.com", Topic = "home-alerts", FirstRunComplete = true };

        private static RecordingSpeaker WithVoices ()
        {
            var speaker = new RecordingSpeaker ();
            speaker.Installed.AddRange ([
                new VoiceOption ("en-gb-x-rp", "English RP", "en-GB", VoiceSex.Female),
                new VoiceOption ("en-us-x-iom", "English US", "en-US", VoiceSex.Male),
                new VoiceOption ("en-us-x-sfg", "English US 2", "en-US", VoiceSex.Unknown),
                new VoiceOption ("fr-fr-x-vlf", "Français", "fr-FR", VoiceSex.Male),
                new VoiceOption ("en-us-x-net", "English US online", "en-US", VoiceSex.Male, RequiresNetwork: true),
            ]);
            return speaker;
        }

        [Fact]
        public void TheSettingsOffer_OnlyTheOfflineVoicesOfTheLanguageInUse_MenFirst ()
        {
            var rig = new FormRig (Configured ());
            var vm = rig.SettingsScreen (speaker: WithVoices ());

            Assert.Equal (["en-us-x-iom", "en-us-x-sfg", "en-gb-x-rp"], vm.AvailableVoices.Select (v => v.Id));
        }

        [Fact]
        public void InFrench_TheFrenchVoicesAreOffered ()
        {
            var rig = new FormRig (Configured () with { Language = AppLanguage.French });     // a new app applies its saved language
            var vm = rig.SettingsScreen (speaker: WithVoices ());

            Assert.Equal (["fr-fr-x-vlf"], vm.AvailableVoices.Select (v => v.Id));
        }

        [Fact]
        public void APickedVoice_IsSaved_AndHeardInThePreview ()
        {
            var rig = new FormRig (Configured ());
            var speaker = WithVoices ();
            var vm = rig.SettingsScreen (speaker: speaker);
            Assert.Null (vm.VoiceId);                                     // automatic until one is picked

            vm.VoiceId = "en-us-x-iom";
            vm.PreviewVoiceCommand.Execute (null);
            vm.SaveCommand.Execute (null);

            Assert.Equal ("en-us-x-iom", rig.Settings.Current.VoiceId);
            Assert.Equal ("en-us-x-iom", Assert.Single (speaker.VoiceIds));
        }

        [Fact]
        public void ThePitchPresets_StillApplyWhenNoVoiceIsPicked_AndAreDroppedWhenOneIs ()
        {
            var rig = new FormRig (Configured ());
            var speaker = WithVoices ();
            var vm = rig.SettingsScreen (speaker: speaker);

            vm.PreviewVoiceCommand.Execute (null);
            Assert.Equal ((VoiceType.Deep, null), (speaker.Voices[0].Voice, speaker.VoiceIds[0]));

            vm.VoiceId = "en-us-x-iom";
            vm.PreviewVoiceCommand.Execute (null);
            Assert.Equal (VoiceType.Standard, speaker.Voices[1].Voice);   // a real voice is not pitched down on top
        }
    }
}
