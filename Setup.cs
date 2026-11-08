using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using Microsoft.Win32;
using Timer = System.Windows.Forms.Timer;

namespace CloudTabletSetup
{
    internal static class Program
    {
        [STAThread]
        private static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new SetupForm());
        }
    }

    internal sealed class SetupForm : Form
    {
        private readonly Label _status;
        private readonly Label _detail;
        private readonly AccentBar _progress;
        private readonly Timer _timer;
        private readonly float _scale;
        private string _installDir;
        private string _target;
        private string _uninstaller;
        private bool _finished;
        private bool _failed;

        public SetupForm()
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
            Text = "云牍 CloudTablet · 一键安装";
            AutoScaleMode = AutoScaleMode.None;
            ClientSize = new Size(520, 292);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition = FormStartPosition.CenterScreen;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = true;
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

            PictureBox icon = new PictureBox();
            icon.Bounds = new Rectangle(24, 24, 52, 52);
            icon.SizeMode = PictureBoxSizeMode.StretchImage;
            icon.BackColor = Color.FromArgb(87, 117, 127);
            try
            {
                using (Stream stream = Assembly.GetExecutingAssembly()
                    .GetManifestResourceStream("CloudTablet.logo.png"))
                {
                    if (stream != null)
                        icon.Image = new Bitmap(stream);
                }
            }
            catch
            {
            }
            if (icon.Image == null)
            {
                try
                {
                    using (Icon ico = Icon.ExtractAssociatedIcon(
                        Application.ExecutablePath))
                    using (Icon big = new Icon(ico, 128, 128))
                        icon.Image = big.ToBitmap();
                }
                catch
                {
                }
            }
            Controls.Add(icon);

            Label title = new Label();
            title.Text = "云牍";
            title.Font = new Font("Microsoft YaHei UI", 17f, FontStyle.Bold,
                GraphicsUnit.Point);
            title.ForeColor = Color.FromArgb(38, 58, 62);
            title.Bounds = new Rectangle(94, 20, 200, 36);
            Controls.Add(title);

            Label english = new Label();
            english.Text = "CloudTablet   ·   极轻量小说阅读器";
            english.ForeColor = Color.FromArgb(105, 124, 126);
            english.Bounds = new Rectangle(96, 54, 390, 22);
            Controls.Add(english);

            Label version = new Label();
            version.Text = "版本 1.2   ·   当前用户安装，无需管理员权限";
            version.ForeColor = Color.FromArgb(105, 124, 126);
            version.Bounds = new Rectangle(96, 76, 390, 22);
            Controls.Add(version);

            _status = new Label();
            _status.Text = "准备安装…";
            _status.ForeColor = Color.FromArgb(38, 58, 62);
            _status.Bounds = new Rectangle(24, 112, 470, 24);
            Controls.Add(_status);

            _progress = new AccentBar();
            _progress.Value = 0;
            _progress.Bounds = new Rectangle(24, 140, 470, 14);
            Controls.Add(_progress);

            _detail = new Label();
            _detail.Text = "安装位置：当前用户程序目录";
            _detail.ForeColor = Color.FromArgb(105, 124, 126);
            _detail.AutoEllipsis = true;
            _detail.Bounds = new Rectangle(24, 164, 470, 38);
            Controls.Add(_detail);

            Label hint = new Label();
            hint.Text = "安装完成后会自动创建桌面快捷方式和卸载入口，并启动云牍。";
            hint.ForeColor = Color.FromArgb(105, 124, 126);
            hint.Bounds = new Rectangle(24, 206, 470, 22);
            Controls.Add(hint);

            Label developer = new Label();
            developer.Text = "开发者：@玉衡景行   ·   使用 DeepSeek V4.1 开发";
            developer.ForeColor = Color.FromArgb(105, 124, 126);
            developer.Bounds = new Rectangle(24, 232, 470, 22);
            Controls.Add(developer);

            LinkLabel github = new LinkLabel();
            github.Text = "github.com/Alioth-MuCanoe";
            github.LinkBehavior = LinkBehavior.HoverUnderline;
            github.LinkColor = Color.FromArgb(63, 110, 115);
            github.ActiveLinkColor = Color.FromArgb(45, 85, 90);
            github.Bounds = new Rectangle(24, 254, 300, 22);
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

            _timer = new Timer();
            _timer.Interval = 180;
            _timer.Tick += delegate
            {
                _timer.Stop();
                Install();
            };
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

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            _timer.Start();
        }

        private void Install()
        {
            try
            {
                _installDir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "Programs", "CloudTablet");
                _target = Path.Combine(_installDir, "云牍.exe");
                _uninstaller = Path.Combine(_installDir, "卸载云牍.exe");
                Directory.CreateDirectory(_installDir);

                SetStatus("正在关闭已运行的云牍…", 8);
                CloseRunningApps();
                Thread.Sleep(100);

                SetStatus("正在安装程序文件…", 35);
                WriteResource("CloudTablet.App.exe", _target);
                WriteResource("CloudTablet.Uninstall.exe", _uninstaller);
                WriteResource("CloudTablet.Readme.md",
                    Path.Combine(_installDir, "使用说明.md"));
                _progress.Value = 65;

                SetStatus("正在创建桌面快捷方式…", 75);
                string desktop = Environment.GetFolderPath(
                    Environment.SpecialFolder.DesktopDirectory);
                bool desktopOk = false;
                if (!string.IsNullOrEmpty(desktop))
                {
                    Directory.CreateDirectory(desktop);
                    desktopOk = CreateShortcut(Path.Combine(desktop, "云牍.lnk"),
                        _target, _installDir, "云牍 · 小说阅读器");
                }

                SetStatus("正在创建开始菜单和卸载入口…", 88);
                string programs = Environment.GetFolderPath(
                    Environment.SpecialFolder.Programs);
                if (!string.IsNullOrEmpty(programs))
                {
                    Directory.CreateDirectory(programs);
                    CreateShortcut(Path.Combine(programs, "云牍.lnk"),
                        _target, _installDir, "云牍 · 小说阅读器");
                    CreateShortcut(Path.Combine(programs, "卸载云牍.lnk"),
                        _uninstaller, _installDir, "卸载云牍");
                }

                WriteUninstallEntry();
                RemoveOldVersion();

                _progress.Value = 100;
                SetStatus("安装完成，正在启动…", 100);
                _detail.Text = "已安装到：" + _installDir;
                try
                {
                    ProcessStartInfo psi = new ProcessStartInfo(_target);
                    psi.WorkingDirectory = _installDir;
                    psi.UseShellExecute = true;
                    Process.Start(psi);
                }
                catch (Exception ex)
                {
                    MessageBox.Show(this,
                        "程序已安装，但启动失败：\r\n" + ex.Message +
                        "\r\n\r\n可手动打开：\r\n" + _target,
                        "云牍", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }

                if (!desktopOk)
                {
                    MessageBox.Show(this,
                        "程序已安装，但桌面快捷方式没有创建成功。\r\n" +
                        "可以手动把安装目录里的“云牍.exe”发送到桌面。\r\n\r\n" +
                        "安装目录：\r\n" + _installDir,
                        "云牍", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }

                _finished = true;
                Timer close = new Timer();
                close.Interval = 1100;
                close.Tick += delegate
                {
                    close.Stop();
                    close.Dispose();
                    Close();
                };
                _timer.Stop();
                _timer.Dispose();
                close.Start();
            }
            catch (Exception ex)
            {
                _failed = true;
                _progress.Value = 0;
                _status.Text = "安装失败";
                _status.ForeColor = Color.FromArgb(180, 60, 40);
                _detail.Text = ex.Message;
                MessageBox.Show(this,
                    "安装失败：\r\n" + ex.Message +
                    "\r\n\r\n如果云牍正在运行，请先关闭它再重试。",
                    "云牍 · 一键安装", MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (!_finished && !_failed)
            {
                if (_progress.Value > 0 && _progress.Value < 100)
                    e.Cancel = true;
            }
            base.OnFormClosing(e);
        }

        private void SetStatus(string text, int value)
        {
            _status.Text = text;
            if (value >= 0 && value <= 100)
                _progress.Value = value;
            _status.Refresh();
            _progress.Refresh();
            Application.DoEvents();
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
                                "云牍正在运行，且没有在 2.5 秒内关闭。请手动退出云牍后再安装。");
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

        private static void WriteResource(string name, string target)
        {
            using (Stream stream = Assembly.GetExecutingAssembly()
                .GetManifestResourceStream(name))
            {
                if (stream == null)
                    throw new InvalidDataException("安装包缺少资源：" + name);
                using (FileStream fs = new FileStream(target, FileMode.Create,
                    FileAccess.Write, FileShare.None))
                {
                    stream.CopyTo(fs);
                }
            }
        }

        private void WriteUninstallEntry()
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.CreateSubKey(
                    @"Software\Microsoft\Windows\CurrentVersion\Uninstall\CloudTablet"))
                {
                    key.SetValue("DisplayName", "云牍 CloudTablet");
                    key.SetValue("DisplayVersion", "1.2");
                    key.SetValue("Publisher", "玉衡景行");
                    key.SetValue("DisplayIcon", _target + ",0");
                    key.SetValue("InstallLocation", _installDir);
                    key.SetValue("UninstallString", "\"" + _uninstaller + "\"");
                    key.SetValue("HelpLink", "https://github.com/Alioth-MuCanoe");
                    key.SetValue("URLInfoAbout", "https://github.com/Alioth-MuCanoe");
                    key.SetValue("NoModify", 1, RegistryValueKind.DWord);
                    key.SetValue("NoRepair", 1, RegistryValueKind.DWord);
                }
            }
            catch
            {
            }
        }

        private static void RemoveOldVersion()
        {
            try
            {
                string desktop = Environment.GetFolderPath(
                    Environment.SpecialFolder.DesktopDirectory);
                string programs = Environment.GetFolderPath(
                    Environment.SpecialFolder.Programs);
                DeleteFile(Path.Combine(desktop, "轻阅.lnk"));
                DeleteFile(Path.Combine(desktop, "轻阅.url"));
                DeleteFile(Path.Combine(programs, "轻阅.lnk"));
                DeleteFile(Path.Combine(programs, "卸载轻阅.lnk"));
                string oldDir = Path.Combine(Environment.GetFolderPath(
                    Environment.SpecialFolder.LocalApplicationData),
                    "Programs", "LightReader");
                if (Directory.Exists(oldDir))
                    Directory.Delete(oldDir, true);
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(
                    @"Software\Microsoft\Windows\CurrentVersion\Uninstall\LightReader",
                    false))
                {
                    if (key != null)
                    {
                        key.Close();
                        Registry.CurrentUser.DeleteSubKeyTree(
                            @"Software\Microsoft\Windows\CurrentVersion\Uninstall\LightReader",
                            false);
                    }
                }
            }
            catch
            {
            }
        }

        private static void DeleteFile(string path)
        {
            try
            {
                if (File.Exists(path))
                    File.Delete(path);
            }
            catch
            {
            }
        }

        private static bool CreateShortcut(string linkPath, string target,
            string workingDirectory, string description)
        {
            try
            {
                Type shellType = Type.GetTypeFromProgID("WScript.Shell");
                if (shellType == null)
                    return CreateUrlShortcut(linkPath, target);
                object shell = Activator.CreateInstance(shellType);
                object shortcut = shellType.InvokeMember("CreateShortcut",
                    BindingFlags.InvokeMethod, null, shell, new object[] { linkPath });
                Type shortcutType = shortcut.GetType();
                shortcutType.InvokeMember("TargetPath", BindingFlags.SetProperty,
                    null, shortcut, new object[] { target });
                shortcutType.InvokeMember("WorkingDirectory", BindingFlags.SetProperty,
                    null, shortcut, new object[] { workingDirectory });
                shortcutType.InvokeMember("IconLocation", BindingFlags.SetProperty,
                    null, shortcut, new object[] { target + ",0" });
                shortcutType.InvokeMember("Description", BindingFlags.SetProperty,
                    null, shortcut, new object[] { description });
                shortcutType.InvokeMember("Save", BindingFlags.InvokeMethod,
                    null, shortcut, null);
                if (shortcut != null && Marshal.IsComObject(shortcut))
                    Marshal.ReleaseComObject(shortcut);
                if (shell != null && Marshal.IsComObject(shell))
                    Marshal.ReleaseComObject(shell);
                return File.Exists(linkPath);
            }
            catch
            {
                return CreateUrlShortcut(linkPath, target);
            }
        }

        private static bool CreateUrlShortcut(string linkPath, string target)
        {
            try
            {
                string urlPath = Path.ChangeExtension(linkPath, ".url");
                string content = "[InternetShortcut]\r\nURL=" +
                    new Uri(target).AbsoluteUri + "\r\nIconFile=" + target +
                    "\r\nIconIndex=0\r\n";
                File.WriteAllText(urlPath, content, Encoding.Default);
                return File.Exists(urlPath);
            }
            catch
            {
                return false;
            }
        }
    }

    internal sealed class AccentBar : Control
    {
        private int _value;

        public AccentBar()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint |
                ControlStyles.UserPaint |
                ControlStyles.OptimizedDoubleBuffer, true);
        }

        public int Value
        {
            get { return _value; }
            set
            {
                int v = value < 0 ? 0 : (value > 100 ? 100 : value);
                if (v == _value)
                    return;
                _value = v;
                Invalidate();
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            Rectangle track = new Rectangle(0, 0, Width, Height);
            using (GraphicsPath path = Round(track, Height / 2f))
            using (SolidBrush brush = new SolidBrush(
                Color.FromArgb(0xE2, 0xD8, 0xC2)))
                e.Graphics.FillPath(brush, path);

            int w = (int)Math.Round(Width * _value / 100.0);
            if (w <= 0)
                return;
            Rectangle fill = new Rectangle(0, 0, w, Height);
            using (GraphicsPath path = Round(fill, Height / 2f))
            using (SolidBrush brush = new SolidBrush(
                Color.FromArgb(0x3F, 0x6E, 0x73)))
                e.Graphics.FillPath(brush, path);
        }

        private static GraphicsPath Round(Rectangle r, float radius)
        {
            GraphicsPath path = new GraphicsPath();
            float d = Math.Min(radius * 2f, Math.Min(r.Width, r.Height));
            if (d <= 1f)
            {
                path.AddRectangle(r);
                return path;
            }
            path.AddArc(r.Left, r.Top, d, d, 180, 90);
            path.AddArc(r.Right - d, r.Top, d, d, 270, 90);
            path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            path.AddArc(r.Left, r.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }
    }
}
