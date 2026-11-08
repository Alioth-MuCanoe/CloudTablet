using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Windows.Forms;
using Microsoft.Win32;

namespace CloudTabletUninstall
{
    internal static class Program
    {
        [STAThread]
        private static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new UninstallForm());
        }
    }

    internal sealed class UninstallForm : Form
    {
        private readonly CheckBox _deleteData;
        private readonly Button _uninstall;
        private readonly Button _cancel;
        private readonly Label _status;
        private readonly float _scale;

        public UninstallForm()
        {
            _scale = 1f;
            try
            {
                using (Graphics g = CreateGraphics())
                    _scale = Math.Max(1f, g.DpiX / 96f);
            }
            catch
            {
            }
            Text = "卸载云牍";
            AutoScaleMode = AutoScaleMode.None;
            ClientSize = new Size(510, 330);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition = FormStartPosition.CenterScreen;
            MaximizeBox = false;
            MinimizeBox = false;
            BackColor = Color.FromArgb(247, 241, 222);
            Font = new Font("Microsoft YaHei UI", 9.5f, FontStyle.Regular,
                GraphicsUnit.Point);
            try
            {
                Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
            }
            catch
            {
            }

            PictureBox logo = new PictureBox();
            logo.Bounds = new Rectangle(28, 24, 54, 54);
            logo.SizeMode = PictureBoxSizeMode.StretchImage;
            logo.BackColor = Color.FromArgb(87, 117, 127);
            try
            {
                using (Stream stream = Assembly.GetExecutingAssembly()
                    .GetManifestResourceStream("CloudTablet.logo.png"))
                {
                    if (stream != null)
                        logo.Image = new Bitmap(stream);
                }
            }
            catch
            {
            }
            if (logo.Image == null)
            {
                try
                {
                    using (Icon ico = Icon.ExtractAssociatedIcon(
                        Application.ExecutablePath))
                    using (Icon big = new Icon(ico, 128, 128))
                        logo.Image = big.ToBitmap();
                }
                catch
                {
                }
            }
            Controls.Add(logo);

            Label title = new Label();
            title.Text = "卸载云牍";
            title.Font = new Font("Microsoft YaHei UI", 17f, FontStyle.Bold,
                GraphicsUnit.Point);
            title.ForeColor = Color.FromArgb(38, 58, 62);
            title.Bounds = new Rectangle(96, 22, 380, 34);
            Controls.Add(title);

            Label english = new Label();
            english.Text = "CloudTablet   ·   版本 1.2";
            english.ForeColor = Color.FromArgb(105, 124, 126);
            english.Bounds = new Rectangle(98, 56, 380, 22);
            Controls.Add(english);

            Label intro = new Label();
            intro.Text = "将删除程序文件和桌面、开始菜单快捷方式。\r\n" +
                "默认保留书架、封面、阅读位置和背景图片。";
            intro.ForeColor = Color.FromArgb(55, 76, 78);
            intro.Bounds = new Rectangle(30, 96, 450, 44);
            Controls.Add(intro);

            _deleteData = new CheckBox();
            _deleteData.Text = "同时删除书架、封面、阅读位置和背景图片";
            _deleteData.ForeColor = Color.FromArgb(38, 58, 62);
            _deleteData.Bounds = new Rectangle(30, 148, 450, 24);
            _deleteData.Checked = false;
            Controls.Add(_deleteData);

            Label note = new Label();
            note.Text = "勾选后不可恢复；不勾选可保留数据，之后重新安装仍可继续阅读。";
            note.ForeColor = Color.FromArgb(120, 136, 136);
            note.Bounds = new Rectangle(30, 176, 450, 22);
            Controls.Add(note);

            Label developer = new Label();
            developer.Text = "开发者：@玉衡景行   ·   DeepSeek V4.1 开发";
            developer.ForeColor = Color.FromArgb(100, 120, 120);
            developer.Bounds = new Rectangle(30, 214, 450, 22);
            Controls.Add(developer);

            LinkLabel github = new LinkLabel();
            github.Text = "github.com/Alioth-MuCanoe";
            github.LinkBehavior = LinkBehavior.HoverUnderline;
            github.LinkColor = Color.FromArgb(63, 110, 115);
            github.ActiveLinkColor = Color.FromArgb(45, 85, 90);
            github.Bounds = new Rectangle(30, 236, 300, 22);
            github.LinkClicked += delegate
            {
                try
                {
                    Process.Start(new ProcessStartInfo(
                        "https://github.com/Alioth-MuCanoe")
                    {
                        UseShellExecute = true
                    });
                }
                catch
                {
                }
            };
            Controls.Add(github);

            _status = new Label();
            _status.Text = "程序位置：" + ExpectedInstallDir();
            _status.ForeColor = Color.FromArgb(120, 136, 136);
            _status.AutoEllipsis = true;
            _status.Bounds = new Rectangle(30, 266, 450, 20);
            Controls.Add(_status);

            _uninstall = new Button();
            _uninstall.Text = "卸载";
            _uninstall.Bounds = new Rectangle(286, 292, 94, 30);
            _uninstall.BackColor = Color.FromArgb(63, 110, 115);
            _uninstall.ForeColor = Color.White;
            _uninstall.FlatStyle = FlatStyle.Flat;
            _uninstall.FlatAppearance.BorderSize = 0;
            _uninstall.Click += delegate { DoUninstall(); };
            Controls.Add(_uninstall);

            _cancel = new Button();
            _cancel.Text = "取消";
            _cancel.Bounds = new Rectangle(390, 292, 94, 30);
            _cancel.FlatStyle = FlatStyle.System;
            _cancel.DialogResult = DialogResult.Cancel;
            Controls.Add(_cancel);
            CancelButton = _cancel;
            ApplyScale();
        }

        private void ApplyScale()
        {
            if (_scale <= 1.01f)
                return;
            foreach (Control control in Controls)
            {
                control.Bounds = new Rectangle(
                    (int)Math.Round(control.Left * _scale),
                    (int)Math.Round(control.Top * _scale),
                    (int)Math.Round(control.Width * _scale),
                    (int)Math.Round(control.Height * _scale));
            }
            ClientSize = new Size(
                (int)Math.Round(ClientSize.Width * _scale),
                (int)Math.Round(ClientSize.Height * _scale));
        }

        private void DoUninstall()
        {
            if (MessageBox.Show(this, "确定要卸载云牍吗？", "卸载云牍",
                MessageBoxButtons.YesNo, MessageBoxIcon.Question,
                MessageBoxDefaultButton.Button2) != DialogResult.Yes)
                return;

            string installDir = Path.GetDirectoryName(Application.ExecutablePath);
            string expected = ExpectedInstallDir();
            if (!string.Equals(Path.GetFullPath(installDir).TrimEnd('\\'),
                Path.GetFullPath(expected).TrimEnd('\\'),
                StringComparison.OrdinalIgnoreCase))
            {
                MessageBox.Show(this,
                    "卸载程序不在标准安装目录中，已停止操作。\r\n\r\n" +
                    "标准位置：\r\n" + expected,
                    "卸载云牍", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            _uninstall.Enabled = false;
            _cancel.Enabled = false;
            try
            {
                _status.Text = "正在关闭云牍…";
                Application.DoEvents();
                CloseRunningApps();

                _status.Text = "正在删除快捷方式…";
                Application.DoEvents();
                DeleteShortcuts();

                _status.Text = "正在移除卸载信息…";
                Application.DoEvents();
                DeleteRegistryEntry();

                bool dataFailed = false;
                if (_deleteData.Checked)
                {
                    _status.Text = "正在删除阅读数据…";
                    Application.DoEvents();
                    dataFailed = !DeleteData();
                }

                _status.Text = "正在删除程序文件…";
                Application.DoEvents();
                DeleteProgramFiles(installDir);

                _status.Text = "卸载完成";
                Application.DoEvents();
                string message = "云牍已卸载。";
                if (_deleteData.Checked)
                    message += dataFailed
                        ? "\r\n\r\n部分阅读数据未能删除，可手动删除：\r\n" +
                          DataFoldersText()
                        : "\r\n\r\n书架、封面和阅读位置已删除。";
                else
                    message += "\r\n\r\n书架和阅读位置已保留：\r\n" +
                        Path.Combine(Environment.GetFolderPath(
                            Environment.SpecialFolder.ApplicationData),
                            "CloudTablet");
                MessageBox.Show(this, message, "卸载云牍",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                ScheduleFolderDelete(installDir);
                Close();
            }
            catch (Exception ex)
            {
                _uninstall.Enabled = true;
                _cancel.Enabled = true;
                _status.Text = "卸载未完成";
                MessageBox.Show(this,
                    "卸载没有完成：\r\n" + ex.Message +
                    "\r\n\r\n可以稍后重新运行卸载程序。",
                    "卸载云牍", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private static string ExpectedInstallDir()
        {
            return Path.Combine(Environment.GetFolderPath(
                Environment.SpecialFolder.LocalApplicationData),
                "Programs", "CloudTablet");
        }

        private static void CloseRunningApps()
        {
            string[] names = new string[] { "云牍", "轻阅" };
            for (int n = 0; n < names.Length; n++)
            {
                Process[] processes = Process.GetProcessesByName(names[n]);
                for (int i = 0; i < processes.Length; i++)
                {
                    try { processes[i].CloseMainWindow(); }
                    catch { }
                }
                for (int i = 0; i < processes.Length; i++)
                {
                    try
                    {
                        if (!processes[i].WaitForExit(2500))
                            throw new InvalidOperationException(
                                "云牍仍在运行，请先手动退出后再卸载。");
                    }
                    catch (InvalidOperationException) { throw; }
                    catch { }
                    finally
                    {
                        try { processes[i].Dispose(); }
                        catch { }
                    }
                }
            }
        }

        private static void DeleteShortcuts()
        {
            string desktop = Environment.GetFolderPath(
                Environment.SpecialFolder.DesktopDirectory);
            string programs = Environment.GetFolderPath(
                Environment.SpecialFolder.Programs);
            DeleteFiles(desktop, new string[] {
                "云牍.lnk", "云牍.url", "轻阅.lnk", "轻阅.url"
            });
            DeleteFiles(programs, new string[] {
                "云牍.lnk", "卸载云牍.lnk", "轻阅.lnk", "卸载轻阅.lnk"
            });
        }

        private static void DeleteFiles(string dir, string[] names)
        {
            if (string.IsNullOrEmpty(dir))
                return;
            for (int i = 0; i < names.Length; i++)
            {
                try
                {
                    string path = Path.Combine(dir, names[i]);
                    if (File.Exists(path))
                        File.Delete(path);
                }
                catch
                {
                }
            }
        }

        private static void DeleteRegistryEntry()
        {
            string[] keys = new string[] {
                @"Software\Microsoft\Windows\CurrentVersion\Uninstall\CloudTablet",
                @"Software\Microsoft\Windows\CurrentVersion\Uninstall\LightReader"
            };
            for (int i = 0; i < keys.Length; i++)
            {
                try
                {
                    Registry.CurrentUser.DeleteSubKeyTree(keys[i], false);
                }
                catch
                {
                }
            }
        }

        private static bool DeleteData()
        {
            bool ok = true;
            string[] dirs = DataFolders();
            for (int i = 0; i < dirs.Length; i++)
            {
                try
                {
                    if (Directory.Exists(dirs[i]))
                        Directory.Delete(dirs[i], true);
                }
                catch
                {
                    ok = false;
                }
            }
            return ok;
        }

        private static string[] DataFolders()
        {
            string appData = Environment.GetFolderPath(
                Environment.SpecialFolder.ApplicationData);
            return new string[] {
                Path.Combine(appData, "CloudTablet"),
                Path.Combine(appData, "LightReader")
            };
        }

        private static string DataFoldersText()
        {
            return string.Join("\r\n", DataFolders());
        }

        private static void DeleteProgramFiles(string installDir)
        {
            string self = Path.GetFullPath(Application.ExecutablePath);
            try
            {
                string[] files = Directory.GetFiles(installDir);
                for (int i = 0; i < files.Length; i++)
                {
                    if (string.Equals(Path.GetFullPath(files[i]), self,
                        StringComparison.OrdinalIgnoreCase))
                        continue;
                    try { File.Delete(files[i]); }
                    catch { }
                }
            }
            catch
            {
            }
        }

        private static void ScheduleFolderDelete(string installDir)
        {
            try
            {
                string comspec = Environment.GetEnvironmentVariable("ComSpec");
                if (string.IsNullOrEmpty(comspec))
                    comspec = "cmd.exe";
                string arguments = "/c ping 127.0.0.1 -n 3 >nul & rd /s /q \"" +
                    installDir + "\"";
                ProcessStartInfo psi = new ProcessStartInfo(comspec, arguments);
                psi.CreateNoWindow = true;
                psi.UseShellExecute = false;
                psi.WindowStyle = ProcessWindowStyle.Hidden;
                psi.WorkingDirectory = Path.GetTempPath();
                Process.Start(psi);
            }
            catch
            {
            }
        }
    }
}
