using System;
using System.Windows.Forms;
using System.Runtime.Versioning;

[assembly: SupportedOSPlatform("windows")]

namespace HOI4Toolbox
{
    internal static class Program
    {
        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
            Application.Run(new MainForm());
        }
    }
}