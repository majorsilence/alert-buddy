using AlertBuddy.Shared.Views;

namespace AlertBuddy
{
    partial class MainForm
    {
        private void InitializeComponent ()
        {
            Text = "Alert Buddy";
            ClientSize = new System.Drawing.Size (420, 720);

            Controls.Add (new PageHost (app.Navigator));
        }
    }
}
