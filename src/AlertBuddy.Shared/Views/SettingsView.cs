using AlertBuddy.ViewModels.Services;
using AlertBuddy.Core.Localization;
using System.Drawing;
using AlertBuddy.Core.Settings;
using AlertBuddy.Shared.Controls;
using AlertBuddy.Shared.Theme;
using AlertBuddy.ViewModels.Screens;
using Majorsilence.Forms;
using Majorsilence.Forms.Backends;
using Majorsilence.Forms.Mvvm;

namespace AlertBuddy.Shared.Views
{
    /// <summary>Settings (PLAN.md section 9): a grouped form behind the gate. Edits stay in the view model until Save.</summary>
    public sealed class SettingsView : UserControl
    {
        private readonly SettingsViewModel vm;
        private readonly BindingScope scope = new ();
        private readonly FormColumn column = new ();
        private readonly ChunkyButton backButton;
        private readonly Label problems;
        private readonly Label saved;

        /// <summary>Builds Settings for <paramref name="vm"/>.</summary>
        public SettingsView (SettingsViewModel vm)
        {
            this.vm = vm ?? throw new ArgumentNullException (nameof (vm));
            Dock = DockStyle.Fill;
            BackColor = AlertPalette.Ground;

            backButton = new ChunkyButton { Text = Loc.T ("Back"), Size = new Size (136, 56), Location = new Point (16, 16) }.Named ("settings.back");
            scope.Add (backButton.BindCommand (vm.BackCommand));
            Controls.Add (backButton);
            Controls.Add (column);

            column.AddHeading (Loc.T ("Server"));
            Caption (Loc.T ("Server address"));
            Field (nameof (SettingsViewModel.ServerUrl), v => v.ServerUrl, (v, t) => v.ServerUrl = t);
            Caption (Loc.T ("Topic"));
            Field (nameof (SettingsViewModel.Topic), v => v.Topic, (v, t) => v.Topic = t);

            Caption (Loc.T ("Sign in"));
            var auth = Choice (nameof (SettingsViewModel.Auth), "No sign-in", "Username and password", "Access token");
            scope.Add (auth.BindSelectedIndex (vm, nameof (SettingsViewModel.Auth), v => (int)v.Auth, (v, i) => v.Auth = (AuthMode)i));
            Caption (Loc.T ("Username"));
            Field (nameof (SettingsViewModel.Username), v => v.Username, (v, t) => v.Username = t);
            Caption (Loc.T ("Password or token (leave blank to keep the saved one)"), paragraph: true);
            Field (nameof (SettingsViewModel.Secret), v => v.Secret, (v, t) => v.Secret = t, secret: true);

            problems = column.AddParagraph ("", AlertPalette.Notice);
            var test = column.Add (new ChunkyButton { Text = Loc.T ("Test connection"), Height = 56 }.Named ("settings.testConnection"));
            scope.Add (test.BindCommand (vm.TestConnectionCommand));
            var testResult = column.AddParagraph ("");
            scope.Add (vm.Observe (nameof (SettingsViewModel.TestResult), v => v.TestResult, r => { testResult.Text = r ?? ""; column.Relayout (); }));

            column.AddHeading (Loc.T ("Buddy"));
            Caption (Loc.T ("Buddy's name"));
            Field (nameof (SettingsViewModel.BuddyName), v => v.BuddyName, (v, t) => v.BuddyName = t);
            Caption (Loc.T ("Language"));
            var language = Choice (nameof (SettingsViewModel.Language), "Follow the device", "English", "Français");
            scope.Add (language.BindSelectedIndex (vm, nameof (SettingsViewModel.Language), v => (int)v.Language, (v, i) => v.Language = (AppLanguage)i));
            Caption (Loc.T ("Look"));
            var look = Choice (nameof (SettingsViewModel.Look), "Follow the device", "Light", "Dark");
            scope.Add (look.BindSelectedIndex (vm, nameof (SettingsViewModel.Look), v => (int)v.Look, (v, i) => v.Look = (LookPreference)i));
            Caption (Loc.T ("Movement"));
            var motion = Choice (nameof (SettingsViewModel.Motion), "Follow the device", "Calmer", "Full");
            scope.Add (motion.BindSelectedIndex (vm, nameof (SettingsViewModel.Motion), v => (int)v.Motion, (v, i) => v.Motion = (MotionPreference)i));

            column.AddHeading (Loc.T ("Sounds and quiet time"));
            var sounds = column.Add (new CheckBox { Text = Loc.T ("Play sounds"), Height = 48 }.Named ("settings.sounds"));
            scope.Add (sounds.BindChecked (vm, nameof (SettingsViewModel.SoundsEnabled), v => v.SoundsEnabled, (v, c) => v.SoundsEnabled = c));
            if (vm.CanReadAloud) {
                var aloud = column.Add (new CheckBox { Text = Loc.T ("Read alerts aloud"), Height = 48 }.Named ("settings.readAloud"));
                scope.Add (aloud.BindChecked (vm, nameof (SettingsViewModel.ReadAloud), v => v.ReadAloud, (v, c) => v.ReadAloud = c));
            }
            Caption (Loc.T ("Alarm sound"));
            var tone = Choice (nameof (SettingsViewModel.AlarmTone), "Whoop", "Code 3", "March time", "Continuous", "Voice evacuation");
            scope.Add (tone.BindSelectedIndex (vm, nameof (SettingsViewModel.AlarmTone), v => (int)v.AlarmTone, (v, i) => v.AlarmTone = (AlarmTone)i));
            var hear = column.Add (new ChunkyButton { Text = Loc.T ("Hear the alarm sound"), Height = 56 }.Named ("settings.hearAlarm"));
            scope.Add (hear.BindCommand (vm.PreviewAlarmToneCommand));
            if (vm.CanReadAloud) {
                BuildVoicePicker ();
                var pitchCaption = Caption (Loc.T ("Voice pitch"));
                var voice = Choice (nameof (SettingsViewModel.Voice), "Deeper", "Standard", "Lighter");
                scope.Add (voice.BindSelectedIndex (vm, nameof (SettingsViewModel.Voice), v => (int)v.Voice, (v, i) => v.Voice = (VoiceType)i));
                // The pitch presets lower or raise the device's own voice; a voice picked by name is spoken as it is.
                scope.Add (vm.Observe (nameof (SettingsViewModel.VoiceId), v => v.VoiceId, id => {
                    voice.Visible = id is null;
                    pitchCaption.Visible = id is null;
                    column.Relayout ();
                }));
                var hearVoice = column.Add (new ChunkyButton { Text = Loc.T ("Hear the voice"), Height = 56 }.Named ("settings.hearVoice"));
                scope.Add (hearVoice.BindCommand (vm.PreviewVoiceCommand));
            }

            Caption (Loc.T ("Practice sound"));
            var practice = Choice (nameof (SettingsViewModel.PracticeSound), "Gentle", "Whoop", "Code 3", "March time", "Continuous", "Voice evacuation");
            scope.Add (practice.BindSelectedIndex (vm, nameof (SettingsViewModel.PracticeSound), v => (int)v.PracticeSound, (v, i) => v.PracticeSound = (PracticeSound)i));
            var night = column.Add (new CheckBox { Text = Loc.T ("Quieter at night (8 pm to 7 am)"), Height = 48 }.Named ("settings.night"));
            scope.Add (night.BindChecked (vm, nameof (SettingsViewModel.NightEnabled), v => v.NightEnabled, (v, c) => v.NightEnabled = c));
            Caption (Loc.T ("Minutes to stay quiet after \"Got it\""));
            Number (nameof (SettingsViewModel.SilenceMinutes), 1, 240, v => v.SilenceMinutes, (v, n) => v.SilenceMinutes = n);

            column.AddHeading (Loc.T ("Grown-up PIN"));
            Caption (Loc.T ("New PIN (4 digits, leave blank to keep it)"));
            Field (nameof (SettingsViewModel.NewPin), v => v.NewPin, (v, t) => v.NewPin = t, secret: true, kind: TextInputKind.Number);
            Caption (Loc.T ("New PIN again"));
            Field (nameof (SettingsViewModel.NewPinConfirm), v => v.NewPinConfirm, (v, t) => v.NewPinConfirm = t, secret: true, kind: TextInputKind.Number);

            if (vm.Permissions.Count > 0) {
                column.AddHeading (Loc.T ("Letting the buddy listen"));
                var steps = column.AddSection ();
                BindingScope? stepScope = null;
                scope.Add (vm.Observe (nameof (SettingsViewModel.PermissionsRefreshed), v => v.PermissionsRefreshed, _ => {
                    stepScope?.Dispose ();
                    stepScope = new BindingScope ();
                    PermissionStepsView.Fill (steps, vm.Permissions, stepScope);
                    column.Relayout ();
                }));
                scope.Add (new Disposer (() => stepScope?.Dispose ()));
            }

            column.AddHeading (Loc.T ("Reading alerts"));
            Caption (Loc.T ("Priority that means an alarm"));
            Number (nameof (SettingsViewModel.AlarmPriority), 1, 5, v => v.AlarmPriority, (v, n) => v.AlarmPriority = n);
            Caption (Loc.T ("Priority that means a warning"));
            Number (nameof (SettingsViewModel.WarningPriority), 1, 5, v => v.WarningPriority, (v, n) => v.WarningPriority = n);
            var emoji = column.Add (new CheckBox { Text = Loc.T ("Ignore a leading emoji in titles"), Height = 48 }.Named ("settings.emoji"));
            scope.Add (emoji.BindChecked (vm, nameof (SettingsViewModel.StripLeadingEmoji), v => v.StripLeadingEmoji, (v, c) => v.StripLeadingEmoji = c));
            var reset = column.Add (new ChunkyButton { Text = Loc.T ("Use the usual rules"), Height = 56 }.Named ("settings.resetRules"));
            scope.Add (reset.BindCommand (vm.ResetInterpretationCommand));

            var clear = column.Add (new ChunkyButton { Text = Loc.T ("Clear the Alert book"), Height = 56 }.Named ("settings.clearBook"), extraTop: 14);
            scope.Add (clear.BindCommand (vm.ClearHistoryCommand));

            column.AddHeading (Loc.T ("About"));
            column.AddParagraph ($"Alert Buddy {vm.Version}");
            column.AddParagraph (vm.SafetyNote);
            column.AddParagraph (vm.Privacy);

            saved = column.AddParagraph ("");
            var save = column.Add (new ChunkyButton { Text = Loc.T ("Save"), Height = 64, FillColor = AlertPalette.Cherry, OutlineColor = AlertPalette.GrapeInk, TextColor = AlertPalette.Paper }.Named ("settings.save"));
            scope.Add (save.BindCommand (vm.SaveCommand));

            scope.Add (vm.Observe (nameof (SettingsViewModel.SavedMessage), v => v.SavedMessage, m => { saved.Text = m ?? ""; column.Relayout (); }));
            foreach (var name in new[] { nameof (SettingsViewModel.ServerProblem), nameof (SettingsViewModel.TopicProblem), nameof (SettingsViewModel.SignInProblem), nameof (SettingsViewModel.PinProblem), nameof (SettingsViewModel.NameProblem) })
                scope.Add (vm.Observe (name, v => v, _ => ShowProblems ()));

            Resize += (_, _) => PerformCustomLayout ();
            PerformCustomLayout ();
        }

        private sealed class Disposer (Action dispose) : IDisposable
        {
            public void Dispose () => dispose ();
        }

        // The label above a field is a separate control, so the field has to be told its own: remembered here as each is added.
        private string lastLabel = "";

        private Label Caption (string text, bool paragraph = false)
        {
            lastLabel = text;
            return paragraph ? column.AddParagraph (text) : column.AddLabel (text);
        }

        private TextBox Field (string property, Func<SettingsViewModel, string> get, Action<SettingsViewModel, string> set, bool secret = false, TextInputKind kind = TextInputKind.Normal)
        {
            var box = column.Add (new TextBox { Height = 48, InputKind = kind }.Named ($"settings.{property}", lastLabel));
            if (secret)
                box.UseSystemPasswordChar = true;
            scope.Add (box.BindText (vm, property, get, set));
            return box;
        }

        private NumericUpDown Number (string property, int min, int max, Func<SettingsViewModel, int> get, Action<SettingsViewModel, int> set)
        {
            var box = column.Add (new NumericUpDown { Minimum = min, Maximum = max, Height = 48 }.Named ($"settings.{property}", lastLabel));
            scope.Add (box.BindValue (vm, property, v => get (v), (v, n) => set (v, (int)n)));
            return box;
        }

        // The installed voices of the language in use: "Automatic", then each one named and, where the device says, whether it is a
        // man's or a woman's. The list arrives from the device when it arrives, so the choices are filled in as it does.
        private void BuildVoicePicker ()
        {
            Caption (Loc.T ("Voice"));
            var box = column.Add (new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Height = 48 }.Named ("settings.VoiceId", lastLabel));
            var filling = false;

            void Fill ()
            {
                filling = true;
                box.Items.Clear ();
                box.Items.Add (Loc.T ("Automatic"));
                foreach (var option in vm.AvailableVoices)
                    box.Items.Add (VoiceLabel (option));
                box.SelectedIndex = SelectedVoiceIndex ();
                filling = false;
            }

            int SelectedVoiceIndex ()
            {
                for (var i = 0; i < vm.AvailableVoices.Count; i++)
                    if (vm.AvailableVoices[i].Id == vm.VoiceId)
                        return i + 1;
                return 0;
            }

            box.SelectedIndexChanged += (_, _) => {
                if (!filling)
                    vm.VoiceId = box.SelectedIndex is > 0 and var i && i - 1 < vm.AvailableVoices.Count ? vm.AvailableVoices[i - 1].Id : null;
            };
            vm.AvailableVoices.CollectionChanged += OnVoicesChanged;
            scope.Add (new Unsubscribe (() => vm.AvailableVoices.CollectionChanged -= OnVoicesChanged));
            scope.Add (vm.Observe (nameof (SettingsViewModel.VoiceId), v => v.VoiceId, _ => {
                if (!filling && box.Items.Count > 0 && box.SelectedIndex != SelectedVoiceIndex ())
                    box.SelectedIndex = SelectedVoiceIndex ();
            }));
            Fill ();

            void OnVoicesChanged (object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e) => Fill ();
        }

        private static string VoiceLabel (VoiceOption option)
        {
            // The list is already of the language in use, so the language is not repeated; a man's or a woman's voice is said where the device says.
            var sex = option.Sex switch { VoiceSex.Male => Loc.T ("man"), VoiceSex.Female => Loc.T ("woman"), _ => "" };
            return sex.Length > 0 ? $"{option.Name} ({sex})" : option.Name;
        }

        private sealed class Unsubscribe (Action action) : IDisposable
        {
            public void Dispose () => action ();
        }

        private ComboBox Choice (string property, params string[] items)
        {
            var box = column.Add (new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Height = 48 }.Named ($"settings.{property}", lastLabel));
            foreach (var item in items)
                box.Items.Add (Loc.T (item));
            return box;
        }

        // The first thing wrong, in plain words, next to Save rather than scattered under every field.
        private void ShowProblems ()
        {
            problems.Text = vm.ServerProblem ?? vm.TopicProblem ?? vm.SignInProblem ?? vm.PinProblem ?? vm.NameProblem ?? "";
            column.Relayout ();
        }

        private void PerformCustomLayout ()
        {
            column.Location = new Point (8, backButton.Bottom + 8);
            column.Size = new Size (Width - 16, Math.Max (0, Height - column.Top - 8));
            column.Relayout ();
        }

        /// <inheritdoc/>
        protected override void Dispose (bool disposing)
        {
            if (disposing)
                scope.Dispose ();

            base.Dispose (disposing);
        }
    }
}
