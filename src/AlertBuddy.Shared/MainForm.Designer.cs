using Majorsilence.Forms;

namespace AlertBuddy
{
    partial class MainForm
    {
        private void InitializeComponent ()
        {
            Text = "Alert Buddy";
            ClientSize = new System.Drawing.Size (400, 300);

            // Placeholder until the Home screen exists (milestone 3). It sits in a docked panel because a docked
            // panel is inset by the system bars on Android while a control placed by Location is not (docs/spikes.md, S5).
            var host = Controls.Add (new Panel { Dock = DockStyle.Fill });
            host.Controls.Add (new Label {
                AutoSize = true,
                Location = new System.Drawing.Point (24, 24),
                Text = "Alert Buddy",
            });
        }
    }
}
