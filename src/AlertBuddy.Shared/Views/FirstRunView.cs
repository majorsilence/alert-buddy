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
    /// <summary>First run (PLAN.md sections 4.3 and 9): five numbered steps, because this is the one true sequence in the app.</summary>
    public sealed class FirstRunView : UserControl
    {
        private readonly FirstRunViewModel vm;
        private readonly BindingScope scope = new ();
        private readonly Label stepLine;
        private readonly Panel body;
        private readonly Dictionary<FirstRunStep, FormColumn> steps = [];
        private readonly Label problem;
        private readonly ChunkyButton backButton;
        private readonly ChunkyButton nextButton;

        /// <summary>Builds first run for <paramref name="vm"/>.</summary>
        public FirstRunView (FirstRunViewModel vm)
        {
            this.vm = vm ?? throw new ArgumentNullException (nameof (vm));
            Dock = DockStyle.Fill;
            BackColor = AlertPalette.Ground;

            stepLine = new Label { AutoSize = true, ForeColor = AlertPalette.OnGround, Font = AlertFonts.Display (20), Location = new Point (24, 20) };
            Controls.Add (stepLine);

            body = new Panel { Name = "firstRun.body" };
            Controls.Add (body);

            problem = new Label { AutoSize = false, ForeColor = AlertPalette.Notice, Name = "firstRun.reason" };
            Controls.Add (problem);

            backButton = new ChunkyButton { Text = Loc.T ("Back"), Size = new Size (136, 56) }.Named ("firstRun.back");
            scope.Add (backButton.BindCommand (vm.BackCommand));
            Controls.Add (backButton);

            nextButton = new ChunkyButton { Size = new Size (160, 56), FillColor = AlertPalette.Cherry, OutlineColor = AlertPalette.GrapeInk, TextColor = AlertPalette.Paper }.Named ("firstRun.next");
            scope.Add (nextButton.BindCommand (vm.NextCommand));
            Controls.Add (nextButton);

            BuildNameStep ();
            BuildPinStep ();
            BuildServerStep ();
            BuildPermissionsStep ();
            BuildPracticeStep ();

            scope.Add (vm.Observe (nameof (FirstRunViewModel.Step), v => v.Step, ShowStep));
            scope.Add (vm.Observe (nameof (FirstRunViewModel.ValidationMessage), v => v.ValidationMessage, m => { problem.Text = m ?? ""; PerformCustomLayout (); }));

            Resize += (_, _) => PerformCustomLayout ();
            PerformCustomLayout ();
        }

        private FormColumn NewStep (FirstRunStep step, string heading)
        {
            var column = new FormColumn { Visible = false };
            column.AddHeading (heading);
            steps[step] = column;
            body.Controls.Add (column);
            return column;
        }

        private void BuildNameStep ()
        {
            var column = NewStep (FirstRunStep.NameBuddy, Loc.T ("Name your buddy"));
            column.AddLabel (Loc.T ("What should the buddy be called?"));
            var name = column.Add (new TextBox { Height = 48 }.Named ("firstRun.buddyName", Loc.T ("What should the buddy be called?")));
            scope.Add (name.BindText (vm, nameof (FirstRunViewModel.BuddyName), v => v.BuddyName, (v, t) => v.BuddyName = t));
            column.AddLabel (Loc.T ("Colour"));
            var colour = column.Add (new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Height = 48 }.Named ("firstRun.buddyColour", Loc.T ("Colour")));
            foreach (var option in Enum.GetNames<BuddyColour> ())
                colour.Items.Add (Loc.T (option));
            scope.Add (colour.BindSelectedIndex (vm, nameof (FirstRunViewModel.BuddyColour), v => (int)v.BuddyColour, (v, i) => v.BuddyColour = (BuddyColour)i));
        }

        private void BuildPinStep ()
        {
            var column = NewStep (FirstRunStep.GrownUpGate, Loc.T ("A grown-up sets a PIN"));
            column.AddParagraph (Loc.T ("The PIN keeps little fingers out of settings. It is a gate, not a lock."));
            column.AddLabel (Loc.T ("PIN (4 digits)"));
            var pin = column.Add (new TextBox { Height = 48, UseSystemPasswordChar = true, InputKind = TextInputKind.Number }.Named ("firstRun.pin", Loc.T ("PIN, 4 digits")));
            scope.Add (pin.BindText (vm, nameof (FirstRunViewModel.Pin), v => v.Pin, (v, t) => v.Pin = t));
            column.AddLabel (Loc.T ("PIN again"));
            var again = column.Add (new TextBox { Height = 48, UseSystemPasswordChar = true, InputKind = TextInputKind.Number }.Named ("firstRun.pinAgain", Loc.T ("PIN again")));
            scope.Add (again.BindText (vm, nameof (FirstRunViewModel.PinConfirm), v => v.PinConfirm, (v, t) => v.PinConfirm = t));
        }

        private void BuildServerStep ()
        {
            var column = NewStep (FirstRunStep.Server, Loc.T ("Server and sign-in"));
            column.AddLabel (Loc.T ("Server address"));
            var url = column.Add (new TextBox { Height = 48 }.Named ("firstRun.serverUrl", Loc.T ("Server address")));
            scope.Add (url.BindText (vm, nameof (FirstRunViewModel.ServerUrl), v => v.ServerUrl, (v, t) => v.ServerUrl = t));
            column.AddLabel (Loc.T ("Topic"));
            var topic = column.Add (new TextBox { Height = 48 }.Named ("firstRun.topic", Loc.T ("Topic")));
            scope.Add (topic.BindText (vm, nameof (FirstRunViewModel.Topic), v => v.Topic, (v, t) => v.Topic = t));

            column.AddLabel (Loc.T ("Sign in"));
            var auth = column.Add (new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Height = 48 }.Named ("firstRun.signIn", Loc.T ("Sign in")));
            foreach (var option in new[] { "No sign-in", "Username and password", "Access token" })
                auth.Items.Add (Loc.T (option));
            scope.Add (auth.BindSelectedIndex (vm, nameof (FirstRunViewModel.Auth), v => (int)v.Auth, (v, i) => v.Auth = (AuthMode)i));
            column.AddLabel (Loc.T ("Username"));
            var user = column.Add (new TextBox { Height = 48 }.Named ("firstRun.username", Loc.T ("Username")));
            scope.Add (user.BindText (vm, nameof (FirstRunViewModel.Username), v => v.Username, (v, t) => v.Username = t));
            column.AddLabel (Loc.T ("Password or token"));
            var secret = column.Add (new TextBox { Height = 48, UseSystemPasswordChar = true }.Named ("firstRun.secret", Loc.T ("Password or token")));
            scope.Add (secret.BindText (vm, nameof (FirstRunViewModel.Secret), v => v.Secret, (v, t) => v.Secret = t));

            var test = column.Add (new ChunkyButton { Text = Loc.T ("Test connection"), Height = 56 }.Named ("firstRun.testConnection"));
            scope.Add (test.BindCommand (vm.TestConnectionCommand));
            var result = column.AddParagraph ("");
            scope.Add (vm.Observe (nameof (FirstRunViewModel.TestResult), v => v.TestResult, r => { result.Text = r ?? ""; column.Relayout (); }));
        }

        private void BuildPermissionsStep ()
        {
            var column = NewStep (FirstRunStep.Permissions, Loc.T ("Letting the buddy listen"));
            var problem = column.AddParagraph ("");
            var section = column.AddSection ();
            var later = column.Add (new ChunkyButton { Text = Loc.T ("Later"), Height = 56 }.Named ("firstRun.later"), extraTop: 10);
            scope.Add (later.BindCommand (vm.LaterCommand));

            // The steps change when the person comes back from system settings, so the section is drawn again each time the view model
            // refreshes them. Its own scope keeps the buttons it binds from outliving the rows they were made for.
            BindingScope? rowScope = null;
            scope.Add (vm.Observe (nameof (FirstRunViewModel.PermissionsProblem), v => v.PermissionsProblem, _ => {
                rowScope?.Dispose ();
                rowScope = new BindingScope ();
                problem.Text = vm.PermissionsProblem ?? (vm.Permissions.Count == 0 ? Loc.T ("Nothing more is needed on this device.") : "");
                PermissionStepsView.Fill (section, vm.Permissions, rowScope);
                column.Relayout ();
            }));
            scope.Add (new Disposer (() => rowScope?.Dispose ()));
        }

        private sealed class Disposer (Action dispose) : IDisposable
        {
            public void Dispose () => dispose ();
        }

        private void BuildPracticeStep ()
        {
            var column = NewStep (FirstRunStep.Practice, Loc.T ("A practice run"));
            column.AddParagraph (Loc.T ("Try a pretend alert so you both know what to expect."));
            var practice = column.Add (new ChunkyButton { Text = Loc.T ("Practice"), Height = 56 }.Named ("firstRun.practice"));
            scope.Add (practice.BindCommand (vm.TryPracticeCommand));
            column.AddParagraph (vm.SafetyNote);
        }

        private void ShowStep (FirstRunStep current)
        {
            foreach (var (step, column) in steps)
                column.Visible = step == current;

            stepLine.Text = Loc.F ("Step {0} of {1}", vm.StepNumber, vm.StepCount);
            nextButton.Text = current == FirstRunStep.Practice ? Loc.T ("Finish") : Loc.T ("Next");
            PerformCustomLayout ();
        }

        private void PerformCustomLayout ()
        {
            var w = Width;
            const int footer = 88;

            // The reason line sits just above the footer, wrapped to the page, and the form ends where it starts: the form scrolls, so
            // nothing is lost, but nothing is drawn under the line either. With no reason the line takes no room.
            problem.Width = Math.Max (160, w - 48);
            problem.Height = problem.Text.Length > 0 ? FormColumn.ParagraphHeight (problem.Text, problem.Width) : 0;
            var reasonTop = Height - footer - 4 - problem.Height;
            problem.Location = new Point (24, reasonTop);

            body.Location = new Point (8, 64);
            body.Size = new Size (w - 16, Math.Max (0, reasonTop - 8 - 64));
            foreach (var column in steps.Values) {
                column.Location = new Point (0, 0);
                column.Size = body.Size;
                column.Relayout ();
            }

            backButton.Location = new Point (16, Height - footer + 16);
            nextButton.Location = new Point (w - nextButton.Width - 16, Height - footer + 16);
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
