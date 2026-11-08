using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Globalization;
using System.Windows.Forms;

namespace LightReader
{
    internal static class Ui
    {
        public static Color Blend(Color a, Color b, float t)
        {
            if (t < 0) t = 0;
            if (t > 1) t = 1;
            return Color.FromArgb(
                (int)Math.Round(a.R + (b.R - a.R) * t),
                (int)Math.Round(a.G + (b.G - a.G) * t),
                (int)Math.Round(a.B + (b.B - a.B) * t));
        }

        public static Color Alpha(Color c, int alpha)
        {
            return Color.FromArgb(Math.Max(0, Math.Min(255, alpha)), c);
        }

        public static bool IsValidHtmlColor(string value)
        {
            if (string.IsNullOrEmpty(value))
                return false;
            try
            {
                Color c = ColorTranslator.FromHtml(value);
                return c.A > 0;
            }
            catch
            {
                return false;
            }
        }

        public static Color ParseColor(string value, Color fallback)
        {
            try
            {
                if (!string.IsNullOrEmpty(value))
                    return ColorTranslator.FromHtml(value);
            }
            catch
            {
            }
            return fallback;
        }

        public static string ColorToHtml(Color c)
        {
            return ColorTranslator.ToHtml(Color.FromArgb(c.R, c.G, c.B));
        }

        public static GraphicsPath RoundRect(Rectangle r, int radius)
        {
            GraphicsPath p = new GraphicsPath();
            if (r.Width <= 0 || r.Height <= 0)
                return p;
            int d = Math.Min(Math.Min(radius * 2, r.Width), r.Height);
            if (d <= 1)
            {
                p.AddRectangle(r);
                p.CloseFigure();
                return p;
            }
            p.AddArc(r.X, r.Y, d, d, 180, 90);
            p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            p.CloseFigure();
            return p;
        }

        public static void FillRound(Graphics g, Rectangle r, int radius, Color color)
        {
            if (r.Width <= 0 || r.Height <= 0)
                return;
            using (GraphicsPath path = RoundRect(r, radius))
            using (SolidBrush brush = new SolidBrush(color))
                g.FillPath(brush, path);
        }

        public static void DrawRound(Graphics g, Rectangle r, int radius, Color color, float width)
        {
            if (r.Width <= 0 || r.Height <= 0)
                return;
            using (GraphicsPath path = RoundRect(r, radius))
            using (Pen pen = new Pen(color, width))
                g.DrawPath(pen, path);
        }

        public static void Text(Graphics g, string text, Font font, Color color,
            Rectangle rect, TextFormatFlags flags)
        {
            if (text == null)
                text = "";
            TextRenderer.DrawText(g, text, font, rect, color,
                flags | TextFormatFlags.NoPrefix |
                TextFormatFlags.PreserveGraphicsClipping);
        }

        public static void TextAt(Graphics g, string text, Font font, Color color,
            int x, int y, int width, TextFormatFlags flags)
        {
            Size size = TextRenderer.MeasureText(g, text, font, new Size(width, int.MaxValue),
                flags | TextFormatFlags.NoPrefix);
            Rectangle r = new Rectangle(x, y, width, size.Height);
            Text(g, text, font, color, r, flags);
        }

        public static int MeasureWidth(string text, Font font)
        {
            if (string.IsNullOrEmpty(text))
                return 0;
            return TextRenderer.MeasureText(text, font, new Size(int.MaxValue, int.MaxValue),
                TextFormatFlags.NoPadding | TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix).Width;
        }

        public static string FormatCount(int count)
        {
            if (count >= 10000)
                return (count / 10000.0).ToString("0.#", CultureInfo.InvariantCulture) + " 万字";
            return count.ToString("N0", CultureInfo.InvariantCulture) + " 字";
        }

        public static string RelativeTime(DateTime t)
        {
            if (t == DateTime.MinValue)
                return "";
            DateTime today = DateTime.Today;
            if (t.Date == today)
                return "今天 " + t.ToString("HH:mm", CultureInfo.InvariantCulture);
            if (t.Date == today.AddDays(-1))
                return "昨天 " + t.ToString("HH:mm", CultureInfo.InvariantCulture);
            if (t.Year == today.Year)
                return t.ToString("M月d日", CultureInfo.InvariantCulture);
            return t.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        }

        public static void DrawImageCover(Graphics g, Image img, Rectangle dest, int radius)
        {
            if (img == null || dest.Width <= 0 || dest.Height <= 0)
                return;
            GraphicsState state = g.Save();
            using (GraphicsPath path = RoundRect(dest, radius))
            {
                g.SetClip(path);
                float srcRatio = img.Width / (float)img.Height;
                float dstRatio = dest.Width / (float)dest.Height;
                RectangleF src;
                if (srcRatio > dstRatio)
                {
                    float w = img.Height * dstRatio;
                    src = new RectangleF((img.Width - w) / 2f, 0, w, img.Height);
                }
                else
                {
                    float h = img.Width / dstRatio;
                    src = new RectangleF(0, (img.Height - h) / 2f, img.Width, h);
                }
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                g.DrawImage(img, dest, src, GraphicsUnit.Pixel);
            }
            g.Restore(state);
        }

        public static void DrawBookMark(Graphics g, Rectangle r, Color color, float stroke)
        {
            int cx = r.Left + r.Width / 2;
            int top = r.Top + (int)(r.Height * 0.20f);
            int bottom = r.Bottom - (int)(r.Height * 0.18f);
            int left = r.Left + (int)(r.Width * 0.14f);
            int right = r.Right - (int)(r.Width * 0.14f);
            int lift = Math.Max(1, (int)(r.Height * 0.10f));
            int gap = Math.Max(1, r.Width / 18);

            PointF[] lp = new PointF[]
            {
                new PointF(cx - gap, top + lift),
                new PointF(left, top),
                new PointF(left, bottom - lift),
                new PointF(cx - gap, bottom)
            };
            PointF[] rp = new PointF[]
            {
                new PointF(cx + gap, top + lift),
                new PointF(right, top),
                new PointF(right, bottom - lift),
                new PointF(cx + gap, bottom)
            };
            using (SolidBrush brush = new SolidBrush(color))
            {
                g.FillPolygon(brush, lp);
                g.FillPolygon(brush, rp);
            }
        }

        public static string ResolveFontFamily(string requested, bool forUi)
        {
            string[] bodyFallbacks = new string[]
            {
                "Microsoft YaHei UI", "Microsoft YaHei", "微软雅黑", "SimSun", "宋体",
                "Segoe UI", "Arial"
            };
            string[] uiFallbacks = new string[]
            {
                "Microsoft YaHei UI", "Microsoft YaHei", "微软雅黑", "Segoe UI", "SimSun", "Arial"
            };
            string[] fallbacks = forUi ? uiFallbacks : bodyFallbacks;

            List<string> installed = InstalledFamilies();
            if (!string.IsNullOrEmpty(requested))
            {
                for (int i = 0; i < installed.Count; i++)
                {
                    if (string.Equals(installed[i], requested, StringComparison.OrdinalIgnoreCase))
                        return installed[i];
                }
            }
            for (int i = 0; i < fallbacks.Length; i++)
            {
                for (int j = 0; j < installed.Count; j++)
                {
                    if (string.Equals(installed[j], fallbacks[i], StringComparison.OrdinalIgnoreCase))
                        return installed[j];
                }
            }
            if (installed.Count > 0)
                return installed[0];
            return FontFamily.GenericSansSerif.Name;
        }

        private static List<string> _families;

        public static List<string> InstalledFamilies()
        {
            if (_families != null)
                return _families;
            List<string> list = new List<string>();
            try
            {
                FontFamily[] f = FontFamily.Families;
                for (int i = 0; i < f.Length; i++)
                {
                    try
                    {
                        list.Add(f[i].Name);
                    }
                    catch
                    {
                    }
                }
            }
            catch
            {
            }
            if (list.Count == 0)
                list.Add(FontFamily.GenericSansSerif.Name);
            list.Sort(StringComparer.CurrentCultureIgnoreCase);
            _families = list;
            return list;
        }

        public static bool FontExists(string family)
        {
            if (string.IsNullOrEmpty(family))
                return false;
            List<string> list = InstalledFamilies();
            for (int i = 0; i < list.Count; i++)
            {
                if (string.Equals(list[i], family, StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            return false;
        }

        public static void FillDashedRound(Graphics g, Rectangle r, int radius,
            Color color, float width)
        {
            using (GraphicsPath path = RoundRect(r, radius))
            using (Pen pen = new Pen(color, width))
            {
                pen.DashStyle = DashStyle.Dash;
                g.DrawPath(pen, path);
            }
        }
    }
}
