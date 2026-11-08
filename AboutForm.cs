using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Windows.Forms;

namespace LightReader
{
    internal sealed class AboutForm : Form
    {
        private readonly float _scale;

        public AboutForm()
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

            Text = "关于云牍";
            AutoScaleMode = AutoScaleMode.None;
            ClientSize = new Size(S(500), S(340));
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition = FormStartPosition.CenterParent;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = false;
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
            logo.Bounds = R(30, 28, 64, 64);
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

            Label name = new Label();
            name.Text = "云牍";
            name.Font = new Font("Microsoft YaHei UI", 22f, FontStyle.Bold,
                GraphicsUnit.Point);
            name.ForeColor = Color.FromArgb(38, 58, 62);
            name.Bounds = R(112, 24, 340, 42);
            Controls.Add(name);

            Label english = new Label();
            english.Text = "CloudTablet   ·   版本 " +
                Application.ProductVersion;
            english.ForeColor = Color.FromArgb(105, 124, 126);
            english.Bounds = R(114, 66, 340, 24);
            Controls.Add(english);

            Label tagline = new Label();
            tagline.Text = "轻量、安静、本地优先的 Windows 小说阅读器";
            tagline.ForeColor = Color.FromArgb(80, 100, 102);
            tagline.Bounds = R(32, 112, 430, 24);
            Controls.Add(tagline);

            Label developer = new Label();
            developer.Text = "开发者：@玉衡景行";
            developer.ForeColor = Color.FromArgb(38, 58, 62);
            developer.Bounds = R(32, 148, 430, 24);
            Controls.Add(developer);

            Label builtWith = new Label();
            builtWith.Text = "开发工具：DeepSeek V4.1";
            builtWith.ForeColor = Color.FromArgb(38, 58, 62);
            builtWith.Bounds = R(32, 176, 430, 24);
            Controls.Add(builtWith);

            Label github = new Label();
            github.Text = "GitHub：@Alioth-MuCanoe";
            github.ForeColor = Color.FromArgb(38, 58, 62);
            github.Bounds = R(32, 204, 220, 24);
            Controls.Add(github);

            LinkLabel link = new LinkLabel();
            link.Text = "https://github.com/Alioth-MuCanoe";
            link.LinkBehavior = LinkBehavior.HoverUnderline;
            link.LinkColor = Color.FromArgb(63, 110, 115);
            link.ActiveLinkColor = Color.FromArgb(45, 85, 90);
            link.Bounds = R(32, 228, 430, 24);
            link.LinkClicked += delegate
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
            Controls.Add(link);

            Label copyright = new Label();
            copyright.Text = "Copyright © 2026 玉衡景行";
            copyright.ForeColor = Color.FromArgb(120, 136, 136);
            copyright.Bounds = R(32, 262, 300, 22);
            Controls.Add(copyright);

            Button close = new Button();
            close.Text = "关闭";
            close.Bounds = R(378, 286, 88, 34);
            close.DialogResult = DialogResult.OK;
            close.FlatStyle = FlatStyle.System;
            Controls.Add(close);
            AcceptButton = close;
            CancelButton = close;
        }

        private int S(int value)
        {
            return (int)Math.Round(value * _scale);
        }

        private Rectangle R(int x, int y, int width, int height)
        {
            return new Rectangle(S(x), S(y), S(width), S(height));
        }
    }
}
