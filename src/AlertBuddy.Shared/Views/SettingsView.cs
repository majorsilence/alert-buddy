using System.Drawing;
using AlertBuddy.Core.Settings;
using AlertBuddy.Shared.Controls;
using AlertBuddy.Shared.Theme;
using AlertBuddy.ViewModels.Screens;
using Majorsilence.Forms;
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

            backButton = new ChunkyButton { Text = "Back", Size = new Size (120, 56), Location = new Point (16, 16) }.Named ("settings.back");
            scope.Add (backButton.BindCommand (vm.BackCommand));
            Controls.Add (backButton);
            Controls.Add (column);

            column.AddHeading ("Server");
            Caption ("Server address");
            Field (nameof (SettingsViewModel.ServerUrl), v => v.ServerUrl, (v, t) => v.ServerUrl = t);
            Caption ("Topic");
            Field (nameof (SettingsViewModel.Topic), v => v.Topic, (v, t) => v.Topic = t);

            Caption ("Sign in");
            var auth = Choice (nameof (SettingsViewModel.Auth), "No sign-in", "Username and password", "Access token");
            scope.Add (auth.BindSelectedIndex (vm, nameof (SettingsViewModel.Auth), v => (int)v.Auth, (v, i) => v.Auth = (AuthMode)i));
            Caption ("Username");
            Field (nameof (SettingsViewModel.Username), v => v.Username, (v, t) => v.Username = t);
            Caption ("Password or token (leave blank to keep the saved one)", paragraph: true);
            Field (nameof (SettingsViewModel.Secret), v => v.Secret, (v, t) => v.Secret = t, secret: true);

            problems = column.AddParagraph ("", AlertPalette.Notice);
            var test = column.Add (new ChunkyButton { Text = "Test connection", Height = 56 }.Named ("settings.testConnection"));
            scope.Add (test.BindCommand (vm.TestConnectionCommand));
            var testResult = column.AddParagraph ("");
            scope.Add (vm.Observe (nameof (SettingsViewModel.TestResult), v => v.TestResult, r => { testResult.Text = r ?? ""; column.Relayout (); }));

            column.AddHeading ("Buddy");
            Caption ("Buddy's name");
            Field (nameof (SettingsViewModel.BuddyName), v => v.BuddyName, (v, t) => v.BuddyName = t);
            Caption ("Look");
            var look = Choice (nameof (SettingsViewModel.Look), "Follow the device", "Day", "Night");
            scope.Add (look.BindSelectedIndex (vm, nameof (SettingsViewModel.Look), v => (int)v.Look, (v, i) => v.Look = (LookPreference)i));
            Caption ("Movement");
            var motion = Choice (nameof (SettingsViewModel.Motion), "Follow the device", "Calmer", "Full");
            scope.Add (motion.BindSelectedIndex (vm, nameof (SettingsViewModel.Motion), v => (int)v.Motion, (v, i) => v.Motion = (MotionPreference)i));

            column.AddHeading ("Sounds and quiet time");
            var sounds = column.Add (new CheckBox { Text = "Play sounds", Height = 48 }.Named ("settings.sounds"));
            scope.Add (sounds.BindChecked (vm, nameof (SettingsViewModel.SoundsEnabled), v => v.SoundsEnabled, (v, c) => v.SoundsEnabled = c));
            if (vm.CanReadAloud) {
                var aloud = column.Add (new CheckBox { Text = "Read alerts aloud", Height = 48 }.Named ("settings.readAloud"));
                scope.Add (aloud.BindChecked (vm, nameof (SettingsViewModel.ReadAloud), v => v.ReadAloud, (v, c) => v.ReadAloud = c));
            }
            var night = column.Add (new CheckBox { Text = "Quieter at night (8 pm to 7 am)", Height = 48 }.Named ("settings.night"));
            scope.Add (night.BindChecked (vm, nameof (SettingsViewModel.NightEnabled), v => v.NightEnabled, (v, c) => v.NightEnabled = c));
            Caption ("Minutes to stay quiet after \"Got it\"");
            Number (nameof (SettingsViewModel.SilenceMinutes), 1, 240, v => v.SilenceMinutes, (v, n) => v.SilenceMinutes = n);

            column.AddHeading ("Grown-up PIN");
            Caption ("New PIN (4 digits, leave blank to keep it)");
            Field (nameof (SettingsViewModel.NewPin), v => v.NewPin, (v, t) => v.NewPin = t, secret: true);
            Caption ("New PIN again");
            Field (nameof (SettingsViewModel.NewPinConfirm), v => v.NewPinConfirm, (v, t) => v.NewPinConfirm = t, secret: true);

            if (vm.Permissions.Count > 0) {
                column.AddHeading ("Letting the buddy listen");
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

            column.AddHeading ("Reading alerts");
            Caption ("Priority that means an alarm");
            Number (nameof (SettingsViewModel.AlarmPriority), 1, 5, v => v.AlarmPriority, (v, n) => v.AlarmPriority = n);
            Caption ("Priority that means a warning");
            Number (nameof (SettingsViewModel.WarningPriority), 1, 5, v => v.WarningPriority, (v, n) => v.WarningPriority = n);
            var emoji = column.Add (new CheckBox { Text = "Ignore a leading emoji in titles", Height = 48 }.Named ("settings.emoji"));
            scope.Add (emoji.BindChecked (vm, nameof (SettingsViewModel.StripLeadingEmoji), v => v.StripLeadingEmoji, (v, c) => v.StripLeadingEmoji = c));
            var reset = column.Add (new ChunkyButton { Text = "Use the usual rules", Height = 56 }.Named ("settings.resetRules"));
            scope.Add (reset.BindCommand (vm.ResetInterpretationCommand));

            var clear = column.Add (new ChunkyButton { Text = "Clear the Alert book", Height = 56 }.Named ("settings.clearBook"), extraTop: 14);
            scope.Add (clear.BindCommand (vm.ClearHistoryCommand));

            column.AddHeading ("About");
            column.AddParagraph ($"Alert Buddy {vm.Version}");
            column.AddParagraph (vm.SafetyNote);
            column.AddParagraph (vm.Privacy);

            saved = column.AddParagraph ("");
            var save = column.Add (new ChunkyButton { Text = "Save", Height = 64, FillColor = AlertPalette.Cherry, OutlineColor = AlertPalette.GrapeInk, TextColor = AlertPalette.Paper }.Named ("settings.save"));
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

        private TextBox Field (string property, Func<SettingsViewModel, string> get, Action<SettingsViewModel, string> set, bool secret = false)
        {
            var box = column.Add (new TextBox { Height = 48 }.Named ($"settings.{property}", lastLabel));
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

        private ComboBox Choice (string property, params string[] items)
        {
            var box = column.Add (new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Height = 48 }.Named ($"settings.{property}", lastLabel));
            foreach (var item in items)
                box.Items.Add (item);
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
