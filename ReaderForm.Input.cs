using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace LightReader
{
    internal sealed partial class ReaderForm
    {
        private List<HitTarget> BuildHitTargets()
        {
            List<HitTarget> hits = new List<HitTarget>();
            if (_loading || _importing)
                return hits;

            if (_overlay == Overlay.ShelfMenu)
            {
                ShelfMenuLayout ml = GetShelfMenuLayout();
                if (ml != null)
                {
                    for (int i = 0; i < ml.Items.Count; i++)
                        hits.Add(new HitTarget(ml.Items[i].Id, ml.Items[i].Rect));
                }
                return hits;
            }

            if (_overlay == Overlay.Toc)
            {
                TocLayout tl = GetTocLayout();
                hits.Add(new HitTarget("toc_close", tl.Close));
                if (_book != null)
                {
                    int first = Math.Max(0, _tocScroll / tl.RowHeight);
                    int last = Math.Min(_book.Chapters.Count - 1,
                        (_tocScroll + tl.List.Height) / tl.RowHeight + 1);
                    for (int i = first; i <= last; i++)
                    {
                        int y = tl.List.Top + i * tl.RowHeight - _tocScroll;
                        Rectangle r = new Rectangle(tl.List.Left, y, tl.List.Width,
                            tl.RowHeight);
                        if (r.Bottom > tl.List.Top && r.Top < tl.List.Bottom)
                            hits.Add(new HitTarget("toc:" + i, r));
                    }
                }
                return hits;
            }

            if (_overlay == Overlay.Settings)
            {
                SettingsLayout sl = GetSettingsLayout();
                hits.Add(new HitTarget("settings_close", sl.Close));
                hits.Add(new HitTarget("font_choose", sl.FontButton));
                hits.Add(new HitTarget("size_minus", sl.SizeMinus));
                hits.Add(new HitTarget("size_plus", sl.SizePlus));
                for (int i = 0; i < 3; i++)
                    hits.Add(new HitTarget("spacing:" + i, sl.Spacing[i]));
                for (int i = 0; i < 6; i++)
                    hits.Add(new HitTarget("theme:" + sl.ThemeIds[i], sl.Themes[i]));
                hits.Add(new HitTarget("brightness", sl.BrightnessTrack));
                hits.Add(new HitTarget("custom_color", sl.CustomColor));
                hits.Add(new HitTarget("bg_image", sl.BackgroundImage));
                if (sl.ClearImage.Width > 0)
                    hits.Add(new HitTarget("bg_clear", sl.ClearImage));
                hits.Add(new HitTarget("reset_typo", sl.Reset));
                hits.Add(new HitTarget("about", sl.About));
                return hits;
            }

            if (_book == null)
            {
                ShelfLayout sl = GetShelfLayout();
                for (int i = 0; i < sl.HeaderButtons.Count; i++)
                    hits.Add(new HitTarget(sl.HeaderButtons[i].Id, sl.HeaderButtons[i].Rect));
                for (int i = 0; i < sl.Cards.Count; i++)
                {
                    ShelfCard card = sl.Cards[i];
                    hits.Add(new HitTarget("shelfopen:" + card.Index, card.Card));
                    hits.Add(new HitTarget("shelfmenu:" + card.Index, card.MenuButton));
                }
                if (sl.EmptyImport.Width > 0)
                    hits.Add(new HitTarget("shelf_import", sl.EmptyImport));
                if (sl.EmptyBatch.Width > 0)
                    hits.Add(new HitTarget("shelf_batch", sl.EmptyBatch));
                return hits;
            }

            if (_chromeVisible)
            {
                List<ToolButton> buttons = GetToolbarButtons();
                for (int i = 0; i < buttons.Count; i++)
                    hits.Add(new HitTarget(buttons[i].Id, buttons[i].Rect));
                hits.Add(new HitTarget("prev", GetPrevButtonRect()));
                hits.Add(new HitTarget("next", GetNextButtonRect()));
            }
            return hits;
        }

        private string HitTest(Point p)
        {
            List<HitTarget> hits = BuildHitTargets();
            for (int i = hits.Count - 1; i >= 0; i--)
            {
                if (hits[i].Rect.Contains(p))
                    return hits[i].Id;
            }
            return "";
        }

        private void ExecuteHit(string id)
        {
            if (string.IsNullOrEmpty(id))
                return;

            if (id.StartsWith("shelfopen:", StringComparison.Ordinal))
            {
                int index;
                if (int.TryParse(id.Substring(10), out index))
                    OpenShelfBook(index);
                return;
            }
            if (id.StartsWith("shelfmenu:", StringComparison.Ordinal))
            {
                int index;
                if (int.TryParse(id.Substring(10), out index))
                    OpenShelfMenu(index);
                return;
            }
            if (id.StartsWith("toc:", StringComparison.Ordinal))
            {
                int index;
                if (int.TryParse(id.Substring(4), out index))
                {
                    _overlay = Overlay.None;
                    GoToChapter(index);
                }
                return;
            }
            if (id.StartsWith("recent:", StringComparison.Ordinal))
            {
                int index;
                if (int.TryParse(id.Substring(7), out index))
                {
                    List<BookMark> recents = GetVisibleRecents();
                    if (index >= 0 && index < recents.Count)
                        OpenBook(recents[index].Path);
                }
                return;
            }
            if (id.StartsWith("theme:", StringComparison.Ordinal))
            {
                ApplyTheme(id.Substring(6));
                return;
            }
            if (id.StartsWith("spacing:", StringComparison.Ordinal))
            {
                int index;
                if (int.TryParse(id.Substring(8), out index))
                {
                    float spacing = index == 0 ? 1.45f : (index == 1 ? 1.7f : 1.95f);
                    ApplyTypography(_settings.FontFamily, _settings.FontSize, spacing, false);
                }
                return;
            }

            switch (id)
            {
                case "open":
                case "welcome_open":
                case "shelf_import":
                    OpenFileDialog();
                    break;
                case "shelf_batch":
                    ShowImportDialog();
                    break;
                case "shelf_night":
                case "night":
                    ToggleDarkMode();
                    break;
                case "shelf_fullscreen":
                    ToggleFullscreen();
                    break;
                case "shelf":
                    GoToShelf();
                    break;
                case "fullscreen":
                    ToggleFullscreen();
                    break;
                case "shelf_menu_open":
                    if (_shelfMenuIndex >= 0 && _shelfMenuIndex < _settings.Books.Count)
                    {
                        int index = _shelfMenuIndex;
                        CloseShelfMenu();
                        OpenShelfBook(index);
                    }
                    break;
                case "shelf_menu_cover":
                    if (_shelfMenuIndex >= 0)
                    {
                        int index = _shelfMenuIndex;
                        CloseShelfMenu();
                        ChooseCover(index);
                    }
                    break;
                case "shelf_menu_remove":
                    if (_shelfMenuIndex >= 0)
                    {
                        int index = _shelfMenuIndex;
                        CloseShelfMenu();
                        RemoveFromShelf(index);
                    }
                    break;
                case "toc":
                    ToggleToc();
                    break;
                case "settings":
                    ToggleSettings();
                    break;
                case "toc_close":
                case "settings_close":
                    CloseOverlay();
                    break;
                case "font_choose":
                    ChooseFont();
                    break;
                case "size_minus":
                    ChangeFontSize(-1f);
                    break;
                case "size_plus":
                    ChangeFontSize(1f);
                    break;
                case "reset_typo":
                    ResetTypography();
                    break;
                case "about":
                    ShowAbout();
                    break;
                case "custom_color":
                    ChooseCustomColor();
                    break;
                case "bg_image":
                    ChooseBackgroundImage();
                    break;
                case "bg_clear":
                    ClearBackgroundImage();
                    break;
                case "exitfull":
                    ToggleFullscreen();
                    break;
                case "prev":
                    PrevPage();
                    break;
                case "next":
                    NextPage();
                    break;
            }
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (_loading || _importing || e.Button != MouseButtons.Left)
                return;

            Focus();
            _mouseDown = e.Location;
            _dragging = false;
            _downHit = "";
            _downRegion = DownRegion.None;
            _pressedId = "";

            if (_overlay == Overlay.ShelfMenu)
            {
                Capture = true;
                _downRegion = DownRegion.ShelfMenu;
                _downHit = HitTest(e.Location);
                _pressedId = _downHit;
                Invalidate();
                return;
            }

            if (_overlay == Overlay.Toc)
            {
                TocLayout tl = GetTocLayout();
                Capture = true;
                if (!tl.Panel.Contains(e.Location))
                {
                    _downRegion = DownRegion.OutsidePanel;
                    Invalidate();
                    return;
                }
                if (tl.List.Contains(e.Location))
                {
                    _downRegion = DownRegion.TocList;
                    _tocDragStartY = e.Y;
                    _tocDragStartScroll = _tocScroll;
                    _tocDragMoved = false;
                }
                else
                {
                    _downRegion = DownRegion.Toc;
                    _downHit = HitTest(e.Location);
                    _pressedId = _downHit;
                }
                Invalidate();
                return;
            }

            if (_overlay == Overlay.Settings)
            {
                SettingsLayout sl = GetSettingsLayout();
                Capture = true;
                if (!sl.Panel.Contains(e.Location))
                {
                    _downRegion = DownRegion.OutsidePanel;
                    Invalidate();
                    return;
                }
                _downRegion = DownRegion.Settings;
                _downHit = HitTest(e.Location);
                _pressedId = _downHit;
                _settingsDragStartY = e.Y;
                _settingsDragStartScroll = _settingsScroll;
                _settingsDragMoved = false;
                if (_downHit == "brightness")
                {
                    _brightnessDragging = true;
                    UpdateBrightnessFromX(e.X);
                }
                Invalidate();
                return;
            }

            if (_book == null)
            {
                Capture = true;
                _downRegion = DownRegion.Shelf;
                _downHit = HitTest(e.Location);
                _pressedId = _downHit;
                _shelfDragStartY = e.Y;
                _shelfDragStartScroll = _shelfScroll;
                _shelfDragMoved = false;
                Invalidate();
                return;
            }

            if (_chromeVisible && GetTopBarRect().Contains(e.Location))
            {
                Capture = true;
                _downRegion = DownRegion.Toolbar;
                _downHit = HitTest(e.Location);
                _pressedId = _downHit;
                Invalidate();
                return;
            }

            if (_chromeVisible && e.Y >= ClientSize.Height - S(13))
            {
                Capture = true;
                _downRegion = DownRegion.Progress;
                _seeking = true;
                _seekRatio = Math.Max(0, Math.Min(1,
                    e.X / (double)Math.Max(1, ClientSize.Width)));
                Invalidate();
                return;
            }

            if (_chromeVisible && GetFooterRect().Contains(e.Location))
            {
                Capture = true;
                _downRegion = DownRegion.Footer;
                _downHit = HitTest(e.Location);
                _pressedId = _downHit;
                Invalidate();
                return;
            }

            Capture = true;
            _downRegion = DownRegion.Content;
            Invalidate();
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);

            if (_seeking)
            {
                _seekRatio = Math.Max(0, Math.Min(1,
                    e.X / (double)Math.Max(1, ClientSize.Width)));
                Invalidate();
                return;
            }

            if (_brightnessDragging)
            {
                UpdateBrightnessFromX(e.X);
                return;
            }

            if (_downRegion == DownRegion.Shelf)
            {
                int dy = e.Y - _shelfDragStartY;
                if (Math.Abs(dy) > S(4))
                    _shelfDragMoved = true;
                if (_shelfDragMoved)
                {
                    ShelfLayout sl = GetShelfLayout();
                    _shelfScroll = _shelfDragStartScroll - dy;
                    ClampShelfScroll(sl);
                    Invalidate();
                }
                return;
            }

            if (_downRegion == DownRegion.Settings)
            {
                int dy = e.Y - _settingsDragStartY;
                if (Math.Abs(dy) > S(4))
                    _settingsDragMoved = true;
                if (_settingsDragMoved)
                {
                    SettingsLayout sl = GetSettingsLayout();
                    _settingsScroll = _settingsDragStartScroll - dy;
                    ClampSettingsScroll(sl);
                    Invalidate();
                }
                return;
            }

            if (_downRegion == DownRegion.TocList)
            {
                int dy = e.Y - _tocDragStartY;
                if (Math.Abs(dy) > S(4))
                    _tocDragMoved = true;
                if (_tocDragMoved)
                {
                    TocLayout tl = GetTocLayout();
                    _tocScroll = _tocDragStartScroll - dy;
                    ClampTocScroll(tl);
                    Invalidate();
                }
                return;
            }

            if (_downRegion == DownRegion.Content)
            {
                int dx = e.X - _mouseDown.X;
                int dy = e.Y - _mouseDown.Y;
                if (Math.Abs(dx) > S(10) || Math.Abs(dy) > S(10))
                    _dragging = true;
                return;
            }

            string hit = HitTest(e.Location);
            if (hit != _hoverId)
            {
                _hoverId = hit;
                Invalidate();
            }

            if (!string.IsNullOrEmpty(hit))
                Cursor = Cursors.Hand;
            else if (_overlay != Overlay.None)
                Cursor = Cursors.Default;
            else if (_book != null && _chromeVisible &&
                GetTopBarRect().Contains(e.Location))
                Cursor = Cursors.Default;
            else if (_book != null && _chromeVisible && GetFooterRect().Contains(e.Location))
                Cursor = Cursors.Default;
            else if (_book != null)
                Cursor = e.X < ClientSize.Width * 0.3 ? Cursors.PanWest :
                    (e.X > ClientSize.Width * 0.7 ? Cursors.PanEast : Cursors.Default);
            else
                Cursor = Cursors.Default;
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            if (e.Button != MouseButtons.Left || _loading || _importing)
                return;
            Capture = false;

            if (_brightnessDragging)
            {
                _brightnessDragging = false;
                ClearDown();
                Invalidate();
                return;
            }

            if (_seeking)
            {
                _seeking = false;
                double ratio = Math.Max(0, Math.Min(1,
                    e.X / (double)Math.Max(1, ClientSize.Width)));
                ClearDown();
                GoToPercent(ratio);
                return;
            }

            if (_overlay == Overlay.ShelfMenu)
            {
                HandleShelfMenuUp(e.Location);
                return;
            }
            if (_overlay == Overlay.Toc)
            {
                HandleTocUp(e.Location);
                return;
            }
            if (_overlay == Overlay.Settings)
            {
                HandleSettingsUp(e.Location);
                return;
            }
            if (_book == null)
            {
                HandleShelfUp(e.Location);
                return;
            }
            if (_downRegion == DownRegion.Toolbar || _downRegion == DownRegion.Footer)
            {
                string hit = HitTest(e.Location);
                if (!string.IsNullOrEmpty(_downHit) && hit == _downHit)
                    ExecuteHit(hit);
                ClearDown();
                Invalidate();
                return;
            }
            if (_downRegion == DownRegion.Content)
            {
                if (_dragging)
                {
                    int dx = e.X - _mouseDown.X;
                    int dy = e.Y - _mouseDown.Y;
                    if (Math.Abs(dx) > S(46) && Math.Abs(dx) > Math.Abs(dy) * 1.1)
                    {
                        if (dx < 0) NextPage(); else PrevPage();
                    }
                    else if (Math.Abs(dy) > S(56))
                    {
                        if (dy < 0) NextPage(); else PrevPage();
                    }
                    else
                    {
                        TapAt(_mouseDown);
                    }
                }
                else
                {
                    TapAt(_mouseDown);
                }
            }
            ClearDown();
            Invalidate();
        }

        private void HandleTocUp(Point p)
        {
            TocLayout tl = GetTocLayout();
            if (!tl.Panel.Contains(p))
            {
                CloseOverlay();
                ClearDown();
                return;
            }
            if (_downRegion == DownRegion.TocList)
            {
                if (!_tocDragMoved && _book != null)
                {
                    int row = (p.Y - tl.List.Top + _tocScroll) / tl.RowHeight;
                    if (row >= 0 && row < _book.Chapters.Count &&
                        p.Y >= tl.List.Top && p.Y < tl.List.Bottom)
                    {
                        _overlay = Overlay.None;
                        GoToChapter(row);
                    }
                }
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

        private void HandleSettingsUp(Point p)
        {
            SettingsLayout sl = GetSettingsLayout();
            if (!sl.Panel.Contains(p))
            {
                CloseOverlay();
                ClearDown();
                return;
            }
            if (_settingsDragMoved)
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

        private void TapAt(Point p)
        {
            if (_book == null)
                return;
            int x = p.X;
            if (x < ClientSize.Width * 0.30)
                PrevPage();
            else if (x > ClientSize.Width * 0.70)
                NextPage();
            else
                ToggleChrome();
        }

        private void ClearDown()
        {
            _pressedId = "";
            _downHit = "";
            _downRegion = DownRegion.None;
            _dragging = false;
            _tocDragMoved = false;
            _shelfDragMoved = false;
            _settingsDragMoved = false;
            _hoverId = "";
        }

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            if (_loading || _importing)
                return;

            if (_overlay == Overlay.ShelfMenu)
                return;
            if (_overlay == Overlay.Toc)
            {
                TocLayout tl = GetTocLayout();
                _tocScroll -= (e.Delta / 120) * tl.RowHeight * 3;
                ClampTocScroll(tl);
                Invalidate();
                return;
            }
            if (_overlay == Overlay.Settings)
            {
                SettingsLayout sl = GetSettingsLayout();
                _settingsScroll -= (e.Delta / 120) * S(60);
                ClampSettingsScroll(sl);
                Invalidate();
                return;
            }
            if (_book == null)
            {
                ShelfLayout shelf = GetShelfLayout();
                _shelfScroll -= (e.Delta / 120) * (shelf.CardHeight + shelf.Gap);
                ClampShelfScroll(shelf);
                Invalidate();
                return;
            }

            _wheelAccum += e.Delta;
            if (_wheelAccum >= 120)
            {
                _wheelAccum = 0;
                PrevPage();
            }
            else if (_wheelAccum <= -120)
            {
                _wheelAccum = 0;
                NextPage();
            }
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            if (!string.IsNullOrEmpty(_hoverId))
            {
                _hoverId = "";
                Invalidate();
            }
        }

        protected override bool IsInputKey(Keys keyData)
        {
            Keys k = keyData & Keys.KeyCode;
            if (k == Keys.Left || k == Keys.Right || k == Keys.Up || k == Keys.Down ||
                k == Keys.Space || k == Keys.PageUp || k == Keys.PageDown ||
                k == Keys.Home || k == Keys.End)
                return true;
            return base.IsInputKey(keyData);
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            if (_importing)
                return;

            if (e.Control && e.Shift && e.KeyCode == Keys.O)
            {
                ShowImportDialog();
                e.Handled = true;
                return;
            }
            if (e.Control && e.KeyCode == Keys.O)
            {
                OpenFileDialog();
                e.Handled = true;
                return;
            }
            if (e.KeyCode == Keys.F1)
            {
                ShowAbout();
                e.Handled = true;
                return;
            }
            if (e.KeyCode == Keys.F11)
            {
                ToggleFullscreen();
                e.Handled = true;
                return;
            }
            if (e.KeyCode == Keys.Escape)
            {
                if (_overlay == Overlay.ShelfMenu)
                    CloseShelfMenu();
                else if (_overlay != Overlay.None)
                    CloseOverlay();
                else if (_fullscreen)
                    ToggleFullscreen();
                e.Handled = true;
                return;
            }
            if (e.Control && e.KeyCode == Keys.B && _book != null)
            {
                GoToShelf();
                e.Handled = true;
                return;
            }
            if (e.Control && e.KeyCode == Keys.T)
            {
                ToggleToc();
                e.Handled = true;
                return;
            }
            if (e.Control && (e.KeyCode == Keys.Oemcomma || e.KeyCode == Keys.OemPeriod))
            {
                ToggleSettings();
                e.Handled = true;
                return;
            }
            if (e.Control && e.KeyCode == Keys.D)
            {
                ToggleDarkMode();
                e.Handled = true;
                return;
            }
            if (e.Control && (e.KeyCode == Keys.Oemplus || e.KeyCode == Keys.Add))
            {
                ChangeFontSize(1f);
                e.Handled = true;
                return;
            }
            if (e.Control && (e.KeyCode == Keys.OemMinus || e.KeyCode == Keys.Subtract))
            {
                ChangeFontSize(-1f);
                e.Handled = true;
                return;
            }
            if (e.Control && (e.KeyCode == Keys.D0 || e.KeyCode == Keys.NumPad0))
            {
                ApplyTypography(_settings.FontFamily, 15f, _settings.LineSpacing, false);
                e.Handled = true;
                return;
            }

            if (_overlay == Overlay.Toc)
            {
                TocLayout tl = GetTocLayout();
                if (e.KeyCode == Keys.Up)
                    _tocScroll -= tl.RowHeight;
                else if (e.KeyCode == Keys.Down)
                    _tocScroll += tl.RowHeight;
                else if (e.KeyCode == Keys.PageUp)
                    _tocScroll -= tl.List.Height;
                else if (e.KeyCode == Keys.PageDown)
                    _tocScroll += tl.List.Height;
                else
                    return;
                ClampTocScroll(tl);
                Invalidate();
                e.Handled = true;
                return;
            }
            if (_overlay == Overlay.Settings)
                return;

            if (_book == null)
                return;

            if (e.KeyCode == Keys.Left || e.KeyCode == Keys.Up ||
                e.KeyCode == Keys.PageUp || (e.KeyCode == Keys.Space && e.Shift))
            {
                PrevPage();
                e.Handled = true;
            }
            else if (e.KeyCode == Keys.Right || e.KeyCode == Keys.Down ||
                e.KeyCode == Keys.PageDown || e.KeyCode == Keys.Space)
            {
                NextPage();
                e.Handled = true;
            }
            else if (e.KeyCode == Keys.Home)
            {
                GoToChapter(0);
                e.Handled = true;
            }
            else if (e.KeyCode == Keys.End && _book != null)
            {
                GoToChapter(_book.Chapters.Count - 1);
                e.Handled = true;
            }
        }
    }
}
