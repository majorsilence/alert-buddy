using AlertBuddy.Shared.Theme;
using AlertBuddy.ViewModels;
using Majorsilence.Forms;

namespace AlertBuddy
{
    /// <summary>The desktop window: one <see cref="Shared.Views.PageHost"/> following <see cref="AlertBuddyApp.Navigator"/>.</summary>
    public partial class MainForm : Form
    {
        private readonly AlertBuddyApp app;

        /// <summary>Builds the window for an already-created app. Does not call <see cref="AlertBuddyApp.Start"/>; the head does that.</summary>
        public MainForm (AlertBuddyApp app)
        {
            this.app = app ?? throw new ArgumentNullException (nameof (app));
            AlertMotion.Follow (app.Settings);
            InitializeComponent ();
            FormClosed += (_, _) => app.DisposeAsync ().AsTask ().GetAwaiter ().GetResult ();
        }
    }
}
