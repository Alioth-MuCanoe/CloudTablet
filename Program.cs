using System;
using System.IO;
using System.Text;
using System.Windows.Forms;

namespace LightReader
{
    internal static class Program
    {
        [STAThread]
        private static void Main(string[] args)
        {
            Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
            Application.ThreadException += delegate(object sender, System.Threading.ThreadExceptionEventArgs e)
            {
                ReportError(e.Exception);
            };
            AppDomain.CurrentDomain.UnhandledException += delegate(object sender, UnhandledExceptionEventArgs e)
            {
                ReportError(e.ExceptionObject as Exception);
            };

            try
            {
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
            }
            catch
            {
            }

            string[] startup = args ?? new string[0];
            try
            {
                Application.Run(new ReaderForm(startup));
            }
            catch (Exception ex)
            {
                ReportError(ex);
            }
        }

        private static void ReportError(Exception ex)
        {
            if (ex == null)
                return;
            try
            {
                Directory.CreateDirectory(ReaderSettings.SettingsFolder);
                string log = Path.Combine(ReaderSettings.SettingsFolder, "error.log");
                File.AppendAllText(log,
                    DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + Environment.NewLine +
                    ex.ToString() + Environment.NewLine + Environment.NewLine,
                    Encoding.UTF8);
            }
            catch
            {
            }
            try
            {
                MessageBox.Show(
                    "云牍遇到了一个问题，但不会影响已经保存的阅读位置。\r\n\r\n" +
                    ex.Message + "\r\n\r\n详细信息已记录到：\r\n" +
                    Path.Combine(ReaderSettings.SettingsFolder, "error.log"),
                    "云牍", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            catch
            {
            }
        }
    }
}
