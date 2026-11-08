using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Globalization;
using System.Windows.Forms;

namespace LightReader
{
    internal sealed partial class ReaderForm
    {
        protected override void OnPaintBackground(PaintEventArgs e)
        {
            e.Graphics.Clear(_theme.Background);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
            DrawBackdrop(g);

            if (_book == null)
                DrawShelf(g);
            else
                DrawReader(g);

            if (_loading)
                DrawLoading(g);
            else if (_importing)
                DrawImporting(g);
            else if (_overlay == Overlay.Toc)
                DrawToc(g);
            else if (_overlay == Overlay.Settings)
                DrawSettings(g);
            else if (_overlay == Overlay.ShelfMenu)
                DrawShelfMenu(g);

            if (!string.IsNullOrEmpty(_toastText) && DateTime.Now < _toastUntil)
                DrawToast(g, _toastText);
            else if (_book != null && !_loading && _overlay == Overlay.None &&
                _hintUntil != DateTime.MinValue && DateTime.Now < _hintUntil)
                DrawHint(g);

            if (_book != null && !_chromeVisible && !_loading &&
                _overlay == Overlay.None && DateTime.Now < _pageInfoUntil &&
                string.IsNullOrEmpty(_toastText))
                DrawPageInfoPill(g);
        }

        private void DrawWelcome(Graphics g)
        {
            WelcomeLayout l = GetWelcomeLayout();

            DrawGhostButton(g, l.Open, "打开", "welcome_open");
            DrawGhostButton(g, l.Night, _theme.IsDark ? "日间" : "夜间", "welcome_night");

            Ui.FillRound(g, l.Logo, S(14), _theme.Accent);
            Rectangle mark = new Rectangle(l.Logo.Left + S(14), l.Logo.Top + S(13),
                l.Logo.Width - S(28), l.Logo.Height - S(26));
            Ui.DrawBookMark(g, mark, _theme.AccentText, Math.Max(1.5f, S(2) * 0.75f));

            Ui.Text(g, "云牍", _welcomeTitle, _theme.Text, l.Title,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter |
                TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix);

            Ui.Text(g, "安静地读一本小说", _uiFont, _theme.Muted, l.Tagline,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter |
                TextFormatFlags.SingleLine);

            bool hasRecent = l.Recents.Count > 0;
            bool primaryHover = _hoverId == "welcome_primary";
            bool primaryPressed = _pressedId == "welcome_primary";
            Color primary = _theme.Accent;
            if (primaryHover) primary = Ui.Blend(primary, Color.White, 0.10f);
            if (primaryPressed) primary = Ui.Blend(primary, Color.Black, 0.10f);
            Ui.FillRound(g, l.Primary, S(12), primary);
            Ui.Text(g, hasRecent ? "继续阅读" : "打开小说", _uiBold, _theme.AccentText,
                l.Primary, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter |
                TextFormatFlags.SingleLine);

            if (l.ShowSecondary)
            {
                bool hov = _hoverId == "welcome_open";
                Color c = hov ? _theme.Text : _theme.Muted;
                Ui.Text(g, "打开其他小说", _uiFont, c, l.Secondary,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter |
                    TextFormatFlags.SingleLine);
                if (hov)
                    g.DrawLine(new Pen(c, 1f), l.Secondary.Left + S(30),
                        l.Secondary.Bottom - S(4), l.Secondary.Right - S(30),
                        l.Secondary.Bottom - S(4));
            }

            if (l.ShowRecent)
            {
                Ui.Text(g, "最近阅读", _uiSmallBold, _theme.Muted, l.RecentHeader,
                    TextFormatFlags.Left | TextFormatFlags.VerticalCenter |
                    TextFormatFlags.SingleLine);
                Ui.Text(g, "接着上次的位置", _uiSmall, _theme.Muted, l.RecentHeader,
                    TextFormatFlags.Right | TextFormatFlags.VerticalCenter |
                    TextFormatFlags.SingleLine);

                Ui.FillRound(g, l.RecentCard, S(12), _theme.Panel);
                Ui.DrawRound(g, l.RecentCard, S(12), _theme.Border, 1f);
                for (int i = 0; i < l.RecentRows.Count && i < l.Recents.Count; i++)
                {
                    Rectangle row = l.RecentRows[i];
                    BookMark b = l.Recents[i];
                    bool hov = _hoverId == "recent:" + i;
                    if (hov)
                        Ui.FillRound(g, new Rectangle(row.Left + S(2), row.Top + S(2),
                            row.Width - S(4), row.Height - S(4)), S(9), _theme.Hover);
                    if (i > 0)
                        g.DrawLine(new Pen(Ui.Alpha(_theme.Border, 150), 1f),
                            row.Left + S(16), row.Top, row.Right - S(16), row.Top);

                    Rectangle icon = new Rectangle(row.Left + S(16), row.Top + S(15),
                        S(32), S(32));
                    Ui.FillRound(g, icon, S(8), _theme.Selected);
                    Ui.DrawBookMark(g, new Rectangle(icon.Left + S(7), icon.Top + S(7),
                        icon.Width - S(14), icon.Height - S(14)), _theme.Accent,
                        Math.Max(1f, S(2) * 0.6f));

                    int textX = row.Left + S(60);
                    int rightPad = S(18);
                    int percentW = S(82);
                    Rectangle titleRect = new Rectangle(textX, row.Top + S(9),
                        row.Width - S(60) - percentW - rightPad, S(26));
                    Ui.Text(g, b.Title, _uiMedium, _theme.PanelText, titleRect,
                        TextFormatFlags.Left | TextFormatFlags.VerticalCenter |
                        TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis);

                    Rectangle pathRect = new Rectangle(textX, row.Top + S(33),
                        row.Width - S(60) - percentW - rightPad, S(20));
                    string path = b.Path;
                    Ui.Text(g, path, _uiSmall, _theme.Muted, pathRect,
                        TextFormatFlags.Left | TextFormatFlags.VerticalCenter |
                        TextFormatFlags.SingleLine | TextFormatFlags.PathEllipsis);

                    Rectangle pctRect = new Rectangle(row.Right - percentW - S(16),
                        row.Top + S(10), percentW, S(24));
                    Ui.Text(g, FormatPercent(b.Percent), _uiSmall, _theme.Accent,
                        pctRect, TextFormatFlags.Right | TextFormatFlags.VerticalCenter |
                        TextFormatFlags.SingleLine);

                    Rectangle timeRect = new Rectangle(row.Right - percentW - S(16),
                        row.Top + S(32), percentW, S(18));
                    Ui.Text(g, Ui.RelativeTime(b.LastRead), _uiSmall, _theme.Muted,
                        timeRect, TextFormatFlags.Right | TextFormatFlags.VerticalCenter |
                        TextFormatFlags.SingleLine);
                }
            }

            if (l.ShowDrop)
            {
                Ui.FillRound(g, l.DropZone, S(12), Ui.Blend(_theme.Background, _theme.Panel, 0.35f));
                Ui.FillDashedRound(g, l.DropZone, S(12), _theme.Border, 1.4f);
                Ui.Text(g, "把 TXT 小说拖到这里", _uiMedium, _theme.Text, l.DropZone,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter |
                    TextFormatFlags.SingleLine);
            }

            Ui.Text(g, "阅读位置自动保存 · 完全离线 · 无广告", _uiSmall, _theme.Muted,
                l.BottomHint, TextFormatFlags.HorizontalCenter |
                TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine);
        }

        private void DrawReader(Graphics g)
        {
            if (_book == null || _layout == null || _layout.Pages.Count == 0)
                return;
            int pi = Math.Max(0, Math.Min(_pageIndex, _layout.Pages.Count - 1));
            PageData page = _layout.Pages[pi];
            ChapterInfo chapter = _book.Chapters[_chapterIndex];
            Rectangle c = _contentRect;

            GraphicsState state = g.Save();
            g.SetClip(c);
            if (page.First && _layout.TitleBlockHeight > 0)
            {
                Rectangle titleRect = new Rectangle(c.Left, c.Top + S(2),
                    c.Width, _titleFont.Height + S(6));
                Ui.Text(g, chapter.Title, _titleFont, _theme.Text, titleRect,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter |
                    TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis |
                    TextFormatFlags.PreserveGraphicsClipping);
                int ruleY = c.Top + S(2) + _titleFont.Height + S(12);
                int ruleW = S(34);
                using (Pen pen = new Pen(_theme.Accent, Math.Max(1.5f, S(2) * 0.8f)))
                    g.DrawLine(pen, c.Left + c.Width / 2 - ruleW / 2, ruleY,
                        c.Left + c.Width / 2 + ruleW / 2, ruleY);
            }

            int bodyTop = c.Top + (page.First ? _layout.TitleBlockHeight : 0);
            bool any = false;
            for (int i = 0; i < page.Lines.Count; i++)
            {
                LineData line = page.Lines[i];
                if (line.Text.Length == 0)
                    continue;
                any = true;
                int x = c.Left + line.Indent;
                int y = bodyTop + i * _layout.LineHeight;
                Rectangle r = new Rectangle(x, y, c.Right - x + S(20),
                    _layout.LineHeight);
                Ui.Text(g, line.Text, _bodyFont, _theme.Text, r,
                    TextFormatFlags.Left | TextFormatFlags.VerticalCenter |
                    TextFormatFlags.SingleLine | TextFormatFlags.NoPadding |
                    TextFormatFlags.PreserveGraphicsClipping);
            }
            if (!any)
            {
                Rectangle empty = new Rectangle(c.Left, bodyTop + S(20), c.Width,
                    _layout.LineHeight * 2);
                Ui.Text(g, "（本章暂无内容）", _uiFont, _theme.Muted, empty,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.Top |
                    TextFormatFlags.SingleLine);
            }
            g.Restore(state);

            if (_chromeVisible)
            {
                DrawTopBar(g);
                DrawFooter(g);
            }
            else
            {
                DrawProgress(g, S(2));
            }
        }

        private void DrawTopBar(Graphics g)
        {
            if (_book == null)
                return;
            int h = S(56);
            g.FillRectangle(new SolidBrush(_theme.Background), 0, 0, ClientSize.Width, h);
            g.DrawLine(new Pen(Ui.Alpha(_theme.Border, 180), 1f), 0, h - 1,
                ClientSize.Width, h - 1);

            int logoSize = S(26);
            Rectangle logo = new Rectangle(S(20), (h - logoSize) / 2, logoSize, logoSize);
            if (_brandLogo != null)
            {
                DrawBrandLogo(g, logo, S(7));
            }
            else
            {
                Ui.FillRound(g, logo, S(7), _theme.Accent);
                Ui.DrawBookMark(g, new Rectangle(logo.Left + S(6), logo.Top + S(5),
                    logo.Width - S(12), logo.Height - S(10)), _theme.AccentText,
                    Math.Max(1.2f, S(2) * 0.6f));
            }

            int wordX = S(54);
            Rectangle wordRect = new Rectangle(wordX, 0, S(76), h);
            Ui.Text(g, "云牍", _uiTitle, _theme.Text, wordRect,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter |
                TextFormatFlags.SingleLine);

            List<ToolButton> buttons = GetToolbarButtons();
            int leftMostButton = ClientSize.Width;
            for (int i = 0; i < buttons.Count; i++)
                leftMostButton = Math.Min(leftMostButton, buttons[i].Rect.Left);
            int wordRight = wordX + Ui.MeasureWidth("云牍", _uiTitle) + S(14);
            if (wordRight + S(110) < leftMostButton)
            {
                g.DrawLine(new Pen(Ui.Alpha(_theme.Border, 210), 1f), wordRight,
                    S(20), wordRight, h - S(20));
                Rectangle titleRect = new Rectangle(wordRight + S(12), 0,
                    leftMostButton - wordRight - S(26), h);
                Ui.Text(g, _book.Title, _uiSmall, _theme.Muted, titleRect,
                    TextFormatFlags.Left | TextFormatFlags.VerticalCenter |
                    TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis);
            }

            for (int i = 0; i < buttons.Count; i++)
                DrawToolButton(g, buttons[i]);
        }

        private void DrawToolButton(Graphics g, ToolButton b)
        {
            bool hover = _hoverId == b.Id;
            bool pressed = _pressedId == b.Id;
            if (hover || pressed)
            {
                Color fill = pressed ? _theme.Selected : _theme.Hover;
                Ui.FillRound(g, b.Rect, S(7), fill);
            }
            Color text = hover ? _theme.Text : _theme.Muted;
            Ui.Text(g, b.Label, _uiFont, text, b.Rect,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter |
                TextFormatFlags.SingleLine);
        }

        private void DrawFooter(Graphics g)
        {
            if (_book == null || _layout == null)
                return;
            int h = S(46);
            int y = ClientSize.Height - h;
            g.FillRectangle(new SolidBrush(_theme.Background), 0, y, ClientSize.Width, h);
            g.DrawLine(new Pen(Ui.Alpha(_theme.Border, 180), 1f), 0, y,
                ClientSize.Width, y);

            bool atFirst = AtFirstPage;
            bool atLast = AtLastPage;
            DrawChevron(g, GetPrevButtonRect(), false,
                atFirst ? Ui.Blend(_theme.Muted, _theme.Background, 0.55f) : _theme.Muted);
            DrawChevron(g, GetNextButtonRect(), true,
                atLast ? Ui.Blend(_theme.Muted, _theme.Background, 0.55f) : _theme.Muted);

            int page = Math.Min(_pageIndex + 1, _layout.Pages.Count);
            int pages = Math.Max(1, _layout.Pages.Count);
            string info;
            if (_seeking)
                info = "跳转到 " + FormatPercent(_seekRatio);
            else
                info = string.Format("第 {0} / {1} 页   ·   {2}", page, pages,
                    FormatPercent(CurrentPercent));
            Rectangle infoRect = new Rectangle(ClientSize.Width - S(318),
                y, S(260), h);
            Ui.Text(g, info, _uiSmall, _theme.Muted, infoRect,
                TextFormatFlags.Right | TextFormatFlags.VerticalCenter |
                TextFormatFlags.SingleLine);

            if (ClientSize.Width > S(540))
            {
                Rectangle chapterRect = new Rectangle(S(52), y,
                    ClientSize.Width - S(390), h);
                Ui.Text(g, _book.Chapters[_chapterIndex].Title, _uiSmall, _theme.Muted,
                    chapterRect, TextFormatFlags.Left | TextFormatFlags.VerticalCenter |
                    TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis);
            }

            DrawProgress(g, S(3));
            if (_seeking)
            {
                int x = (int)Math.Round(ClientSize.Width * _seekRatio);
                x = Math.Max(0, Math.Min(ClientSize.Width - 1, x));
                using (Pen pen = new Pen(_theme.Accent, Math.Max(1f, S(1.5f))))
                    g.DrawLine(pen, x, y - S(8), x, ClientSize.Height);
                string pct = FormatPercent(_seekRatio);
                int tw = Ui.MeasureWidth(pct, _uiSmall) + S(18);
                Rectangle pill = new Rectangle(
                    Math.Max(S(6), Math.Min(ClientSize.Width - tw - S(6), x - tw / 2)),
                    y - S(34), tw, S(24));
                Ui.FillRound(g, pill, S(7), _theme.Panel);
                Ui.DrawRound(g, pill, S(7), _theme.Border, 1f);
                Ui.Text(g, pct, _uiSmall, _theme.Accent, pill,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter |
                    TextFormatFlags.SingleLine);
            }
        }

        private void DrawProgress(Graphics g, int height)
        {
            int y = ClientSize.Height - height;
            g.FillRectangle(new SolidBrush(_theme.Track), 0, y, ClientSize.Width, height);
            double p = _seeking ? _seekRatio : CurrentPercent;
            int fill = (int)Math.Round(ClientSize.Width * Math.Max(0, Math.Min(1, p)));
            if (fill > 0)
                g.FillRectangle(new SolidBrush(_theme.Accent), 0, y, fill, height);
        }

        private void DrawChevron(Graphics g, Rectangle r, bool right, Color color)
        {
            float cx = r.Left + r.Width / 2f;
            float cy = r.Top + r.Height / 2f;
            float w = S(5);
            float h = S(8);
            PointF[] pts;
            if (right)
            {
                pts = new PointF[]
                {
                    new PointF(cx - w / 2, cy - h),
                    new PointF(cx + w / 2, cy),
                    new PointF(cx - w / 2, cy + h)
                };
            }
            else
            {
                pts = new PointF[]
                {
                    new PointF(cx + w / 2, cy - h),
                    new PointF(cx - w / 2, cy),
                    new PointF(cx + w / 2, cy + h)
                };
            }
            using (Pen pen = new Pen(color, Math.Max(1.5f, S(2) * 0.8f)))
            {
                pen.StartCap = LineCap.Round;
                pen.EndCap = LineCap.Round;
                pen.LineJoin = LineJoin.Round;
                g.DrawLines(pen, pts);
            }
        }

        private void DrawToc(Graphics g)
        {
            TocLayout l = GetTocLayout();
            g.FillRectangle(new SolidBrush(Color.FromArgb(_theme.IsDark ? 150 : 75, 0, 0, 0)),
                ClientRectangle);
            g.FillRectangle(new SolidBrush(_theme.Panel), l.Panel);
            g.DrawLine(new Pen(_theme.Border, 1f), l.Panel.Right - 1, 0,
                l.Panel.Right - 1, ClientSize.Height);

            Ui.Text(g, "目录", _uiTitle, _theme.PanelText,
                new Rectangle(S(24), S(14), S(150), S(38)),
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter |
                TextFormatFlags.SingleLine);
            if (_book != null)
            {
                Ui.Text(g, "共 " + _book.Chapters.Count + " 章", _uiSmall, _theme.Muted,
                    new Rectangle(S(24), S(42), S(200), S(18)),
                    TextFormatFlags.Left | TextFormatFlags.Top | TextFormatFlags.SingleLine);
            }
            DrawCloseButton(g, l.Close, "toc_close");
            g.DrawLine(new Pen(_theme.Border, 1f), 0, S(64), l.Panel.Right, S(64));

            if (_book == null)
                return;

            GraphicsState state = g.Save();
            g.SetClip(l.List);
            int first = Math.Max(0, _tocScroll / l.RowHeight);
            int last = Math.Min(_book.Chapters.Count - 1,
                (_tocScroll + l.List.Height) / l.RowHeight + 1);
            for (int i = first; i <= last; i++)
            {
                int y = l.List.Top + i * l.RowHeight - _tocScroll;
                Rectangle row = new Rectangle(0, y, l.Panel.Width, l.RowHeight);
                bool current = i == _chapterIndex;
                bool hover = _hoverId == "toc:" + i;
                if (current)
                    g.FillRectangle(new SolidBrush(_theme.Selected), row);
                else if (hover)
                    g.FillRectangle(new SolidBrush(_theme.Hover), row);
                if (current)
                    g.FillRectangle(new SolidBrush(_theme.Accent), 0, row.Top,
                        S(3), row.Height);

                Ui.Text(g, (i + 1).ToString("000", CultureInfo.InvariantCulture),
                    _uiSmall, current ? _theme.Accent : _theme.Muted,
                    new Rectangle(S(16), row.Top, S(36), row.Height),
                    TextFormatFlags.Left | TextFormatFlags.VerticalCenter |
                    TextFormatFlags.SingleLine);

                Ui.Text(g, _book.Chapters[i].Title,
                    current ? _uiBold : _uiFont,
                    current ? _theme.PanelText : _theme.PanelText,
                    new Rectangle(S(56), row.Top, l.Panel.Width - S(72), row.Height),
                    TextFormatFlags.Left | TextFormatFlags.VerticalCenter |
                    TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis);
                g.DrawLine(new Pen(Ui.Alpha(_theme.Border, 130), 1f),
                    S(16), row.Bottom - 1, l.Panel.Right - S(16), row.Bottom - 1);
            }
            g.Restore(state);

            DrawScrollbar(g, l);
        }

        private void DrawScrollbar(Graphics g, TocLayout l)
        {
            if (l.MaxScroll <= 0 || _book == null)
                return;
            int trackTop = l.List.Top + S(4);
            int trackH = l.List.Height - S(8);
            int total = _book.Chapters.Count * l.RowHeight;
            int thumbH = Math.Max(S(28), (int)((long)trackH * l.List.Height / total));
            int maxTop = trackH - thumbH;
            int thumbTop = trackTop + (maxTop <= 0 ? 0 :
                (int)((long)(l.MaxScroll - _tocScroll) * maxTop / l.MaxScroll));
            Rectangle thumb = new Rectangle(l.Panel.Right - S(7), thumbTop, S(3), thumbH);
            Ui.FillRound(g, thumb, S(2), Ui.Alpha(_theme.Muted, 120));
        }

        private void DrawSettings(Graphics g)
        {
            SettingsLayout l = GetSettingsLayout();
            g.FillRectangle(new SolidBrush(Color.FromArgb(_theme.IsDark ? 150 : 75, 0, 0, 0)),
                ClientRectangle);
            g.FillRectangle(new SolidBrush(_theme.Panel), l.Panel);
            g.DrawLine(new Pen(_theme.Border, 1f), l.Panel.Left, 0, l.Panel.Left,
                ClientSize.Height);

            Ui.Text(g, "阅读设置", _uiTitle, _theme.PanelText,
                new Rectangle(l.Panel.Left + S(22), S(14), S(200), S(36)),
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter |
                TextFormatFlags.SingleLine);
            Ui.Text(g, "更改会立即保存", _uiSmall, _theme.Muted,
                new Rectangle(l.Panel.Left + S(22), S(41), S(200), S(16)),
                TextFormatFlags.Left | TextFormatFlags.Top | TextFormatFlags.SingleLine);
            DrawCloseButton(g, l.Close, "settings_close");

            GraphicsState state = g.Save();
            g.SetClip(new Rectangle(l.Panel.Left, l.Header.Bottom,
                l.Panel.Width, Math.Max(0, l.Panel.Height - l.Header.Bottom)));

            Ui.Text(g, "字体", _uiSmallBold, _theme.Muted, l.FontLabel,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter |
                TextFormatFlags.SingleLine);
            Ui.Text(g, _settings.FontFamily, _uiMedium, _theme.PanelText, l.FontValue,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter |
                TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis);
            DrawSmallButton(g, l.FontButton, "更换", "font_choose", false);

            Ui.Text(g, "字号", _uiSmallBold, _theme.Muted, l.SizeLabel,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter |
                TextFormatFlags.SingleLine);
            Ui.Text(g, "行距", _uiSmallBold, _theme.Muted, l.SpacingLabel,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter |
                TextFormatFlags.SingleLine);

            DrawSmallButton(g, l.SizeMinus, "A−", "size_minus", false);
            DrawSmallButton(g, l.SizePlus, "A+", "size_plus", false);
            Ui.FillRound(g, l.SizeValue, S(7), _theme.Background);
            Ui.DrawRound(g, l.SizeValue, S(7), _theme.Border, 1f);
            Ui.Text(g, _settings.FontSize.ToString("0.#", CultureInfo.InvariantCulture),
                _uiMedium, _theme.PanelText, l.SizeValue,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter |
                TextFormatFlags.SingleLine);

            string[] labels = new string[] { "紧凑", "标准", "宽松" };
            float[] values = new float[] { 1.45f, 1.7f, 1.95f };
            for (int i = 0; i < 3; i++)
            {
                bool active = Math.Abs(_settings.LineSpacing - values[i]) < 0.06f;
                DrawSmallButton(g, l.Spacing[i], labels[i], "spacing:" + i, active);
            }

            Ui.Text(g, "亮度", _uiSmallBold, _theme.Muted, l.BrightnessLabel,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter |
                TextFormatFlags.SingleLine);
            Ui.Text(g, _brightness + "%", _uiSmall, _theme.Muted, l.BrightnessValue,
                TextFormatFlags.Right | TextFormatFlags.VerticalCenter |
                TextFormatFlags.SingleLine);
            DrawBrightnessSlider(g, l.BrightnessTrack);

            Ui.Text(g, "阅读背景", _uiSmallBold, _theme.Muted, l.ThemeLabel,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter |
                TextFormatFlags.SingleLine);
            for (int i = 0; i < 6; i++)
            {
                ReaderTheme t = ThemeCatalog.All[i];
                DrawThemeSwatch(g, l.Themes[i], t, _theme.Id == t.Id,
                    _hoverId == "theme:" + t.Id);
            }
            DrawCustomColorButton(g, l.CustomColor);
            DrawBackgroundImageButton(g, l.BackgroundImage);
            if (l.ClearImage.Width > 0)
            {
                bool hov = _hoverId == "bg_clear";
                Ui.Text(g, "清除图片", _uiSmall, hov ? _theme.Accent : _theme.Muted,
                    l.ClearImage, TextFormatFlags.Left | TextFormatFlags.VerticalCenter |
                    TextFormatFlags.SingleLine);
                if (hov)
                {
                    using (Pen pen = new Pen(_theme.Accent, 1f))
                        g.DrawLine(pen, l.ClearImage.Left, l.ClearImage.Bottom - S(4),
                            l.ClearImage.Left + Ui.MeasureWidth("清除图片", _uiSmall),
                            l.ClearImage.Bottom - S(4));
                }
            }

            g.DrawLine(new Pen(_theme.Border, 1f), l.Divider1.Left, l.Divider1.Top,
                l.Divider1.Right, l.Divider1.Top);
            g.DrawLine(new Pen(_theme.Border, 1f), l.Divider2.Left, l.Divider2.Top,
                l.Divider2.Right, l.Divider2.Top);
            g.DrawLine(new Pen(_theme.Border, 1f), l.Divider3.Left, l.Divider3.Top,
                l.Divider3.Right, l.Divider3.Top);
            g.DrawLine(new Pen(_theme.Border, 1f), l.Divider4.Left, l.Divider4.Top,
                l.Divider4.Right, l.Divider4.Top);

            if (_book != null)
            {
                string info = "编码 " + _book.EncodingName + "  ·  " +
                    Ui.FormatCount(_book.Text.Length) + "  ·  " +
                    _book.Chapters.Count + " 章";
                Ui.Text(g, info, _uiSmall, _theme.Muted, l.Info1,
                    TextFormatFlags.Left | TextFormatFlags.VerticalCenter |
                    TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis);
            }
            Ui.Text(g, "← → 翻页 · 空格下一页 · F11 全屏 · Ctrl+O 打开",
                _uiSmall, _theme.Muted, l.Info2,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter |
                TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis);

            bool resetHover = _hoverId == "reset_typo";
            Ui.Text(g, "恢复默认排版", _uiSmall, resetHover ? _theme.Accent : _theme.Muted,
                l.Reset, TextFormatFlags.Left | TextFormatFlags.VerticalCenter |
                TextFormatFlags.SingleLine);
            if (resetHover)
            {
                using (Pen pen = new Pen(_theme.Accent, 1f))
                    g.DrawLine(pen, l.Reset.Left, l.Reset.Bottom - S(5),
                        l.Reset.Left + Ui.MeasureWidth("恢复默认排版", _uiSmall),
                        l.Reset.Bottom - S(5));
            }
            bool aboutHover = _hoverId == "about";
            Ui.Text(g, "关于云牍", _uiSmall, aboutHover ? _theme.Accent : _theme.Muted,
                l.About, TextFormatFlags.Left | TextFormatFlags.VerticalCenter |
                TextFormatFlags.SingleLine);
            if (aboutHover)
            {
                using (Pen pen = new Pen(_theme.Accent, 1f))
                    g.DrawLine(pen, l.About.Left, l.About.Bottom - S(5),
                        l.About.Left + Ui.MeasureWidth("关于云牍", _uiSmall),
                        l.About.Bottom - S(5));
            }
            if (l.Scrollbar.Width > 0)
                Ui.FillRound(g, l.Scrollbar, S(2), Ui.Alpha(_theme.Muted, 130));
            g.Restore(state);
        }

        private void DrawBrandLogo(Graphics g, Rectangle r, int radius)
        {
            if (_brandLogo == null)
                return;
            GraphicsState state = g.Save();
            using (GraphicsPath path = Ui.RoundRect(r, radius))
            {
                g.SetClip(path, CombineMode.Intersect);
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                g.DrawImage(_brandLogo, r);
            }
            g.Restore(state);
        }

        private void DrawBrightnessSlider(Graphics g, Rectangle r)
        {
            int y = r.Top + r.Height / 2;
            Rectangle track = new Rectangle(r.Left + S(6), y - S(3),
                r.Width - S(12), S(6));
            Ui.FillRound(g, track, S(3), _theme.Track);
            double t = (_brightness - 20) / 80.0;
            int fill = (int)Math.Round(track.Width * t);
            if (fill > 0)
                Ui.FillRound(g, new Rectangle(track.Left, track.Top, fill, track.Height),
                    S(3), _theme.Accent);
            int knobX = track.Left + fill;
            int kr = S(8);
            using (SolidBrush b1 = new SolidBrush(_theme.Panel))
                g.FillEllipse(b1, knobX - kr, y - kr, kr * 2, kr * 2);
            using (SolidBrush b2 = new SolidBrush(_theme.Accent))
                g.FillEllipse(b2, knobX - kr + S(2), y - kr + S(2),
                    (kr - S(2)) * 2, (kr - S(2)) * 2);
        }

        private void DrawCustomColorButton(Graphics g, Rectangle r)
        {
            bool active = _theme.Id == "custom";
            bool hover = _hoverId == "custom_color";
            Color fill = active ? _theme.Selected : (hover ? _theme.Hover : _theme.Background);
            Ui.FillRound(g, r, S(7), fill);
            Ui.DrawRound(g, r, S(7), active ? _theme.Accent : _theme.Border,
                active ? Math.Max(1.5f, S(2) * 0.8f) : 1f);
            Color custom = Ui.ParseColor(_settings.CustomColor,
                Color.FromArgb(0xF7, 0xF0, 0xDD));
            int d = S(18);
            Rectangle dot = new Rectangle(r.Left + S(10), r.Top + (r.Height - d) / 2,
                d, d);
            using (SolidBrush b = new SolidBrush(custom))
                g.FillEllipse(b, dot);
            Ui.Text(g, "自定义颜色", _uiSmall, _theme.PanelText,
                new Rectangle(dot.Right + S(8), r.Top, r.Right - dot.Right - S(14),
                    r.Height),
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter |
                TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis);
        }

        private void DrawBackgroundImageButton(Graphics g, Rectangle r)
        {
            bool hover = _hoverId == "bg_image";
            Ui.FillRound(g, r, S(7), hover ? _theme.Hover : _theme.Background);
            Ui.DrawRound(g, r, S(7), _theme.Border, 1f);
            int d = S(22);
            Rectangle thumb = new Rectangle(r.Left + S(8), r.Top + (r.Height - d) / 2,
                d, d);
            if (_backgroundBitmap != null)
                Ui.DrawImageCover(g, _backgroundBitmap, thumb, S(4));
            else
            {
                Ui.FillRound(g, thumb, S(4), _theme.Track);
                Ui.Text(g, "图", _uiSmall, _theme.Muted, thumb,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter |
                    TextFormatFlags.SingleLine);
            }
            Ui.Text(g, string.IsNullOrEmpty(_settings.BackgroundImage) ?
                "背景图片" : "更换图片", _uiSmall, _theme.PanelText,
                new Rectangle(thumb.Right + S(8), r.Top,
                    r.Right - thumb.Right - S(14), r.Height),
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter |
                TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis);
        }

        private void DrawThemeSwatch(Graphics g, Rectangle r, ReaderTheme t,
            bool selected, bool hover)
        {
            Ui.FillRound(g, r, S(8), t.Background);
            Color border = selected ? _theme.Accent :
                (hover ? Ui.Blend(t.Background, t.Text, 0.45f) : t.Border);
            Ui.DrawRound(g, r, S(8), border, selected ? Math.Max(1.5f, S(2) * 0.8f) : 1f);
            Ui.Text(g, "文", _uiMedium, t.Text,
                new Rectangle(r.Left, r.Top + S(4), r.Width, r.Height - S(18)),
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter |
                TextFormatFlags.SingleLine);
            Ui.Text(g, t.Name, _uiSmall, selected ? t.Accent : t.Muted,
                new Rectangle(r.Left, r.Bottom - S(18), r.Width, S(15)),
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter |
                TextFormatFlags.SingleLine);
            if (selected)
            {
                int d = S(9);
                Rectangle dot = new Rectangle(r.Right - d - S(6), r.Top + S(6), d, d);
                g.FillEllipse(new SolidBrush(_theme.Accent), dot);
                using (Pen pen = new Pen(t.Background, Math.Max(1f, S(1.2f))))
                {
                    pen.StartCap = LineCap.Round;
                    pen.EndCap = LineCap.Round;
                    g.DrawLine(pen, dot.Left + d * 0.24f, dot.Top + d * 0.52f,
                        dot.Left + d * 0.44f, dot.Top + d * 0.72f);
                    g.DrawLine(pen, dot.Left + d * 0.44f, dot.Top + d * 0.72f,
                        dot.Left + d * 0.76f, dot.Top + d * 0.30f);
                }
            }
        }

        private void DrawSmallButton(Graphics g, Rectangle r, string label, string id,
            bool active)
        {
            bool hover = _hoverId == id;
            bool pressed = _pressedId == id;
            Color fill;
            Color text;
            if (active)
            {
                fill = pressed ? Ui.Blend(_theme.Accent, Color.Black, 0.10f) : _theme.Accent;
                text = _theme.AccentText;
            }
            else
            {
                fill = pressed ? _theme.Selected : (hover ? _theme.Hover : _theme.Background);
                text = _theme.PanelText;
            }
            Ui.FillRound(g, r, S(7), fill);
            if (!active)
                Ui.DrawRound(g, r, S(7), _theme.Border, 1f);
            Ui.Text(g, label, _uiFont, text, r,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter |
                TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis);
        }

        private void DrawGhostButton(Graphics g, Rectangle r, string label, string id)
        {
            bool hover = _hoverId == id;
            bool pressed = _pressedId == id;
            if (hover || pressed)
                Ui.FillRound(g, r, S(7), pressed ? _theme.Selected : _theme.Hover);
            Ui.DrawRound(g, r, S(7), _theme.Border, 1f);
            Ui.Text(g, label, _uiSmall, _theme.Muted, r,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter |
                TextFormatFlags.SingleLine);
        }

        private void DrawCloseButton(Graphics g, Rectangle r, string id)
        {
            bool hover = _hoverId == id;
            bool pressed = _pressedId == id;
            if (hover || pressed)
                Ui.FillRound(g, r, S(7), pressed ? _theme.Selected : _theme.Hover);
            Color c = hover ? _theme.Text : _theme.Muted;
            float pad = r.Width * 0.32f;
            using (Pen pen = new Pen(c, Math.Max(1.2f, S(1.8f))))
            {
                pen.StartCap = LineCap.Round;
                pen.EndCap = LineCap.Round;
                g.DrawLine(pen, r.Left + pad, r.Top + pad, r.Right - pad, r.Bottom - pad);
                g.DrawLine(pen, r.Right - pad, r.Top + pad, r.Left + pad, r.Bottom - pad);
            }
        }

        private void DrawLoading(Graphics g)
        {
            g.FillRectangle(new SolidBrush(Color.FromArgb(_theme.IsDark ? 150 : 95, 0, 0, 0)),
                ClientRectangle);
            int w = S(330);
            int h = S(112);
            Rectangle panel = new Rectangle((ClientSize.Width - w) / 2,
                (ClientSize.Height - h) / 2, w, h);
            Ui.FillRound(g, panel, S(14), _theme.Panel);
            Ui.DrawRound(g, panel, S(14), _theme.Border, 1f);
            Ui.Text(g, "正在打开", _uiBold, _theme.PanelText,
                new Rectangle(panel.Left + S(24), panel.Top + S(20),
                    panel.Width - S(48), S(24)),
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter |
                TextFormatFlags.SingleLine);
            Ui.Text(g, _loadingName, _uiSmall, _theme.Muted,
                new Rectangle(panel.Left + S(24), panel.Top + S(48),
                    panel.Width - S(48), S(20)),
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter |
                TextFormatFlags.SingleLine | TextFormatFlags.PathEllipsis);
            int lineY = panel.Top + S(80);
            Ui.FillRound(g, new Rectangle(panel.Left + S(24), lineY,
                panel.Width - S(48), S(4)), S(2), _theme.Track);
            Ui.FillRound(g, new Rectangle(panel.Left + S(24), lineY,
                (panel.Width - S(48)) / 3, S(4)), S(2), _theme.Accent);
        }

        private void DrawToast(Graphics g, string text)
        {
            int maxW = ClientSize.Width - S(48);
            Font font = _uiFont;
            Size size = TextRenderer.MeasureText(g, text, font,
                new Size(maxW, int.MaxValue), TextFormatFlags.SingleLine |
                TextFormatFlags.NoPrefix);
            int w = Math.Min(maxW, size.Width + S(34));
            int h = S(34);
            int x = (ClientSize.Width - w) / 2;
            int y = ClientSize.Height - (_chromeVisible ? S(70) : S(34)) - h;
            Color bg = _theme.IsDark ? Color.FromArgb(238, 238, 241, 245) :
                Color.FromArgb(240, 32, 36, 42);
            Color fg = _theme.IsDark ? Color.FromArgb(30, 32, 36) : Color.White;
            Ui.FillRound(g, new Rectangle(x, y, w, h), S(9), bg);
            Ui.Text(g, text, font, fg, new Rectangle(x + S(12), y, w - S(24), h),
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter |
                TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis);
        }

        private void DrawHint(Graphics g)
        {
            string text = "轻点两侧翻页 · 中间呼出菜单 · 左右轻扫也可以";
            int maxW = ClientSize.Width - S(48);
            Size size = TextRenderer.MeasureText(g, text, _uiSmall,
                new Size(maxW, int.MaxValue), TextFormatFlags.SingleLine);
            int w = Math.Min(maxW, size.Width + S(34));
            int h = S(32);
            int x = (ClientSize.Width - w) / 2;
            int y = ClientSize.Height - S(70) - h;
            Rectangle r = new Rectangle(x, y, w, h);
            Ui.FillRound(g, r, S(9), _theme.Panel);
            Ui.DrawRound(g, r, S(9), _theme.Border, 1f);
            g.FillRectangle(new SolidBrush(_theme.Accent), x + S(10), y + S(9),
                S(3), h - S(18));
            Ui.Text(g, text, _uiSmall, _theme.PanelText,
                new Rectangle(x + S(20), y, w - S(30), h),
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter |
                TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis);
        }

        private void DrawPageInfoPill(Graphics g)
        {
            if (_layout == null || _layout.Pages.Count == 0)
                return;
            string text = (_pageIndex + 1) + " / " + _layout.Pages.Count +
                "  ·  " + FormatPercent(CurrentPercent);
            int tw = Ui.MeasureWidth(text, _uiSmall) + S(24);
            int th = S(25);
            int x = ClientSize.Width - tw - S(14);
            int y = ClientSize.Height - th - S(12);
            Rectangle r = new Rectangle(x, y, tw, th);
            Ui.FillRound(g, r, S(8), Ui.Alpha(_theme.Panel, 235));
            Ui.DrawRound(g, r, S(8), _theme.Border, 1f);
            Ui.Text(g, text, _uiSmall, _theme.Muted, r,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter |
                TextFormatFlags.SingleLine);
        }

        private string FormatPercent(double p)
        {
            if (p >= 0.9995)
                return "100%";
            return (p * 100).ToString("0.0", CultureInfo.InvariantCulture) + "%";
        }
    }
}
