using System.Drawing;
using AlertBuddy.Core.Ntfy;
using AlertBuddy.ViewModels.Screens;
using Majorsilence.Forms;
using Majorsilence.Forms.Headless;
using Xunit;

namespace AlertBuddy.Shared.Tests
{
    /// <summary>
    /// The views actually drive, and are driven by, their view models (PLAN.md section 10): a property change reaches the control, a press
    /// reaches the command, and a view that has been left no longer listens. Real clicks and real text input through the Headless backend,
    /// controls found by the <c>Name</c> the accessibility pass gave them.
    /// </summary>
    public class ViewBehaviourTests
    {
        private static T Find<T> (MainForm form, string name) where T : Control
            => Assert.IsAssignableFrom<T> (AccessibilityTests.Descendants (form.Controls.Cast<Control> ()).First (c => c.Name == name));

        private static void Render (MainForm form) => HeadlessRenderer.CapturePng (form, 420, 1400);

        // The middle of a control in window coordinates: its own offset inside each parent, all the way up. The chain already passes
        // through the form's client area and chrome, so nothing is added for them (adding the chrome again put the click 34 low).
        private static Point Centre (Control control)
        {
            int x = control.Width / 2, y = control.Height / 2;
            for (Control? c = control; c is not null; c = c.Parent) {
                x += c.Left;
                y += c.Top;
            }

            return new Point (x, y);
        }

        private static void Press (MainForm form, Control control)
        {
            var at = Centre (control);
            HeadlessRenderer.Click (form, at.X, at.Y);
        }

        [Fact]
        public async Task PressingPractice_OnHome_ReachesTheCommand ()
        {
            var app = SmokeTests.CreateApp ();
            try {
                var form = new MainForm (app);
                Render (form);

                Press (form, Find<Control> (form, "home.practice"));

                Assert.IsType<PracticeViewModel> (app.Navigator.Current);
            } finally {
                await app.DisposeAsync ();
            }
        }

        [Fact]
        public async Task PressingTold_OnTheAlarm_AcknowledgesIt_AndGoesHomeThankingThem ()
        {
            var app = SmokeTests.CreateApp ();
            try {
                var form = new MainForm (app);
                app.Engine.Handle (new NtfyEvent (NtfyEventKind.Message, new NtfyMessage (
                    "a1", DateTimeOffset.UtcNow, "home-alerts", "Workshop: temperature alarm", "Workshop is at 50.6 °C", 5, [])));
                Assert.IsType<AlarmViewModel> (app.Navigator.Current);
                Render (form);

                Press (form, Find<Control> (form, "alarm.told"));

                // Acknowledging silences the sound and returns to Home with the thanks, the alarm itself still open (PLAN.md section 4.3).
                var home = Assert.IsType<MainViewModel> (app.Navigator.Current);
                Assert.Equal ((BeaconMood.Reassured, AlertBuddy.ViewModels.Copy.Words.ThankYou), (home.Mood, home.StatusText));
                Render (form);
                Assert.Equal (AlertBuddy.ViewModels.Copy.Words.BeaconDescription (BeaconMood.Reassured), Find<Control> (form, "home.buddy").AccessibleDescription);
                Assert.Equal (AlertBuddy.ViewModels.Copy.Words.ThankYou, Find<Control> (form, "home.status").AccessibleName);
            } finally {
                await app.DisposeAsync ();
            }
        }

        [Fact]
        public async Task Settings_FieldsGoBothWays ()
        {
            var app = SmokeTests.CreateApp ();
            try {
                var form = new MainForm (app);
                app.Navigator.GoTo<SettingsViewModel> ();
                var vm = Assert.IsType<SettingsViewModel> (app.Navigator.Current);
                Render (form);

                var sounds = Find<CheckBox> (form, "settings.sounds");
                var name = Find<TextBox> (form, "settings.BuddyName");
                var silence = Find<NumericUpDown> (form, "settings.SilenceMinutes");

                // view model to control
                vm.SoundsEnabled = false;
                vm.BuddyName = "Sunny";
                vm.SilenceMinutes = 25;
                Assert.False (sounds.Checked);
                Assert.Equal ("Sunny", name.Text);
                Assert.Equal (25, (int)silence.Value);

                // control to view model
                sounds.Checked = true;
                name.Text = "Bo";
                silence.Value = 40;
                Assert.True (vm.SoundsEnabled);
                Assert.Equal ("Bo", vm.BuddyName);
                Assert.Equal (40, vm.SilenceMinutes);
            } finally {
                await app.DisposeAsync ();
            }
        }

        [Fact]
        public async Task ALeftView_NoLongerListens_ToItsViewModel ()
        {
            var app = SmokeTests.CreateApp ();
            try {
                var form = new MainForm (app);
                app.Navigator.GoTo<SettingsViewModel> ();
                var vm = Assert.IsType<SettingsViewModel> (app.Navigator.Current);
                Render (form);
                var name = Find<TextBox> (form, "settings.BuddyName");

                app.Navigator.GoBack ();        // Settings is left and its view disposed
                vm.BuddyName = "Nobody is listening";

                Assert.NotEqual ("Nobody is listening", name.Text);
            } finally {
                await app.DisposeAsync ();
            }
        }
    }
}
