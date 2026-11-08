using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Windows.Forms;

namespace LightReader
{
    internal sealed class ShelfCard
    {
        public int Index;
        public BookMark Book;
        public Rectangle Card;
        public Rectangle Cover;
        public Rectangle MenuButton;
    }

    internal sealed class ShelfLayout
    {
        public Rectangle Header;
        public Rectangle Title;
        public Rectangle Grid;
        public Rectangle EmptyPanel;
        public Rectangle EmptyImport;
        public Rectangle EmptyBatch;
        public Rectangle Scrollbar;
        public List<ToolButton> HeaderButtons = new List<ToolButton>();
        public List<ShelfCard> Cards = new List<ShelfCard>();
        public int CardWidth;
        public int CardHeight;
        public int Gap;
        public int MaxScroll;
        public int Columns;
    }

    internal sealed class ShelfMenuItem
    {
        public string Id;
        public string Label;
        public Rectangle Rect;
    }

    internal sealed class ShelfMenuLayout
    {
        public Rectangle Panel;
        public List<ShelfMenuItem> Items = new List<ShelfMenuItem>();
    }

    internal sealed partial class ReaderForm
    {
        private ShelfLayout GetShelfLayout()
        {
            ShelfLayout l = new ShelfLayout();
            int w = ClientSize.Width;
            int h = ClientSize.Height;
            int pad = Math.Max(S(24), (w - S(1000)) / 2);
            int cw = Math.Min(S(1000), w - pad * 2);
            if (cw < S(300)) cw = Math.Max(S(240), w - S(24));
            l.Header = new Rectangle(pad, S(16), cw, S(58));
            l.Title = new Rectangle(pad, S(86), cw, S(46));
            l.Grid = new Rectangle(pad, S(142), cw,
                Math.Max(S(100), h - S(142) - S(24)));

            int bh = S(36);
            int gap = S(8);
            string nightLabel = _theme.IsDark ? "日间" : "夜间";
            string fullLabel = _fullscreen ? "退出" : "全屏";
            string batchLabel = "批量导入";
            int importW = Math.Max(S(64), Ui.MeasureWidth("导入", _uiFont) + S(26));
            int batchW = Math.Max(S(88), Ui.MeasureWidth(batchLabel, _uiFont) + S(30));
            int fullW = Math.Max(S(64), Ui.MeasureWidth(fullLabel, _uiFont) + S(26));
            int nightW = Math.Max(S(64), Ui.MeasureWidth(nightLabel, _uiFont) + S(26));
            if (importW + batchW + fullW + nightW + gap * 3 > cw - S(150))
            {
                batchLabel = "批量";
                batchW = Math.Max(S(64), Ui.MeasureWidth(batchLabel, _uiFont) + S(26));
            }
            int x = pad + cw;
            x -= nightW;
            l.HeaderButtons.Add(new ToolButton("shelf_night", nightLabel,
                new Rectangle(x, S(24), nightW, bh)));
            x -= gap + fullW;
            l.HeaderButtons.Add(new ToolButton("shelf_fullscreen", fullLabel,
                new Rectangle(x, S(24), fullW, bh)));
            x -= gap + batchW;
            l.HeaderButtons.Add(new ToolButton("shelf_batch", batchLabel,
                new Rectangle(x, S(24), batchW, bh)));
            x -= gap + importW;
            l.HeaderButtons.Add(new ToolButton("shelf_import", "导入",
                new Rectangle(x, S(24), importW, bh)));

            int count = _settings.Books.Count;
            if (count == 0)
            {
                int ew = Math.Min(S(460), cw - S(20));
                int eh = S(290);
                l.EmptyPanel = new Rectangle(pad + (cw - ew) / 2,
                    l.Grid.Top + Math.Max(0, (l.Grid.Height - eh) / 2), ew, eh);
                l.EmptyImport = new Rectangle(l.EmptyPanel.Left + S(36),
                    l.EmptyPanel.Bottom - S(92), S(170), S(48));
                l.EmptyBatch = new Rectangle(l.EmptyImport.Right + S(12),
                    l.EmptyImport.Top, S(180), S(48));
                return l;
            }

            l.Columns = cw >= S(900) ? 4 : (cw >= S(560) ? 3 : 2);
            int cardGap = S(18);
            int cardW = (cw - cardGap * (l.Columns - 1)) / l.Columns;
            int coverH = (int)Math.Round(cardW * 1.40);
            int cardH = coverH + S(64);
            l.CardWidth = cardW;
            l.CardHeight = cardH;
            l.Gap = cardGap;
            int rows = (count + l.Columns - 1) / l.Columns;
            int contentH = rows * cardH + Math.Max(0, rows - 1) * cardGap;
            l.MaxScroll = Math.Max(0, contentH - l.Grid.Height);
            ClampShelfScroll(l);

            for (int i = 0; i < count; i++)
            {
                int row = i / l.Columns;
                int col = i % l.Columns;
                int cy = l.Grid.Top + row * (cardH + cardGap) - _shelfScroll;
                ShelfCard card = new ShelfCard();
                card.Index = i;
                card.Book = _settings.Books[i];
                card.Card = new Rectangle(
                    l.Grid.Left + col * (cardW + cardGap), cy, cardW, cardH);
                card.Cover = new Rectangle(card.Card.Left, card.Card.Top,
                    cardW, coverH);
                card.MenuButton = new Rectangle(card.Cover.Right - S(34),
                    card.Cover.Top + S(8), S(28), S(28));
                l.Cards.Add(card);
            }
            if (l.MaxScroll > 0)
            {
                int trackH = l.Grid.Height;
                int thumbH = Math.Max(S(30),
                    (int)((long)trackH * l.Grid.Height / Math.Max(1, contentH)));
                int maxTop = trackH - thumbH;
                int thumbTop = l.Grid.Top + (maxTop <= 0 ? 0 :
                    (int)((long)_shelfScroll * maxTop / l.MaxScroll));
                l.Scrollbar = new Rectangle(w - S(10), thumbTop, S(3), thumbH);
            }
            return l;
        }

        private void ClampShelfScroll(ShelfLayout l)
        {
            if (_shelfScroll < 0) _shelfScroll = 0;
            if (_shelfScroll > l.MaxScroll) _shelfScroll = l.MaxScroll;
        }

        private void DrawBackdrop(Graphics g)
        {
            if (string.IsNullOrEmpty(_settings.BackgroundImage))
                return;
            if (!string.Equals(_backgroundBitmapPath, _settings.BackgroundImage,
                StringComparison.OrdinalIgnoreCase))
            {
                _backgroundBitmapPath = _settings.BackgroundImage;
                if (_backgroundBitmap != null)
                {
                    _backgroundBitmap.Dispose();
                    _backgroundBitmap = null;
                }
                try
                {
                    if (File.Exists(_settings.BackgroundImage))
                        _backgroundBitmap = ImageLoader.LoadScaled(
                            _settings.BackgroundImage, 1920, 1200);
                }
                catch
                {
                    _backgroundBitmap = null;
                }
            }
            if (_backgroundBitmap == null)
                return;
            Ui.DrawImageCover(g, _backgroundBitmap, ClientRectangle, 0);
            using (SolidBrush tint = new SolidBrush(Color.FromArgb(
                _theme.IsDark ? 165 : 140, _theme.Background)))
                g.FillRectangle(tint, ClientRectangle);
        }

        private void DrawShelf(Graphics g)
        {
            ShelfLayout l = GetShelfLayout();
            int logo = S(34);
            Rectangle logoRect = new Rectangle(l.Header.Left, S(22), logo, logo);
            if (_brandLogo != null)
            {
                DrawBrandLogo(g, logoRect, S(9));
            }
            else
            {
                Ui.FillRound(g, logoRect, S(9), _theme.Accent);
                Ui.DrawBookMark(g, new Rectangle(logoRect.Left + S(7),
                    logoRect.Top + S(6), logoRect.Width - S(14),
                    logoRect.Height - S(12)), _theme.AccentText,
                    Math.Max(1.2f, S(2) * 0.6f));
            }
            Ui.Text(g, "云牍", _uiTitle, _theme.Text,
                new Rectangle(logoRect.Right + S(10), S(20), S(100), S(24)),
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter |
                TextFormatFlags.SingleLine);
            Ui.Text(g, "本地小说 · 我的书架", _uiSmall, _theme.Muted,
                new Rectangle(logoRect.Right + S(10), S(44), S(240), S(18)),
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter |
                TextFormatFlags.SingleLine);

            for (int i = 0; i < l.HeaderButtons.Count; i++)
            {
                ToolButton b = l.HeaderButtons[i];
                bool primary = b.Id == "shelf_import";
                if (primary)
                {
                    Color fill = _pressedId == b.Id ? Ui.Blend(_theme.Accent, Color.Black, 0.1f)
                        : (_hoverId == b.Id ? Ui.Blend(_theme.Accent, Color.White, 0.08f)
                        : _theme.Accent);
                    Ui.FillRound(g, b.Rect, S(8), fill);
                    Ui.Text(g, b.Label, _uiFont, _theme.AccentText, b.Rect,
                        TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter |
                        TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis);
                }
                else
                {
                    bool hover = _hoverId == b.Id;
                    if (hover)
                        Ui.FillRound(g, b.Rect, S(8), _theme.Hover);
                    Ui.DrawRound(g, b.Rect, S(8), _theme.Border, 1f);
                    Ui.Text(g, b.Label, _uiFont, _theme.Text, b.Rect,
                        TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter |
                        TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis);
                }
            }

            Ui.Text(g, "我的书架", _shelfTitle, _theme.Text, l.Title,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter |
                TextFormatFlags.SingleLine);
            int count = _settings.Books.Count;
            if (count > 0)
            {
                Ui.Text(g, "共 " + count + " 本", _uiSmall, _theme.Muted,
                    new Rectangle(l.Title.Right - S(160), l.Title.Top,
                        S(160), l.Title.Height),
                    TextFormatFlags.Right | TextFormatFlags.VerticalCenter |
                    TextFormatFlags.SingleLine);
            }

            if (count == 0)
            {
                DrawEmptyShelf(g, l);
                return;
            }

            GraphicsState state = g.Save();
            g.SetClip(l.Grid);
            for (int i = 0; i < l.Cards.Count; i++)
            {
                ShelfCard card = l.Cards[i];
                if (card.Card.Bottom < l.Grid.Top || card.Card.Top > l.Grid.Bottom)
                    continue;
                DrawShelfCard(g, card);
            }
            g.Restore(state);
            if (l.MaxScroll > 0)
                Ui.FillRound(g, l.Scrollbar, S(2), Ui.Alpha(_theme.Muted, 130));
            Ui.Text(g, "把 TXT / EPUB 文件拖进窗口，也可以批量导入",
                _uiSmall, _theme.Muted,
                new Rectangle(l.Grid.Left, ClientSize.Height - S(22),
                    l.Grid.Width, S(18)),
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter |
                TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis);
        }

        private void DrawShelfCard(Graphics g, ShelfCard card)
        {
            BookMark b = card.Book;
            bool hover = _hoverId == "shelfopen:" + card.Index ||
                _hoverId == "shelfmenu:" + card.Index;
            Ui.FillRound(g, card.Cover, S(10), _theme.Panel);
            Bitmap cover = _coverCache.Get(b.CoverPath);
            if (cover != null)
                Ui.DrawImageCover(g, cover, card.Cover, S(10));
            else
            {
                Ui.FillRound(g, card.Cover, S(10),
                    Ui.Blend(_theme.Panel, _theme.Accent, 0.18f));
                string initial = string.IsNullOrEmpty(b.Title) ? "书" :
                    b.Title.Substring(0, 1);
                Ui.Text(g, initial, _welcomeTitle, Ui.Alpha(_theme.Accent, 220),
                    new Rectangle(card.Cover.Left, card.Cover.Top + S(20),
                        card.Cover.Width, card.Cover.Height - S(70)),
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter |
                    TextFormatFlags.SingleLine);
                Ui.Text(g, string.IsNullOrEmpty(b.Format) ? "TXT" : b.Format,
                    _uiSmallBold, _theme.Muted,
                    new Rectangle(card.Cover.Left, card.Cover.Bottom - S(54),
                        card.Cover.Width, S(22)),
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter |
                    TextFormatFlags.SingleLine);
            }
            Ui.DrawRound(g, card.Cover, S(10),
                hover ? _theme.Accent : _theme.Border,
                hover ? Math.Max(1.5f, S(2) * 0.8f) : 1f);

            if (b.Percent > 0)
            {
                Rectangle track = new Rectangle(card.Cover.Left + S(1),
                    card.Cover.Bottom - S(5), card.Cover.Width - S(2), S(4));
                using (SolidBrush tb = new SolidBrush(Color.FromArgb(90, 0, 0, 0)))
                    g.FillRectangle(tb, track);
                int fill = (int)Math.Round(track.Width *
                    Math.Max(0, Math.Min(1, b.Percent)));
                if (fill > 0)
                    using (SolidBrush fb = new SolidBrush(_theme.Accent))
                        g.FillRectangle(fb, track.Left, track.Top, fill, track.Height);
                int pw = S(58);
                Rectangle pill = new Rectangle(card.Cover.Right - pw - S(9),
                    card.Cover.Bottom - S(31), pw, S(22));
                Ui.FillRound(g, pill, S(7), Color.FromArgb(145, 0, 0, 0));
                Ui.Text(g, FormatPercent(b.Percent), _uiSmall, Color.White, pill,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter |
                    TextFormatFlags.SingleLine);
            }
            else
            {
                int pw = S(48);
                Rectangle pill = new Rectangle(card.Cover.Left + S(9),
                    card.Cover.Bottom - S(31), pw, S(22));
                Ui.FillRound(g, pill, S(7), Color.FromArgb(130, 0, 0, 0));
                Ui.Text(g, "未读", _uiSmall, Color.White, pill,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter |
                    TextFormatFlags.SingleLine);
            }

            Rectangle more = card.MenuButton;
            Ui.FillRound(g, more, more.Width / 2, Color.FromArgb(145, 0, 0, 0));
            using (SolidBrush brush = new SolidBrush(Color.White))
            {
                int cy = more.Top + more.Height / 2;
                int r = Math.Max(1, S(1.5f));
                g.FillEllipse(brush, more.Left + S(8) - r, cy - r, r * 2, r * 2);
                g.FillEllipse(brush, more.Left + S(14) - r, cy - r, r * 2, r * 2);
                g.FillEllipse(brush, more.Left + S(20) - r, cy - r, r * 2, r * 2);
            }

            Rectangle titleRect = new Rectangle(card.Card.Left, card.Cover.Bottom + S(7),
                card.Card.Width, S(24));
            Ui.Text(g, b.Title, _shelfCard, _theme.Text, titleRect,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter |
                TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis);
            string sub = !string.IsNullOrEmpty(b.Author) ? b.Author :
                (string.IsNullOrEmpty(b.Format) ? "TXT" : b.Format);
            if (b.Percent > 0 && string.IsNullOrEmpty(b.Author))
                sub += "  ·  " + FormatPercent(b.Percent);
            Rectangle subRect = new Rectangle(card.Card.Left, card.Cover.Bottom + S(31),
                card.Card.Width, S(20));
            Ui.Text(g, sub, _uiSmall, _theme.Muted, subRect,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter |
                TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis);
        }

        private void DrawEmptyShelf(Graphics g, ShelfLayout l)
        {
            Rectangle p = l.EmptyPanel;
            Ui.FillRound(g, p, S(16), _theme.Panel);
            Ui.DrawRound(g, p, S(16), _theme.Border, 1f);
            Rectangle icon = new Rectangle(p.Left + (p.Width - S(64)) / 2,
                p.Top + S(28), S(64), S(64));
            if (_brandLogo != null)
            {
                DrawBrandLogo(g, icon, S(15));
            }
            else
            {
                Ui.FillRound(g, icon, S(15), _theme.Accent);
                Ui.DrawBookMark(g, new Rectangle(icon.Left + S(14), icon.Top + S(12),
                    icon.Width - S(28), icon.Height - S(24)), _theme.AccentText,
                    Math.Max(1.4f, S(2) * 0.7f));
            }
            Ui.Text(g, "书架还是空的", _shelfTitle, _theme.Text,
                new Rectangle(p.Left, p.Top + S(104), p.Width, S(40)),
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter |
                TextFormatFlags.SingleLine);
            Ui.Text(g, "导入 TXT 或 EPUB，开始安静地读一本小说", _uiFont,
                _theme.Muted,
                new Rectangle(p.Left + S(20), p.Top + S(144), p.Width - S(40), S(26)),
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter |
                TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis);
            Ui.Text(g, "支持批量导入，也可以把文件拖进窗口", _uiSmall,
                _theme.Muted,
                new Rectangle(p.Left + S(20), p.Top + S(172), p.Width - S(40), S(22)),
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter |
                TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis);
            DrawAccentButton(g, l.EmptyImport, "导入小说", "shelf_import");
            DrawGhostButton(g, l.EmptyBatch, "批量导入", "shelf_batch");
        }

        private void DrawAccentButton(Graphics g, Rectangle r, string label, string id)
        {
            bool hover = _hoverId == id;
            bool pressed = _pressedId == id;
            Color fill = pressed ? Ui.Blend(_theme.Accent, Color.Black, 0.1f)
                : (hover ? Ui.Blend(_theme.Accent, Color.White, 0.08f) : _theme.Accent);
            Ui.FillRound(g, r, S(9), fill);
            Ui.Text(g, label, _uiBold, _theme.AccentText, r,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter |
                TextFormatFlags.SingleLine);
        }

        private void DrawShelfMenu(Graphics g)
        {
            ShelfMenuLayout ml = GetShelfMenuLayout();
            if (ml == null)
                return;
            g.FillRectangle(new SolidBrush(Color.FromArgb(90, 0, 0, 0)), ClientRectangle);
            Ui.FillRound(g, ml.Panel, S(10), _theme.Panel);
            Ui.DrawRound(g, ml.Panel, S(10), _theme.Border, 1f);
            for (int i = 0; i < ml.Items.Count; i++)
            {
                ShelfMenuItem item = ml.Items[i];
                bool hover = _hoverId == item.Id;
                if (hover)
                    Ui.FillRound(g, new Rectangle(item.Rect.Left + S(4),
                        item.Rect.Top + S(2), item.Rect.Width - S(8),
                        item.Rect.Height - S(4)), S(7), _theme.Hover);
                Color c = item.Id == "shelf_menu_remove" ? _theme.Accent :
                    _theme.PanelText;
                Ui.Text(g, item.Label, _uiFont, c,
                    new Rectangle(item.Rect.Left + S(14), item.Rect.Top,
                        item.Rect.Width - S(20), item.Rect.Height),
                    TextFormatFlags.Left | TextFormatFlags.VerticalCenter |
                    TextFormatFlags.SingleLine);
            }
        }

        private ShelfMenuLayout GetShelfMenuLayout()
        {
            if (_shelfMenuIndex < 0 || _shelfMenuIndex >= _settings.Books.Count)
                return null;
            ShelfLayout l = GetShelfLayout();
            ShelfCard card = null;
            for (int i = 0; i < l.Cards.Count; i++)
            {
                if (l.Cards[i].Index == _shelfMenuIndex)
                {
                    card = l.Cards[i];
                    break;
                }
            }
            if (card == null)
                return null;
            int w = S(166);
            int itemH = S(42);
            int h = itemH * 3 + S(8);
            int x = card.MenuButton.Right - w;
            int y = card.MenuButton.Bottom + S(6);
            if (x < S(8)) x = S(8);
            if (x + w > ClientSize.Width - S(8))
                x = ClientSize.Width - w - S(8);
            if (y + h > ClientSize.Height - S(8))
                y = ClientSize.Height - h - S(8);
            ShelfMenuLayout ml = new ShelfMenuLayout();
            ml.Panel = new Rectangle(x, y, w, h);
            string[] labels = new string[] { "打开", "更换封面", "从书架移除" };
            string[] ids = new string[] { "shelf_menu_open",
                "shelf_menu_cover", "shelf_menu_remove" };
            for (int i = 0; i < 3; i++)
            {
                ShelfMenuItem item = new ShelfMenuItem();
                item.Id = ids[i];
                item.Label = labels[i];
                item.Rect = new Rectangle(x + S(4), y + S(4) + i * itemH,
                    w - S(8), itemH);
                ml.Items.Add(item);
            }
            return ml;
        }

        private void DrawImporting(Graphics g)
        {
            g.FillRectangle(new SolidBrush(Color.FromArgb(120, 0, 0, 0)), ClientRectangle);
            int w = S(380);
            int h = S(142);
            Rectangle p = new Rectangle((ClientSize.Width - w) / 2,
                (ClientSize.Height - h) / 2, w, h);
            Ui.FillRound(g, p, S(14), _theme.Panel);
            Ui.DrawRound(g, p, S(14), _theme.Border, 1f);
            Ui.Text(g, "正在导入", _uiBold, _theme.PanelText,
                new Rectangle(p.Left + S(24), p.Top + S(18), p.Width - S(48), S(26)),
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter |
                TextFormatFlags.SingleLine);
            Ui.Text(g, _importCurrent, _uiSmall, _theme.Muted,
                new Rectangle(p.Left + S(24), p.Top + S(46), p.Width - S(48), S(20)),
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter |
                TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis);
            int trackY = p.Top + S(82);
            Ui.FillRound(g, new Rectangle(p.Left + S(24), trackY,
                p.Width - S(48), S(5)), S(2), _theme.Track);
            double ratio = _importTotal <= 0 ? 0 :
                Math.Min(1, _importDone / (double)_importTotal);
            Ui.FillRound(g, new Rectangle(p.Left + S(24), trackY,
                (int)((p.Width - S(48)) * ratio), S(5)), S(2), _theme.Accent);
            Ui.Text(g, _importDone + " / " + _importTotal, _uiSmall, _theme.Muted,
                new Rectangle(p.Left + S(24), p.Top + S(102), p.Width - S(48), S(20)),
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter |
                TextFormatFlags.SingleLine);
        }

        private void GoToShelf()
        {
            if (_book != null)
                SaveProgress();
            _book = null;
            _bookPath = null;
            _layout = null;
            _pageIndex = 0;
            _chapterIndex = 0;
            _overlay = Overlay.None;
            _shelfMenuIndex = -1;
            _chromeVisible = true;
            _hintUntil = DateTime.MinValue;
            Text = "云牍 · 小说阅读器";
            Invalidate();
        }

        private void OpenShelfBook(int index)
        {
            if (index < 0 || index >= _settings.Books.Count)
                return;
            BookMark b = _settings.Books[index];
            if (!File.Exists(b.Path))
            {
                ShowToast("文件不存在，可能已移动");
                return;
            }
            _overlay = Overlay.None;
            _shelfMenuIndex = -1;
            OpenBook(b.Path);
        }

        private void OpenShelfMenu(int index)
        {
            if (index < 0 || index >= _settings.Books.Count)
                return;
            _shelfMenuIndex = index;
            _overlay = Overlay.ShelfMenu;
            Invalidate();
        }

        private void CloseShelfMenu()
        {
            _overlay = Overlay.None;
            _shelfMenuIndex = -1;
            _pressedId = "";
            _downHit = "";
            Invalidate();
        }

        private void HandleShelfUp(Point p)
        {
            if (_shelfDragMoved)
            {
                ClearDown();
                Invalidate();
                return;
            }
            string hit = HitTest(p);
            if (!string.IsNullOrEmpty(_downHit) && hit == _downHit)
                ExecuteHit(hit);
            ClearDown();
            Invalidate();
        }

        private void HandleShelfMenuUp(Point p)
        {
            ShelfMenuLayout ml = GetShelfMenuLayout();
            if (ml == null)
            {
                CloseShelfMenu();
                return;
            }
            if (!ml.Panel.Contains(p))
            {
                CloseShelfMenu();
                return;
            }
            string hit = HitTest(p);
            if (!string.IsNullOrEmpty(_downHit) && hit == _downHit)
                ExecuteHit(hit);
            ClearDown();
            Invalidate();
        }

        private void ShowImportDialog()
        {
            try
            {
                using (OpenFileDialog dlg = new OpenFileDialog())
                {
                    dlg.Title = "批量导入小说";
                    dlg.Filter = "小说 (*.txt;*.epub;*.md;*.log)|*.txt;*.epub;*.md;*.log|所有文件 (*.*)|*.*";
                    dlg.CheckFileExists = true;
                    dlg.Multiselect = true;
                    dlg.RestoreDirectory = true;
                    if (dlg.ShowDialog(this) == DialogResult.OK)
                        ImportBooks(dlg.FileNames);
                }
            }
            catch (Exception ex)
            {
                ShowToast("无法打开文件选择器：" + ex.Message);
            }
        }

        private void ImportBooks(string[] paths)
        {
            if (_importing || paths == null || paths.Length == 0)
                return;
            _importToken++;
            int token = _importToken;
            _importing = true;
            _importDone = 0;
            _importTotal = paths.Length;
            _importCurrent = "";
            _overlay = Overlay.None;
            _shelfMenuIndex = -1;
            Invalidate();

            string[] copy = (string[])paths.Clone();
            System.Threading.ThreadPool.QueueUserWorkItem(delegate
            {
                List<BookMetadata> ok = new List<BookMetadata>();
                List<string> failed = new List<string>();
                for (int i = 0; i < copy.Length; i++)
                {
                    string path = copy[i];
                    try
                    {
                        BookMetadata m = BookLoader.LoadMetadata(path);
                        ok.Add(m);
                    }
                    catch
                    {
                        failed.Add(Path.GetFileName(path));
                    }
                    int done = i + 1;
                    string name = Path.GetFileName(path);
                    try
                    {
                        BeginInvoke(new MethodInvoker(delegate
                        {
                            if (IsDisposed || token != _importToken)
                                return;
                            _importDone = done;
                            _importCurrent = name;
                            Invalidate();
                        }));
                    }
                    catch
                    {
                    }
                }
                try
                {
                    BeginInvoke(new MethodInvoker(delegate
                    {
                        if (IsDisposed || token != _importToken)
                            return;
                        FinishImport(ok, failed);
                    }));
                }
                catch
                {
                }
            });
        }

        private void FinishImport(List<BookMetadata> ok, List<string> failed)
        {
            _importing = false;
            _importCurrent = "";
            for (int i = 0; i < ok.Count; i++)
            {
                BookMetadata m = ok[i];
                _settings.UpsertBook(m.Path, m.Title, m.Author, m.CoverPath,
                    m.Format, m.Size);
            }
            _settings.SortBooks();
            if (_settings.Books.Count > 200)
                _settings.Books.RemoveRange(200, _settings.Books.Count - 200);
            QueueSave();
            Invalidate();
            if (failed.Count > 0)
                ShowToast("已导入 " + ok.Count + " 本，失败 " + failed.Count + " 本");
            else
                ShowToast("已导入 " + ok.Count + " 本");
        }

        private void ChooseCover(int index)
        {
            if (index < 0 || index >= _settings.Books.Count)
                return;
            BookMark b = _settings.Books[index];
            try
            {
                using (OpenFileDialog dlg = new OpenFileDialog())
                {
                    dlg.Title = "选择封面";
                    dlg.Filter = "图片 (*.jpg;*.jpeg;*.png;*.webp)|*.jpg;*.jpeg;*.png;*.webp";
                    dlg.CheckFileExists = true;
                    if (dlg.ShowDialog(this) != DialogResult.OK)
                        return;
                    using (Bitmap bmp = ImageLoader.LoadScaled(dlg.FileName, 720, 960))
                    {
                        string cover = ImageLoader.SaveCover(bmp, b.Path);
                        b.CoverPath = cover;
                    }
                    _coverCache.Remove(b.CoverPath);
                    QueueSave();
                    Invalidate();
                    ShowToast("封面已更新");
                }
            }
            catch (Exception ex)
            {
                ShowToast("封面设置失败：" + ex.Message);
            }
        }

        private void RemoveFromShelf(int index)
        {
            if (index < 0 || index >= _settings.Books.Count)
                return;
            BookMark b = _settings.Books[index];
            DialogResult r = MessageBox.Show(this,
                "从书架移除《" + b.Title + "》？\r\n原文件不会被删除。",
                "云牍", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
            if (r != DialogResult.Yes)
                return;
            _coverCache.Remove(b.CoverPath);
            ImageLoader.DeleteCover(b.CoverPath);
            _settings.RemoveBook(b.Path);
            _shelfMenuIndex = -1;
            _overlay = Overlay.None;
            QueueSave();
            Invalidate();
            ShowToast("已从书架移除");
        }

        private void UpdateBrightnessFromX(int x)
        {
            SettingsLayout sl = GetSettingsLayout();
            int left = sl.BrightnessTrack.Left;
            int width = Math.Max(1, sl.BrightnessTrack.Width);
            double t = (x - left) / (double)width;
            if (t < 0) t = 0;
            if (t > 1) t = 1;
            int value = (int)Math.Round(20 + t * 80);
            if (value == _brightness)
                return;
            _brightness = value;
            if (!_brightnessController.Apply(value))
            {
                _brightness = 100;
                _brightnessController.Restore();
                ShowToast("当前屏幕不支持软件亮度调节");
            }
            Invalidate();
        }

        private void ClampSettingsScroll(SettingsLayout l)
        {
            if (_settingsScroll < 0) _settingsScroll = 0;
            if (_settingsScroll > l.MaxScroll) _settingsScroll = l.MaxScroll;
        }

        private void ChooseCustomColor()
        {
            try
            {
                using (ColorDialog dlg = new ColorDialog())
                {
                    dlg.FullOpen = true;
                    dlg.Color = Ui.ParseColor(_settings.CustomColor,
                        Color.FromArgb(0xF7, 0xF0, 0xDD));
                    if (dlg.ShowDialog(this) == DialogResult.OK)
                    {
                        _settings.CustomColor = Ui.ColorToHtml(dlg.Color);
                        ApplyTheme("custom");
                    }
                }
            }
            catch (Exception ex)
            {
                ShowToast("无法打开颜色选择器：" + ex.Message);
            }
        }

        private void ChooseBackgroundImage()
        {
            try
            {
                using (OpenFileDialog dlg = new OpenFileDialog())
                {
                    dlg.Title = "选择阅读背景图片";
                    dlg.Filter = "图片 (*.jpg;*.jpeg;*.png;*.webp)|*.jpg;*.jpeg;*.png;*.webp";
                    dlg.CheckFileExists = true;
                    if (dlg.ShowDialog(this) != DialogResult.OK)
                        return;
                    using (Bitmap bmp = ImageLoader.LoadScaled(dlg.FileName, 1920, 1200))
                    {
                        if (bmp == null)
                            throw new InvalidDataException("图片无法读取。");
                        string dest = ImageLoader.SaveBackground(bmp);
                        _settings.BackgroundImage = dest;
                    }
                    _backgroundBitmapPath = "";
                    if (_backgroundBitmap != null)
                    {
                        _backgroundBitmap.Dispose();
                        _backgroundBitmap = null;
                    }
                    QueueSave();
                    Invalidate();
                    ShowToast("背景图片已更新");
                }
            }
            catch (Exception ex)
            {
                ShowToast("背景图片设置失败：" + ex.Message);
            }
        }

        private void ClearBackgroundImage()
        {
            if (string.IsNullOrEmpty(_settings.BackgroundImage))
                return;
            string old = _settings.BackgroundImage;
            _settings.BackgroundImage = "";
            _backgroundBitmapPath = "";
            if (_backgroundBitmap != null)
            {
                _backgroundBitmap.Dispose();
                _backgroundBitmap = null;
            }
            try
            {
                if (old.StartsWith(ReaderSettings.SettingsFolder,
                    StringComparison.OrdinalIgnoreCase) && File.Exists(old))
                    File.Delete(old);
            }
            catch
            {
            }
            QueueSave();
            Invalidate();
            ShowToast("已清除背景图片");
        }
    }
}
