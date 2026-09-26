using Majorsilence.Forms;

namespace AlertBuddy.Desktop
{
    internal static class Program
    {
        [STAThread]
        private static void Main (string[] args)
        {
            Application.Run (new MainForm ());
        }
    }
}
