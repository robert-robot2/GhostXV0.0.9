using System.Windows.Forms;

namespace GhostShell
{
    internal static class Program
    {
        [STAThread]
        static void Main()
        {
            GhostUIHost.Start();
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new GhostForm());
        }
    }
}