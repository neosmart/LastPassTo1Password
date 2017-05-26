using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
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

            Form ui = null;
            var processStarted = new ManualResetEventSlim(false);
            var converter = new Converter();
            converter.ConversionStarted += () =>
            {
                processStarted.Set();
            };

            var complete = new ManualResetEventSlim(false);
            converter.ConversionCompleted += (b) =>
            {
                complete.Set();
            };
            converter.ProcessCompleted += () => complete.Set();

            try
            {
                var thread = new Thread(converter.LoadAndConvert);
                thread.SetApartmentState(ApartmentState.STA);
                thread.Start();

                var waitHandles = new[] { processStarted.WaitHandle, complete.WaitHandle };
                int waitResult = WaitHandle.WaitAny(waitHandles);
                if (waitResult == 0)
                {
                    ui = new ConverterUi(converter);
                    ui.StartPosition = FormStartPosition.CenterScreen;
                    Application.Run(ui);

                    complete.Wait();
                }
            }
            catch (ConverterException ex)
            {
                MessageBox.Show(ex.Message, ex.Title, MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Unknown exception during conversion!", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        [DllImport("user32.dll")]
        private static extern bool SetProcessDPIAware();
    }
}
