using System;
using System.Drawing;

namespace LightReader
{
    internal sealed class ReaderTheme
    {
        public readonly string Id;
        public readonly string Name;
        public readonly bool IsDark;
        public readonly Color Background;
        public readonly Color Text;
        public readonly Color Muted;
        public readonly Color Accent;
        public readonly Color AccentText;
        public readonly Color Panel;
        public readonly Color PanelText;
        public readonly Color Border;

        public ReaderTheme(string id, string name, bool dark, Color bg, Color text,
            Color muted, Color accent, Color panel, Color panelText, Color border)
        {
            Id = id;
            Name = name;
            IsDark = dark;
            Background = bg;
            Text = text;
            Muted = muted;
            Accent = accent;
            AccentText = Luminance(accent) > 0.58 ? Color.FromArgb(30, 30, 30) : Color.White;
            Panel = panel;
            PanelText = panelText;
            Border = border;
        }

        public Color Hover
        {
            get { return Ui.Blend(Background, Text, IsDark ? 0.13f : 0.065f); }
        }

        public Color Selected
        {
            get { return Ui.Blend(Background, Accent, IsDark ? 0.24f : 0.15f); }
        }

        public Color Track
        {
            get { return Ui.Blend(Background, Text, IsDark ? 0.16f : 0.11f); }
        }

        public Color Shadow
        {
            get { return Color.FromArgb(IsDark ? 150 : 55, 0, 0, 0); }
        }

        private static double Luminance(Color c)
        {
            return (0.2126 * c.R + 0.7152 * c.G + 0.0722 * c.B) / 255.0;
        }
    }

    internal static class ThemeCatalog
    {
        public const string LightDefault = "paper";
        public const string DarkDefault = "dark";

        public static readonly ReaderTheme[] All = new ReaderTheme[]
        {
            new ReaderTheme("paper", "纸白", false,
                Color.FromArgb(0xF7, 0xF0, 0xDD), Color.FromArgb(0x25, 0x3A, 0x3E),
                Color.FromArgb(0x6F, 0x83, 0x85), Color.FromArgb(0x3F, 0x6E, 0x73),
                Color.FromArgb(0xEF, 0xE5, 0xCF), Color.FromArgb(0x25, 0x3A, 0x3E),
                Color.FromArgb(0xD9, 0xCC, 0xB5)),
            new ReaderTheme("cream", "米黄", false,
                Color.FromArgb(0xF2, 0xE7, 0xCF), Color.FromArgb(0x3A, 0x34, 0x28),
                Color.FromArgb(0x82, 0x74, 0x60), Color.FromArgb(0x4B, 0x74, 0x77),
                Color.FromArgb(0xEA, 0xDC, 0xC0), Color.FromArgb(0x3A, 0x34, 0x28),
                Color.FromArgb(0xD8, 0xC7, 0xA8)),
            new ReaderTheme("green", "护眼绿", false,
                Color.FromArgb(0xDD, 0xEB, 0xE3), Color.FromArgb(0x20, 0x34, 0x2F),
                Color.FromArgb(0x5D, 0x7A, 0x70), Color.FromArgb(0x3F, 0x71, 0x6C),
                Color.FromArgb(0xCE, 0xE1, 0xD6), Color.FromArgb(0x20, 0x34, 0x2F),
                Color.FromArgb(0xB8, 0xD1, 0xC5)),
            new ReaderTheme("grey", "灰蓝", false,
                Color.FromArgb(0xE7, 0xEC, 0xEB), Color.FromArgb(0x23, 0x30, 0x33),
                Color.FromArgb(0x6E, 0x7F, 0x80), Color.FromArgb(0x46, 0x6E, 0x75),
                Color.FromArgb(0xDC, 0xE3, 0xE2), Color.FromArgb(0x23, 0x30, 0x33),
                Color.FromArgb(0xC9, 0xD3, 0xD2)),
            new ReaderTheme("dark", "深灰", true,
                Color.FromArgb(0x1B, 0x29, 0x2D), Color.FromArgb(0xD8, 0xE7, 0xE3),
                Color.FromArgb(0x83, 0x9A, 0x98), Color.FromArgb(0x8F, 0xC9, 0xC1),
                Color.FromArgb(0x25, 0x36, 0x3A), Color.FromArgb(0xDE, 0xEB, 0xE8),
                Color.FromArgb(0x3A, 0x50, 0x55)),
            new ReaderTheme("black", "纯黑", true,
                Color.FromArgb(0x00, 0x00, 0x00), Color.FromArgb(0xC4, 0xD6, 0xD2),
                Color.FromArgb(0x6E, 0x85, 0x82), Color.FromArgb(0x88, 0xC0, 0xB9),
                Color.FromArgb(0x0D, 0x16, 0x18), Color.FromArgb(0xD8, 0xE7, 0xE3),
                Color.FromArgb(0x23, 0x36, 0x39))
        };

        public static ReaderTheme Get(string id)
        {
            if (!string.IsNullOrEmpty(id))
            {
                for (int i = 0; i < All.Length; i++)
                {
                    if (string.Equals(All[i].Id, id, StringComparison.OrdinalIgnoreCase))
                        return All[i];
                }
            }
            return null;
        }

        public static ReaderTheme GetOrDefault(string id)
        {
            ReaderTheme t = Get(id);
            return t == null ? All[0] : t;
        }

        public static ReaderTheme FromCustom(Color bg)
        {
            double lum = (0.2126 * bg.R + 0.7152 * bg.G + 0.0722 * bg.B) / 255.0;
            bool dark = lum < 0.46;
            Color text = dark ? Color.FromArgb(0xD8, 0xE7, 0xE3)
                : Color.FromArgb(0x25, 0x3A, 0x3E);
            Color muted = Ui.Blend(bg, text, 0.48f);
            Color accent = dark ? Color.FromArgb(0x8F, 0xC9, 0xC1)
                : Color.FromArgb(0x3F, 0x6E, 0x73);
            Color panel = Ui.Blend(bg, text, dark ? 0.08f : 0.055f);
            Color border = Ui.Blend(bg, text, dark ? 0.20f : 0.16f);
            return new ReaderTheme("custom", "自定义", dark, bg, text, muted,
                accent, panel, text, border);
        }
    }
}
