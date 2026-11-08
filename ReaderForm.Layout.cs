using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace LightReader
{
    internal sealed class WelcomeLayout
    {
        public Rectangle Column;
        public Rectangle Logo;
        public Rectangle Title;
        public Rectangle Tagline;
        public Rectangle Primary;
        public Rectangle Secondary;
        public Rectangle RecentHeader;
        public Rectangle RecentCard;
        public Rectangle DropZone;
        public Rectangle Night;
        public Rectangle Open;
        public Rectangle BottomHint;
        public List<Rectangle> RecentRows = new List<Rectangle>();
        public List<BookMark> Recents = new List<BookMark>();
        public bool ShowSecondary;
        public bool ShowRecent;
        public bool ShowDrop;
    }

    internal sealed class SettingsLayout
    {
        public Rectangle Panel;
        public Rectangle Header;
        public Rectangle Close;
        public Rectangle FontLabel;
        public Rectangle FontValue;
        public Rectangle FontButton;
        public Rectangle SizeLabel;
        public Rectangle SizeMinus;
        public Rectangle SizeValue;
        public Rectangle SizePlus;
        public Rectangle SpacingLabel;
        public Rectangle[] Spacing = new Rectangle[3];
        public Rectangle BrightnessLabel;
        public Rectangle BrightnessValue;
        public Rectangle BrightnessTrack;
        public Rectangle ThemeLabel;
        public Rectangle[] Themes = new Rectangle[6];
        public string[] ThemeIds = new string[6];
        public Rectangle CustomColor;
        public Rectangle BackgroundImage;
        public Rectangle ClearImage;
        public Rectangle Divider1;
        public Rectangle Divider2;
        public Rectangle Divider3;
        public Rectangle Divider4;
        public Rectangle Info1;
        public Rectangle Info2;
        public Rectangle Reset;
        public Rectangle About;
        public Rectangle Scrollbar;
        public int ContentHeight;
        public int MaxScroll;
    }

    internal sealed class TocLayout
    {
        public Rectangle Panel;
        public Rectangle Header;
        public Rectangle Close;
        public Rectangle List;
        public int RowHeight;
        public int MaxScroll;
    }

    internal sealed partial class ReaderForm
    {
        private WelcomeLayout GetWelcomeLayout()
        {
            WelcomeLayout l = new WelcomeLayout();
            int w = ClientSize.Width;
            int h = ClientSize.Height;
            int cw = Math.Min(S(680), w - S(48));
            if (cw < S(300))
                cw = Math.Max(S(200), w - S(24));
            int cx = (w - cw) / 2;
            l.Column = new Rectangle(cx, 0, cw, h);
            l.Night = new Rectangle(w - S(100), S(18), S(82), S(34));
            l.Open = new Rectangle(w - S(190), S(18), S(82), S(34));

            l.Recents = GetVisibleRecents();
            int maxRows = Math.Max(0, (h - S(390)) / S(62));
            if (maxRows > 5) maxRows = 5;
            if (maxRows > l.Recents.Count) maxRows = l.Recents.Count;
            bool showRecent = maxRows > 0;
            bool showSecondary = h >= S(500) && l.Recents.Count > 0;
            bool showDrop = l.Recents.Count == 0 && h >= S(560);
            int rows = showRecent ? maxRows : 0;

            int logoSize = S(58);
            int total = logoSize + S(16) + S(46) + S(2) + S(24) + S(26) + S(52);
            if (showSecondary) total += S(10) + S(32);
            if (showRecent) total += S(28) + S(24) + rows * S(62);
            if (showDrop) total += S(24) + S(84);
            total += S(34);
            int y = (h - total) / 2;
            if (y < S(52)) y = S(52);
            if (h - y < total) y = Math.Max(S(20), h - total - S(12));

            l.Logo = new Rectangle(cx + (cw - logoSize) / 2, y, logoSize, logoSize);
            y += logoSize + S(16);
            l.Title = new Rectangle(cx, y, cw, S(46));
            y += S(46) + S(2);
            l.Tagline = new Rectangle(cx, y, cw, S(24));
            y += S(24) + S(26);

            int bw = Math.Min(S(250), cw - S(20));
            l.Primary = new Rectangle(cx + (cw - bw) / 2, y, bw, S(52));
            y += S(52);
            if (showSecondary)
            {
                y += S(10);
                l.Secondary = new Rectangle(cx + (cw - S(220)) / 2, y, S(220), S(32));
                y += S(32);
            }
            l.ShowSecondary = showSecondary;
            l.ShowRecent = showRecent;
            l.ShowDrop = showDrop;

            if (showRecent)
            {
                y += S(28);
                l.RecentHeader = new Rectangle(cx, y, cw, S(24));
                y += S(24);
                l.RecentCard = new Rectangle(cx, y, cw, rows * S(62));
                for (int i = 0; i < rows; i++)
                {
                    Rectangle row = new Rectangle(cx, y + i * S(62), cw, S(62));
                    l.RecentRows.Add(row);
                }
                y += rows * S(62);
            }
            if (showDrop)
            {
                y += S(24);
                l.DropZone = new Rectangle(cx + S(30), y, cw - S(60), S(84));
            }
            l.BottomHint = new Rectangle(cx, h - S(32), cw, S(20));
            return l;
        }

        private SettingsLayout GetSettingsLayout()
        {
            SettingsLayout l = new SettingsLayout();
            int w = ClientSize.Width;
            int h = ClientSize.Height;
            int pw = Math.Min(S(380), w - S(20));
            int px = w - pw;
            l.Panel = new Rectangle(px, 0, pw, h);

            int pad = S(22);
            int cx = px + pad;
            int cw = pw - pad * 2;
            l.Header = new Rectangle(px, 0, pw, S(58));
            l.Close = new Rectangle(px + pw - S(46), S(15), S(32), S(32));

            int y = S(65);
            l.FontLabel = new Rectangle(cx, y, cw, S(17));
            y += S(20);
            l.FontValue = new Rectangle(cx, y, cw - S(82), S(34));
            l.FontButton = new Rectangle(cx + cw - S(76), y, S(76), S(34));
            y += S(44);

            l.Divider1 = new Rectangle(cx, y, cw, 1);
            y += S(10);

            l.SizeLabel = new Rectangle(cx, y, S(140), S(17));
            l.SpacingLabel = new Rectangle(cx + S(156), y, cw - S(156), S(17));
            y += S(20);
            l.SizeMinus = new Rectangle(cx, y, S(40), S(34));
            l.SizeValue = new Rectangle(cx + S(46), y, S(54), S(34));
            l.SizePlus = new Rectangle(cx + S(106), y, S(40), S(34));
            int spacingX = cx + S(156);
            int spacingTotal = cw - S(156);
            int gap = S(4);
            int sw = Math.Max(S(42), (spacingTotal - gap * 2) / 3);
            for (int i = 0; i < 3; i++)
                l.Spacing[i] = new Rectangle(spacingX + i * (sw + gap), y, sw, S(34));
            y += S(46);

            l.Divider2 = new Rectangle(cx, y, cw, 1);
            y += S(10);

            l.BrightnessLabel = new Rectangle(cx, y, cw - S(80), S(17));
            l.BrightnessValue = new Rectangle(cx + cw - S(80), y, S(80), S(17));
            y += S(21);
            l.BrightnessTrack = new Rectangle(cx, y, cw, S(28));
            y += S(44);

            l.Divider3 = new Rectangle(cx, y, cw, 1);
            y += S(10);

            l.ThemeLabel = new Rectangle(cx, y, cw, S(17));
            y += S(20);
            int tgap = S(8);
            int tw = (cw - tgap * 2) / 3;
            int th = S(58);
            for (int i = 0; i < 6; i++)
            {
                int col = i % 3;
                int row = i / 3;
                l.Themes[i] = new Rectangle(cx + col * (tw + tgap),
                    y + row * (th + tgap), tw, th);
                l.ThemeIds[i] = ThemeCatalog.All[i].Id;
            }
            y += th * 2 + tgap + S(10);

            int half = (cw - S(8)) / 2;
            l.CustomColor = new Rectangle(cx, y, half, S(34));
            l.BackgroundImage = new Rectangle(cx + half + S(8), y,
                cw - half - S(8), S(34));
            y += S(40);
            if (!string.IsNullOrEmpty(_settings.BackgroundImage))
            {
                l.ClearImage = new Rectangle(cx, y, S(100), S(26));
                y += S(30);
            }

            l.Divider4 = new Rectangle(cx, y, cw, 1);
            y += S(10);
            l.Info1 = new Rectangle(cx, y, cw, S(18));
            y += S(20);
            l.Info2 = new Rectangle(cx, y, cw, S(18));
            y += S(34);
            l.Reset = new Rectangle(cx, y, S(130), S(28));
            l.About = new Rectangle(cx + S(146), y, S(130), S(28));
            y += S(32);
            l.ContentHeight = y;
            l.MaxScroll = Math.Max(0, y - (h - S(14)));
            if (_settingsScroll < 0) _settingsScroll = 0;
            if (_settingsScroll > l.MaxScroll) _settingsScroll = l.MaxScroll;
            int scroll = _settingsScroll;

            l.FontLabel.Y -= scroll; l.FontValue.Y -= scroll;
            l.FontButton.Y -= scroll; l.SizeLabel.Y -= scroll;
            l.SizeMinus.Y -= scroll; l.SizeValue.Y -= scroll;
            l.SizePlus.Y -= scroll; l.SpacingLabel.Y -= scroll;
            for (int i = 0; i < 3; i++) l.Spacing[i].Y -= scroll;
            l.BrightnessLabel.Y -= scroll; l.BrightnessValue.Y -= scroll;
            l.BrightnessTrack.Y -= scroll; l.ThemeLabel.Y -= scroll;
            for (int i = 0; i < 6; i++) l.Themes[i].Y -= scroll;
            l.CustomColor.Y -= scroll; l.BackgroundImage.Y -= scroll;
            l.ClearImage.Y -= scroll;
            l.Divider1.Y -= scroll; l.Divider2.Y -= scroll;
            l.Divider3.Y -= scroll; l.Divider4.Y -= scroll;
            l.Info1.Y -= scroll; l.Info2.Y -= scroll; l.Reset.Y -= scroll;
            l.About.Y -= scroll;

            if (l.MaxScroll > 0)
            {
                int trackTop = l.Header.Bottom + S(8);
                int trackH = Math.Max(S(30), h - trackTop - S(14));
                int thumbH = Math.Max(S(26),
                    (int)((long)trackH * (h - l.Header.Bottom) /
                    Math.Max(1, l.ContentHeight)));
                int maxTop = trackH - thumbH;
                int thumbTop = trackTop + (maxTop <= 0 ? 0 :
                    (int)((long)scroll * maxTop / l.MaxScroll));
                l.Scrollbar = new Rectangle(px + pw - S(7), thumbTop, S(3), thumbH);
            }
            return l;
        }

        private TocLayout GetTocLayout()
        {
            TocLayout l = new TocLayout();
            int w = ClientSize.Width;
            int h = ClientSize.Height;
            int pw = Math.Min(S(350), (int)(w * 0.82));
            if (pw < S(260)) pw = Math.Min(w - S(16), S(300));
            l.Panel = new Rectangle(0, 0, pw, h);
            l.Header = new Rectangle(0, 0, pw, S(64));
            l.Close = new Rectangle(pw - S(46), S(16), S(32), S(32));
            l.List = new Rectangle(0, S(64), pw, Math.Max(S(80), h - S(76)));
            l.RowHeight = S(52);
            int count = _book == null ? 0 : _book.Chapters.Count;
            l.MaxScroll = Math.Max(0, count * l.RowHeight - l.List.Height);
            return l;
        }

        private void ClampTocScroll(TocLayout l)
        {
            if (_tocScroll < 0) _tocScroll = 0;
            if (_tocScroll > l.MaxScroll) _tocScroll = l.MaxScroll;
        }

        private List<ToolButton> GetToolbarButtons()
        {
            List<ToolButton> list = new List<ToolButton>();
            int w = ClientSize.Width;
            int bh = S(34);
            int by = (S(56) - bh) / 2;
            int bw = S(66);
            int gap = S(4);
            int x = w - S(18) - bw;
            if (_fullscreen)
            {
                list.Add(new ToolButton("exitfull", "退出", new Rectangle(x, by, bw, bh)));
                x -= bw + gap;
            }
            else
            {
                list.Add(new ToolButton("fullscreen", "全屏", new Rectangle(x, by, bw, bh)));
                x -= bw + gap;
            }
            list.Add(new ToolButton("shelf", "书架", new Rectangle(x, by, bw, bh)));
            x -= bw + gap;
            list.Add(new ToolButton("open", "打开", new Rectangle(x, by, bw, bh)));
            x -= bw + gap;
            list.Add(new ToolButton("night", _theme.IsDark ? "日间" : "夜间",
                new Rectangle(x, by, bw, bh)));
            x -= bw + gap;
            list.Add(new ToolButton("settings", "设置", new Rectangle(x, by, bw, bh)));
            x -= bw + gap;
            list.Add(new ToolButton("toc", "目录", new Rectangle(x, by, bw, bh)));
            return list;
        }

        private Rectangle GetTopBarRect()
        {
            return new Rectangle(0, 0, ClientSize.Width, S(56));
        }

        private Rectangle GetFooterRect()
        {
            return new Rectangle(0, ClientSize.Height - S(46), ClientSize.Width, S(46));
        }

        private Rectangle GetProgressRect()
        {
            return new Rectangle(0, ClientSize.Height - S(3), ClientSize.Width, S(3));
        }

        private Rectangle GetPrevButtonRect()
        {
            return new Rectangle(S(8), ClientSize.Height - S(41), S(36), S(30));
        }

        private Rectangle GetNextButtonRect()
        {
            return new Rectangle(ClientSize.Width - S(44), ClientSize.Height - S(41),
                S(36), S(30));
        }
    }
}
