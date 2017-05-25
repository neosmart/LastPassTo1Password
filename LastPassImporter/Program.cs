using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace LastPassImporter
{
    static class Program
    {
        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            SetProcessDPIAware();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new ConverterUi());
        }

        [DllImport("user32.dll")]
        private static extern bool SetProcessDPIAware();
    }
}
