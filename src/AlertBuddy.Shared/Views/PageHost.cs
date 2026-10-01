using AlertBuddy.Shared.Theme;
using AlertBuddy.ViewModels.Screens;
using AlertBuddy.ViewModels.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using Majorsilence.Forms;

namespace AlertBuddy.Shared.Views
{
    /// <summary>
    /// Swaps pages as <see cref="INavigator.Current"/> changes (PLAN.md section 8.5). A screen with no registered view yet shows an
    /// honest placeholder instead of a blank window or a crash, so navigating to an unfinished screen is safe.
    /// </summary>
    public sealed class PageHost : UserControl
    {
        private readonly INavigator navigator;
        private readonly Dictionary<Type, Func<ObservableObject, Control>> factories = [];
        private Control? current;

        /// <summary>Builds a page host that follows <paramref name="navigator"/>.</summary>
        public PageHost (INavigator navigator)
        {
            this.navigator = navigator ?? throw new ArgumentNullException (nameof (navigator));
            Dock = DockStyle.Fill;

            Register<MainViewModel> (vm => new HomeView (vm));
            Register<AlarmViewModel> (vm => new AlarmView (vm));
            Register<AlertDetailViewModel> (vm => new AlertDetailView (vm));
            Register<AlertBookViewModel> (vm => new AlertBookView (vm));
            Register<PracticeViewModel> (vm => new PracticeView (vm));
            Register<GateViewModel> (vm => new GateView (vm));
            Register<SettingsViewModel> (vm => new SettingsView (vm));
            Register<FirstRunViewModel> (vm => new FirstRunView (vm));

            navigator.CurrentChanged += ShowCurrentScreen;
            ShowCurrentScreen ();
        }

        /// <summary>Says how to build the view for a screen type. Home, the alarm takeover, detail, the Alert book, Practice, the gate, Settings and First run are wired in already.</summary>
        public void Register<T> (Func<T, Control> factory) where T : ObservableObject
            => factories[typeof (T)] = vm => factory ((T)vm);

        private void ShowCurrentScreen ()
        {
            current?.Dispose ();
            current = Build (navigator.Current);
            Controls.Clear ();
            Controls.Add (current);
        }

        private Control Build (ObservableObject screen)
        {
            if (factories.TryGetValue (screen.GetType (), out var factory))
                return factory (screen);

            // No view for this screen type yet (PLAN.md milestone 3 is not finished): say so plainly rather than showing nothing.
            return new Panel {
                Dock = DockStyle.Fill,
                BackColor = AlertPalette.Paper,
                Controls = {
                    new Label {
                        AutoSize = true,
                        Location = new System.Drawing.Point (24, 24),
                        Text = $"{screen.GetType ().Name} is on the way.",
                        ForeColor = AlertPalette.OnGround,
                    },
                },
            };
        }

        /// <inheritdoc/>
        protected override void Dispose (bool disposing)
        {
            if (disposing) {
                navigator.CurrentChanged -= ShowCurrentScreen;
                current?.Dispose ();
            }

            base.Dispose (disposing);
        }
    }
}
