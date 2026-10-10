using AlertBuddy.Core.Localization;
using AlertBuddy.Shared.Views;
using AlertBuddy.ViewModels.Screens;
using Majorsilence.Forms;
using Majorsilence.Forms.Headless;
using Xunit;

namespace AlertBuddy.Shared.Tests
{
    // The language is process-wide, so these run alone.
    [CollectionDefinition ("Language", DisableParallelization = true)]
    public class LanguageCollection;

    [Collection ("Language")]
    public sealed class FrenchViewTests : IDisposable
    {
        public FrenchViewTests () => Loc.Use (AppLanguage.English);

        public void Dispose () => Loc.Use (AppLanguage.English);

        private static T Find<T> (MainForm form, string name) where T : Control
            => Assert.IsAssignableFrom<T> (AccessibilityTests.Descendants (form.Controls.Cast<Control> ()).First (c => c.Name == name));

        [Fact]
        public async Task Home_IsInFrench ()
        {
            var app = SmokeTests.CreateApp ();       // a new app applies its saved language, so French is chosen after it
            Loc.Use (AppLanguage.French);
            try {
                var form = new MainForm (app);
                HeadlessRenderer.CapturePng (form, 420, 720);

                Assert.Equal ("Essai", Find<Control> (form, "home.practice").Text);
                Assert.Equal ("Carnet d’alertes", Find<Control> (form, "home.alertBook").Text);
                Assert.Equal ("Sombre", Find<Control> (form, "home.bedside").Text);
                Assert.Equal ("Réglages, pour les adultes", Find<Control> (form, "home.settings").AccessibleName);
            } finally {
                await app.DisposeAsync ();
            }
        }

        [Fact]
        public async Task Practice_IsInFrench_IncludingTheSounds ()
        {
            var app = SmokeTests.CreateApp ();       // a new app applies its saved language, so French is chosen after it
            Loc.Use (AppLanguage.French);
            try {
                var form = new MainForm (app);
                app.Main.StartPracticeCommand.Execute (null);
                HeadlessRenderer.CapturePng (form, 420, 720);

                Assert.Equal ("Commencer l’essai", Find<Control> (form, "practice.start").Text);
                Assert.Equal ("Doux", Find<Control> (form, "practice.sound.Gentle").Text);
                Assert.Equal ("Écouter le whoop", Find<Control> (form, "practice.sound.Whoop").AccessibleName);
                Assert.Equal ("Essai. Rien n’est chaud.", Find<Control> (form, "practice.sound.Whoop").Parent!.Controls.Cast<Control> ().OfType<Label> ().First (l => l.Text.StartsWith ("Essai")).Text);
            } finally {
                await app.DisposeAsync ();
            }
        }

        [Fact]
        public async Task TheLanguageChoice_IsInSettings_NamedInItsOwnLanguage ()
        {
            var app = SmokeTests.CreateApp ();
            try {
                var form = new MainForm (app);
                app.Main.OpenSettingsCommand.Execute (null);
                Assert.IsType<GateViewModel> (app.Navigator.Current).HoldCompletedCommand.Execute (null);
                Assert.IsType<SettingsViewModel> (app.Navigator.Current);
                HeadlessRenderer.CapturePng (form, 420, 1400);

                var language = Find<ComboBox> (form, "settings.Language");
                Assert.Equal (["Follow the device", "English", "Français"], language.Items.Cast<object> ().Select (i => i.ToString ()));

                // Choosing French and saving rebuilds the screen on show, in French.
                var vm = (SettingsViewModel) app.Navigator.Current;
                vm.Language = AppLanguage.French;
                vm.SaveCommand.Execute (null);
                HeadlessRenderer.CapturePng (form, 420, 1400);

                var french = Find<ComboBox> (form, "settings.Language");
                Assert.Equal (["Suivre l’appareil", "English", "Français"], french.Items.Cast<object> ().Select (i => i.ToString ()));
            } finally {
                await app.DisposeAsync ();
            }
        }

        private sealed class FakeSpeaker : AlertBuddy.ViewModels.Services.ISpeaker
        {
            public bool IsSupported => true;
            public string? LastVoiceId { get; private set; }
            public Task<IReadOnlyList<AlertBuddy.ViewModels.Services.VoiceOption>> ListVoicesAsync () => Task.FromResult<IReadOnlyList<AlertBuddy.ViewModels.Services.VoiceOption>> ([
                new ("en-gb-x-rp", "English RP", "en-GB", AlertBuddy.ViewModels.Services.VoiceSex.Female),
                new ("en-us-x-iom", "English US", "en-US", AlertBuddy.ViewModels.Services.VoiceSex.Male),
            ]);
            public void Speak (string text, AlertBuddy.Core.Settings.VoiceType voice = AlertBuddy.Core.Settings.VoiceType.Standard, double volume = 1, string? voiceId = null) => LastVoiceId = voiceId;
        }

        [Fact]
        public async Task Settings_ListsTheInstalledVoices_AndAPickedOneIsKept ()
        {
            var speaker = new FakeSpeaker ();
            var app = SmokeTests.CreateApp (speaker: speaker);
            try {
                var form = new MainForm (app);
                app.Main.OpenSettingsCommand.Execute (null);
                Assert.IsType<GateViewModel> (app.Navigator.Current).HoldCompletedCommand.Execute (null);
                HeadlessRenderer.CapturePng (form, 420, 1800);

                var box = Find<ComboBox> (form, "settings.VoiceId");
                Assert.Equal (["Automatic", "English US (man)", "English RP (woman)"], box.Items.Cast<object> ().Select (i => i.ToString ()));
                Assert.True (Find<ComboBox> (form, "settings.Voice").Visible);      // the pitch presets, while the voice is automatic

                box.SelectedIndex = 1;
                var vm = (SettingsViewModel) app.Navigator.Current;
                Assert.Equal ("en-us-x-iom", vm.VoiceId);
                HeadlessRenderer.CapturePng (form, 420, 1800);
                Assert.False (Find<ComboBox> (form, "settings.Voice").Visible);     // a picked voice is not pitched on top
            } finally {
                await app.DisposeAsync ();
            }
        }

        private sealed class FakeTransfer : AlertBuddy.ViewModels.Services.ISettingsTransfer
        {
            public bool IsSupported => true;
            public Task<bool> SaveAsync (string suggestedName, string text) => Task.FromResult (true);
            public Task<string?> LoadAsync () => Task.FromResult<string?> (null);
        }

        [Fact]
        public async Task TheSettingsCopy_IsOffered_OnlyWhereTheDeviceCanPickAFile ()
        {
            foreach (var supported in new[] { false, true }) {
                var app = SmokeTests.CreateApp (transfer: supported ? new FakeTransfer () : null);
                try {
                    var form = new MainForm (app);
                    app.Main.OpenSettingsCommand.Execute (null);
                    Assert.IsType<GateViewModel> (app.Navigator.Current).HoldCompletedCommand.Execute (null);
                    HeadlessRenderer.CapturePng (form, 420, 2600);

                    var present = AccessibilityTests.Descendants (form.Controls.Cast<Control> ()).Any (c => c.Name == "settings.saveFile");
                    Assert.Equal (supported, present);
                    if (supported)
                        Assert.Contains (AccessibilityTests.Descendants (form.Controls.Cast<Control> ()), c => c.Name == "settings.loadFile");
                } finally {
                    await app.DisposeAsync ();
                }
            }
        }

        [Fact]
        public async Task FirstRun_OffersToRestoreFromAFile_WhereTheDeviceCanPickOne ()
        {
            var app = SmokeTests.CreateApp (transfer: new FakeTransfer ());
            try {
                var form = new MainForm (app);
                app.Navigator.GoTo<FirstRunViewModel> ();
                HeadlessRenderer.CapturePng (form, 420, 720);

                Assert.Contains (AccessibilityTests.Descendants (form.Controls.Cast<Control> ()), c => c.Name == "firstRun.restore");
            } finally {
                await app.DisposeAsync ();
            }
        }
    }
}
