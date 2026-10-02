using System.Drawing;
using AlertBuddy.Core.Settings;
using AlertBuddy.Shared.Controls;
using AlertBuddy.Shared.Theme;
using AlertBuddy.ViewModels.Screens;
using Majorsilence.Forms;
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

            body = new Panel ();
            Controls.Add (body);

            problem = new Label { AutoSize = true, ForeColor = AlertPalette.Notice };
            Controls.Add (problem);

            backButton = new ChunkyButton { Text = "Back", Size = new Size (120, 56) };
            scope.Add (backButton.BindCommand (vm.BackCommand));
            Controls.Add (backButton);

            nextButton = new ChunkyButton { Size = new Size (160, 56), FillColor = AlertPalette.Cherry, OutlineColor = AlertPalette.GrapeInk, TextColor = AlertPalette.Paper };
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
            var column = NewStep (FirstRunStep.NameBuddy, "Name your buddy");
            column.AddLabel ("What should the buddy be called?");
            var name = column.Add (new TextBox { Height = 40 });
            scope.Add (name.BindText (vm, nameof (FirstRunViewModel.BuddyName), v => v.BuddyName, (v, t) => v.BuddyName = t));
            column.AddLabel ("Colour");
            var colour = column.Add (new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Height = 40 });
            foreach (var option in Enum.GetNames<BuddyColour> ())
                colour.Items.Add (option);
            scope.Add (colour.BindSelectedIndex (vm, nameof (FirstRunViewModel.BuddyColour), v => (int)v.BuddyColour, (v, i) => v.BuddyColour = (BuddyColour)i));
        }

        private void BuildPinStep ()
        {
            var column = NewStep (FirstRunStep.GrownUpGate, "A grown-up sets a PIN");
            column.AddParagraph ("The PIN keeps little fingers out of settings. It is a gate, not a lock.");
            column.AddLabel ("PIN (4 digits)");
            var pin = column.Add (new TextBox { Height = 40, UseSystemPasswordChar = true });
            scope.Add (pin.BindText (vm, nameof (FirstRunViewModel.Pin), v => v.Pin, (v, t) => v.Pin = t));
            column.AddLabel ("PIN again");
            var again = column.Add (new TextBox { Height = 40, UseSystemPasswordChar = true });
            scope.Add (again.BindText (vm, nameof (FirstRunViewModel.PinConfirm), v => v.PinConfirm, (v, t) => v.PinConfirm = t));
        }

        private void BuildServerStep ()
        {
            var column = NewStep (FirstRunStep.Server, "Server and sign-in");
            column.AddLabel ("Server address");
            var url = column.Add (new TextBox { Height = 40 });
            scope.Add (url.BindText (vm, nameof (FirstRunViewModel.ServerUrl), v => v.ServerUrl, (v, t) => v.ServerUrl = t));
            column.AddLabel ("Topic");
            var topic = column.Add (new TextBox { Height = 40 });
            scope.Add (topic.BindText (vm, nameof (FirstRunViewModel.Topic), v => v.Topic, (v, t) => v.Topic = t));

            column.AddLabel ("Sign in");
            var auth = column.Add (new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Height = 40 });
            foreach (var option in new[] { "No sign-in", "Username and password", "Access token" })
                auth.Items.Add (option);
            scope.Add (auth.BindSelectedIndex (vm, nameof (FirstRunViewModel.Auth), v => (int)v.Auth, (v, i) => v.Auth = (AuthMode)i));
            column.AddLabel ("Username");
            var user = column.Add (new TextBox { Height = 40 });
            scope.Add (user.BindText (vm, nameof (FirstRunViewModel.Username), v => v.Username, (v, t) => v.Username = t));
            column.AddLabel ("Password or token");
            var secret = column.Add (new TextBox { Height = 40, UseSystemPasswordChar = true });
            scope.Add (secret.BindText (vm, nameof (FirstRunViewModel.Secret), v => v.Secret, (v, t) => v.Secret = t));

            var test = column.Add (new ChunkyButton { Text = "Test connection", Height = 56 });
            scope.Add (test.BindCommand (vm.TestConnectionCommand));
            var result = column.AddParagraph ("");
            scope.Add (vm.Observe (nameof (FirstRunViewModel.TestResult), v => v.TestResult, r => { result.Text = r ?? ""; column.Relayout (); }));
        }

        private void BuildPermissionsStep ()
        {
            var column = NewStep (FirstRunStep.Permissions, "Letting the buddy listen");
            var problem = column.AddParagraph ("");
            var section = column.AddSection ();
            var later = column.Add (new ChunkyButton { Text = "Later", Height = 56 }, extraTop: 10);
            scope.Add (later.BindCommand (vm.LaterCommand));

            // The steps change when the person comes back from system settings, so the section is drawn again each time the view model
            // refreshes them. Its own scope keeps the buttons it binds from outliving the rows they were made for.
            BindingScope? rowScope = null;
            scope.Add (vm.Observe (nameof (FirstRunViewModel.PermissionsProblem), v => v.PermissionsProblem, _ => {
                rowScope?.Dispose ();
                rowScope = new BindingScope ();
                problem.Text = vm.PermissionsProblem ?? (vm.Permissions.Count == 0 ? "Nothing more is needed on this device." : "");
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
            var column = NewStep (FirstRunStep.Practice, "A practice run");
            column.AddParagraph ("Try a pretend alert so you both know what to expect.");
            var practice = column.Add (new ChunkyButton { Text = "Practice", Height = 56 });
            scope.Add (practice.BindCommand (vm.TryPracticeCommand));
            column.AddParagraph (vm.SafetyNote);
        }

        private void ShowStep (FirstRunStep current)
        {
            foreach (var (step, column) in steps)
                column.Visible = step == current;

            stepLine.Text = $"Step {vm.StepNumber} of {vm.StepCount}";
            nextButton.Text = current == FirstRunStep.Practice ? "Finish" : "Next";
            PerformCustomLayout ();
        }

        private void PerformCustomLayout ()
        {
            var w = Width;
            const int footer = 88;
            body.Location = new Point (8, 64);
            body.Size = new Size (w - 16, Math.Max (0, Height - 64 - footer - 24));
            foreach (var column in steps.Values) {
                column.Location = new Point (0, 0);
                column.Size = body.Size;
                column.Relayout ();
            }

            problem.Location = new Point (24, Height - footer - 20);
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
