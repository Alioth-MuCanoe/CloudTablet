using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Windows.Forms;
using Timer = System.Windows.Forms.Timer;

namespace LightReader
{
    internal enum Overlay
    {
        None,
        Toc,
        Settings,
        ShelfMenu
    }

    internal enum DownRegion
    {
        None,
        Content,
        Toolbar,
        Footer,
        Progress,
        Welcome,
        Shelf,
        ShelfMenu,
        Toc,
        TocList,
        Settings,
        OutsidePanel
    }

    internal sealed class HitTarget
    {
        public string Id;
        public Rectangle Rect;

        public HitTarget(string id, Rectangle rect)
        {
            Id = id;
            Rect = rect;
        }
    }

    internal sealed class ToolButton
    {
        public string Id;
        public string Label;
        public Rectangle Rect;

        public ToolButton(string id, string label, Rectangle rect)
        {
            Id = id;
            Label = label;
            Rect = rect;
        }
    }

    internal sealed partial class ReaderForm : Form
    {
        private ReaderSettings _settings;
        private ReaderTheme _theme;
        private BookData _book;
        private string _bookPath;
        private ChapterLayout _layout;
        private int _chapterIndex;
        private int _pageIndex;
        private string[] _startupPaths;

        private Font _bodyFont;
        private Font _titleFont;
        private Font _uiFont;
        private Font _uiSmall;
        private Font _uiSmallBold;
        private Font _uiMedium;
        private Font _uiBold;
        private Font _uiTitle;
        private Font _welcomeTitle;
        private Font _shelfTitle;
        private Font _shelfCard;

        private Timer _saveTimer;
        private Timer _toastTimer;
        private Timer _relayoutTimer;
        private Timer _pageInfoTimer;
        private Timer _hintTimer;

        private bool _loading;
        private string _loadingName = "";
        private int _loadToken;
        private bool _chromeVisible = true;
        private Overlay _overlay = Overlay.None;
        private bool _fullscreen;
        private FormBorderStyle _preFullscreenStyle = FormBorderStyle.Sizable;
        private Rectangle _preFullscreenBounds = Rectangle.Empty;
        private FormWindowState _preFullscreenState = FormWindowState.Normal;
        private bool _suppressRelayout;

        private float _dpiScale = 1f;
        private Rectangle _contentRect = Rectangle.Empty;

        private string _toastText = "";
        private DateTime _toastUntil = DateTime.MinValue;
        private DateTime _pageInfoUntil = DateTime.MinValue;
        private DateTime _hintUntil = DateTime.MinValue;

        private int _tocScroll;
        private int _tocDragStartY;
        private int _tocDragStartScroll;
        private bool _tocDragMoved;

        private Point _mouseDown;
        private bool _dragging;
        private DownRegion _downRegion = DownRegion.None;
        private string _downHit = "";
        private string _pressedId = "";
        private string _hoverId = "";

        private bool _seeking;
        private double _seekRatio;
        private int _wheelAccum;

        private List<BookMark> _recentVisible;
        private DateTime _recentVisibleAt = DateTime.MinValue;

        private int _shelfScroll;
        private int _shelfDragStartY;
        private int _shelfDragStartScroll;
        private bool _shelfDragMoved;
        private int _shelfMenuIndex = -1;
        private readonly CoverCache _coverCache = new CoverCache(18);
        private Bitmap _backgroundBitmap;
        private Bitmap _brandLogo;
        private string _backgroundBitmapPath = "";
        private int _brightness = 100;
        private readonly BrightnessController _brightnessController =
            new BrightnessController();
        private bool _brightnessDragging;
        private int _settingsScroll;
        private int _settingsDragStartY;
        private int _settingsDragStartScroll;
        private bool _settingsDragMoved;
        private bool _importing;
        private int _importDone;
        private int _importTotal;
        private string _importCurrent = "";
        private int _importToken;

        public ReaderForm(string[] startupPaths)
        {
            BrightnessController.RestoreStale();
            _startupPaths = startupPaths;
            _settings = ReaderSettings.Load();
            _settings.Normalize();
            if (string.Equals(_settings.Theme, "custom", StringComparison.OrdinalIgnoreCase))
                _theme = ThemeCatalog.FromCustom(Ui.ParseColor(_settings.CustomColor,
                    Color.FromArgb(0xF7, 0xF0, 0xDD)));
            else
                _theme = ThemeCatalog.GetOrDefault(_settings.Theme);

            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            DoubleBuffered = true;
            AutoScaleMode = AutoScaleMode.None;
            FormBorderStyle = FormBorderStyle.Sizable;
            StartPosition = FormStartPosition.Manual;
            KeyPreview = true;
            AllowDrop = true;
            BackColor = _theme.Background;
            Text = "云牍 · 小说阅读器";

            UpdateDpiScale();

            string uiFamily = Ui.ResolveFontFamily("", true);
            _uiFont = new Font(uiFamily, 10f, FontStyle.Regular, GraphicsUnit.Point);
            _uiSmall = new Font(uiFamily, 8.5f, FontStyle.Regular, GraphicsUnit.Point);
            _uiSmallBold = new Font(uiFamily, 8.5f, FontStyle.Bold, GraphicsUnit.Point);
            _uiMedium = new Font(uiFamily, 11.5f, FontStyle.Regular, GraphicsUnit.Point);
            _uiBold = new Font(uiFamily, 10f, FontStyle.Bold, GraphicsUnit.Point);
            _uiTitle = new Font(uiFamily, 13f, FontStyle.Bold, GraphicsUnit.Point);
            _welcomeTitle = new Font(uiFamily, 28f, FontStyle.Bold, GraphicsUnit.Point);
            _shelfTitle = new Font(uiFamily, 21f, FontStyle.Bold, GraphicsUnit.Point);
            _shelfCard = new Font(uiFamily, 11f, FontStyle.Bold, GraphicsUnit.Point);

            _bodyFont = CreateBodyFont();
            _titleFont = CreateTitleFont(_bodyFont);

            MinimumSize = SizeFromClientSize(new Size(S(640), S(500)));
            ApplyWindowBounds();

            _saveTimer = new Timer();
            _saveTimer.Interval = 1200;
            _saveTimer.Tick += delegate
            {
                _saveTimer.Stop();
                SaveNow();
            };

            _toastTimer = new Timer();
            _toastTimer.Interval = 2500;
            _toastTimer.Tick += delegate
            {
                _toastTimer.Stop();
                Invalidate();
            };

            _pageInfoTimer = new Timer();
            _pageInfoTimer.Interval = 1400;
            _pageInfoTimer.Tick += delegate
            {
                _pageInfoTimer.Stop();
                Invalidate();
            };

            _hintTimer = new Timer();
            _hintTimer.Interval = 9000;
            _hintTimer.Tick += delegate
            {
                _hintTimer.Stop();
                _settings.Hints = false;
                QueueSave();
                Invalidate();
            };

            _relayoutTimer = new Timer();
            _relayoutTimer.Interval = 160;
            _relayoutTimer.Tick += delegate
            {
                _relayoutTimer.Stop();
                RelayoutPreservingOffset();
            };

            try
            {
                using (Stream stream = Assembly.GetExecutingAssembly()
                    .GetManifestResourceStream("CloudTablet.logo.png"))
                {
                    if (stream != null)
                        _brandLogo = new Bitmap(stream);
                }
            }
            catch
            {
            }

            try
            {
                using (Icon ico = Icon.ExtractAssociatedIcon(
                    Application.ExecutablePath))
                {
                    Icon = ico;
                    if (_brandLogo == null && ico != null)
                    {
                        using (Icon big = new Icon(ico, 128, 128))
                            _brandLogo = big.ToBitmap();
                    }
                }
            }
            catch
            {
            }
        }

        private Font CreateBodyFont()
        {
            string family = Ui.ResolveFontFamily(_settings.FontFamily, false);
            _settings.FontFamily = family;
            try
            {
                return new Font(family, _settings.FontSize, FontStyle.Regular,
                    GraphicsUnit.Point);
            }
            catch
            {
                return new Font(FontFamily.GenericSansSerif, _settings.FontSize,
                    FontStyle.Regular, GraphicsUnit.Point);
            }
        }

        private Font CreateTitleFont(Font body)
        {
            return CreateTitleFont(body, _settings.FontSize);
        }

        private Font CreateTitleFont(Font body, float bodySize)
        {
            float size = Math.Max(12f, Math.Min(40f, bodySize * 1.42f));
            try
            {
                return new Font(body.FontFamily, size, FontStyle.Bold, GraphicsUnit.Point);
            }
            catch
            {
                try
                {
                    return new Font(body.FontFamily, size, FontStyle.Regular,
                        GraphicsUnit.Point);
                }
                catch
                {
                    return new Font(FontFamily.GenericSansSerif, size, FontStyle.Bold,
                        GraphicsUnit.Point);
                }
            }
        }

        private void UpdateDpiScale()
        {
            try
            {
                using (Graphics g = CreateGraphics())
                    _dpiScale = Math.Max(0.75f, Math.Min(3f, g.DpiX / 96f));
            }
            catch
            {
                _dpiScale = 1f;
            }
            if (Math.Abs(_dpiScale - 1f) < 0.01f)
                _dpiScale = 1f;
        }

        private int S(float px)
        {
            return (int)Math.Round(px * _dpiScale);
        }

        private void ApplyWindowBounds()
        {
            Rectangle wa = Screen.PrimaryScreen.WorkingArea;
            int w;
            int h;
            if (_settings.WindowWidth > 0 && _settings.WindowHeight > 0)
            {
                w = S(_settings.WindowWidth);
                h = S(_settings.WindowHeight);
            }
            else
            {
                w = Math.Min(S(1120), wa.Width - S(48));
                h = Math.Min(S(800), wa.Height - S(48));
            }
            w = Math.Max(MinimumSize.Width, Math.Min(w, wa.Width));
            h = Math.Max(MinimumSize.Height, Math.Min(h, wa.Height));
            int x = wa.Left + (wa.Width - w) / 2;
            int y = wa.Top + (wa.Height - h) / 2;
            Rectangle b = new Rectangle(x, y, w, h);
            Rectangle virtualScreen = SystemInformation.VirtualScreen;
            if (!virtualScreen.IntersectsWith(b))
                b = new Rectangle(wa.Left + S(20), wa.Top + S(20), w, h);
            Bounds = b;
            if (_settings.Maximized)
                WindowState = FormWindowState.Maximized;
        }

        private void EnsureContentRect()
        {
            int w = ClientSize.Width;
            int h = ClientSize.Height;
            int top = S(56) + S(20);
            int bottom = S(46) + S(18);
            int maxWidth = S(860);
            int side = Math.Max(S(28), (w - maxWidth) / 2);
            int cw = Math.Min(maxWidth, w - side * 2);
            if (cw < S(240))
            {
                side = S(16);
                cw = Math.Max(S(80), w - side * 2);
            }
            int ch = h - top - bottom;
            if (ch < S(120))
                ch = Math.Max(S(60), h - top - S(16));
            _contentRect = new Rectangle(side, top, Math.Max(S(80), cw), Math.Max(S(60), ch));
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            Native.SetDarkTitleBar(Handle, _theme.IsDark);
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            EnsureContentRect();
            if (_startupPaths != null && _startupPaths.Length == 1)
                OpenBook(_startupPaths[0]);
            else if (_startupPaths != null && _startupPaths.Length > 1)
                ImportBooks(_startupPaths);
            Invalidate();
        }

        private void OpenBook(string path)
        {
            if (string.IsNullOrEmpty(path))
                return;
            if (!File.Exists(path))
            {
                ShowError("找不到文件：\r\n" + path);
                if (_settings.FindBook(path) != null)
                {
                    _settings.RemoveBook(path);
                    InvalidateRecentCache();
                    QueueSave();
                }
                return;
            }

            if (_book != null)
            {
                SaveProgress();
                SaveWindowState();
                SaveNow();
            }

            _loadToken++;
            int token = _loadToken;
            _loading = true;
            _loadingName = Path.GetFileName(path);
            _overlay = Overlay.None;
            _toastText = "";
            _toastUntil = DateTime.MinValue;
            _hintUntil = DateTime.MinValue;
            if (!_fullscreen)
                _chromeVisible = true;
            Invalidate();

            ThreadPool.QueueUserWorkItem(delegate
            {
                BookData loaded = null;
                Exception error = null;
                try
                {
                    loaded = BookLoader.Load(path);
                }
                catch (Exception ex)
                {
                    error = ex;
                }
                try
                {
                    BeginInvoke(new MethodInvoker(delegate
                    {
                        if (IsDisposed || token != _loadToken)
                            return;
                        FinishOpenBook(loaded, error, path);
                    }));
                }
                catch
                {
                }
            });
        }

        private void FinishOpenBook(BookData loaded, Exception error, string path)
        {
            _loading = false;
            if (error != null)
            {
                Invalidate();
                ShowError("打开失败：\r\n" + error.Message);
                return;
            }
            if (loaded == null)
            {
                Invalidate();
                return;
            }

            _book = loaded;
            _bookPath = loaded.Path;
            _layout = null;
            _pageIndex = 0;
            _chapterIndex = 0;
            _overlay = Overlay.None;
            _shelfMenuIndex = -1;
            _toastText = "";
            _toastUntil = DateTime.MinValue;
            _tocScroll = 0;
            InvalidateRecentCache();

            BookMark mark = _settings.FindBook(_bookPath);
            int offset = mark == null ? 0 : mark.Offset;
            if (offset < 0 || offset >= _book.Text.Length)
                offset = 0;
            EnsureContentRect();
            BuildLayoutForOffset(offset);
            double percent = _book.Text.Length <= 1 ? 0 : offset / (double)_book.Text.Length;
            BookMark stored = _settings.TouchBook(_bookPath, _book.Title, offset, percent);
            stored.Author = _book.Author ?? "";
            stored.Format = _book.Format ?? ReaderSettings.GuessFormat(_bookPath);
            if (!string.IsNullOrEmpty(_book.CoverPath) && !File.Exists(stored.CoverPath))
                stored.CoverPath = _book.CoverPath;
            try { stored.Size = new FileInfo(_bookPath).Length; }
            catch { }
            QueueSave();

            Text = _book.Title + " · 云牍";
            if (!_fullscreen)
                _chromeVisible = true;
            Invalidate();

            if (mark != null && offset > 0)
            {
                ShowToast("已回到上次阅读位置");
            }
            else if (_settings.Hints)
            {
                _hintUntil = DateTime.Now.AddMilliseconds(9000);
                _hintTimer.Stop();
                _hintTimer.Start();
            }
        }

        private void BuildLayoutForOffset(int absoluteOffset)
        {
            if (_book == null || _book.Chapters.Count == 0)
                return;
            int chapter = _book.ChapterIndexForOffset(absoluteOffset);
            ChapterInfo ch = _book.Chapters[chapter];
            int rel = absoluteOffset - ch.Start;
            if (rel < 0) rel = 0;
            BuildChapterLayout(chapter, rel);
        }

        private void BuildChapterLayout(int chapter, int targetRelativeOffset)
        {
            if (_book == null || _book.Chapters.Count == 0)
                return;
            if (chapter < 0) chapter = 0;
            if (chapter >= _book.Chapters.Count) chapter = _book.Chapters.Count - 1;
            EnsureContentRect();
            ChapterInfo ch = _book.Chapters[chapter];
            TextLayoutEngine engine = new TextLayoutEngine(_bodyFont, _titleFont,
                _contentRect.Width, _contentRect.Height, _settings.LineSpacing, _dpiScale);
            _layout = engine.Build(_book.Text, ch.Start, ch.Length, ch.Title);
            _chapterIndex = chapter;
            if (targetRelativeOffset < 0)
                targetRelativeOffset = 0;
            _pageIndex = _layout.PageIndexForOffset(targetRelativeOffset);
            if (_pageIndex < 0)
                _pageIndex = 0;
            if (_pageIndex >= _layout.Pages.Count)
                _pageIndex = Math.Max(0, _layout.Pages.Count - 1);
        }

        private void RelayoutPreservingOffset()
        {
            if (_book == null)
            {
                EnsureContentRect();
                Invalidate();
                return;
            }
            int offset = CurrentAbsoluteOffset();
            BuildLayoutForOffset(offset);
            Invalidate();
        }

        private int CurrentAbsoluteOffset()
        {
            if (_book == null || _book.Chapters.Count == 0)
                return 0;
            if (_layout == null || _layout.Pages.Count == 0)
                return _book.Chapters[_chapterIndex].Start;
            int rel = _layout.Pages[Math.Max(0,
                Math.Min(_pageIndex, _layout.Pages.Count - 1))].Start;
            return _book.Chapters[_chapterIndex].Start + rel;
        }

        private double CurrentPercent
        {
            get
            {
                if (_book == null || _book.Text.Length <= 1)
                    return 0;
                if (_chapterIndex == _book.Chapters.Count - 1 &&
                    _layout != null && _pageIndex == _layout.Pages.Count - 1)
                    return 1;
                return Math.Max(0, Math.Min(1,
                    CurrentAbsoluteOffset() / (double)_book.Text.Length));
            }
        }

        private bool AtFirstPage
        {
            get { return _chapterIndex <= 0 && _pageIndex <= 0; }
        }

        private bool AtLastPage
        {
            get
            {
                return _book != null && _chapterIndex >= _book.Chapters.Count - 1 &&
                    _layout != null && _pageIndex >= _layout.Pages.Count - 1;
            }
        }

        private void NextPage()
        {
            if (_book == null || _layout == null || _loading)
                return;
            DismissHint();
            if (_pageIndex + 1 < _layout.Pages.Count)
            {
                _pageIndex++;
            }
            else if (_chapterIndex + 1 < _book.Chapters.Count)
            {
                BuildChapterLayout(_chapterIndex + 1, 0);
                _pageIndex = 0;
            }
            else
            {
                ShowToast("已经是最后一页");
                return;
            }
            AfterPageChange();
        }

        private void PrevPage()
        {
            if (_book == null || _layout == null || _loading)
                return;
            DismissHint();
            if (_pageIndex > 0)
            {
                _pageIndex--;
            }
            else if (_chapterIndex > 0)
            {
                BuildChapterLayout(_chapterIndex - 1, int.MaxValue);
                _pageIndex = Math.Max(0, _layout.Pages.Count - 1);
            }
            else
            {
                ShowToast("已经是第一页");
                return;
            }
            AfterPageChange();
        }

        private void AfterPageChange()
        {
            _pageInfoUntil = DateTime.Now.AddMilliseconds(1400);
            _pageInfoTimer.Stop();
            _pageInfoTimer.Start();
            SaveProgress();
            Invalidate();
        }

        private void DismissHint()
        {
            if (_hintUntil == DateTime.MinValue)
                return;
            _hintUntil = DateTime.MinValue;
            _hintTimer.Stop();
            _settings.Hints = false;
            QueueSave();
        }

        private void GoToChapter(int index)
        {
            if (_book == null)
                return;
            if (index < 0) index = 0;
            if (index >= _book.Chapters.Count) index = _book.Chapters.Count - 1;
            _overlay = Overlay.None;
            BuildChapterLayout(index, 0);
            _chromeVisible = true;
            AfterPageChange();
        }

        private void GoToOffset(int absoluteOffset)
        {
            if (_book == null)
                return;
            if (absoluteOffset < 0) absoluteOffset = 0;
            if (absoluteOffset >= _book.Text.Length)
                absoluteOffset = Math.Max(0, _book.Text.Length - 1);
            int chapter = _book.ChapterIndexForOffset(absoluteOffset);
            if (chapter == _chapterIndex && _layout != null)
            {
                _pageIndex = _layout.PageIndexForOffset(
                    absoluteOffset - _book.Chapters[chapter].Start);
            }
            else
            {
                BuildLayoutForOffset(absoluteOffset);
            }
            AfterPageChange();
        }

        private void GoToPercent(double p)
        {
            if (_book == null)
                return;
            if (p < 0) p = 0;
            if (p > 1) p = 1;
            int offset = (int)Math.Round(p * Math.Max(1, _book.Text.Length - 1));
            GoToOffset(offset);
        }

        private void ToggleChrome()
        {
            _chromeVisible = !_chromeVisible;
            _hoverId = "";
            Invalidate();
        }

        private void ToggleToc()
        {
            if (_book == null)
                return;
            if (_overlay == Overlay.Toc)
            {
                _overlay = Overlay.None;
                Invalidate();
                return;
            }
            _overlay = Overlay.Toc;
            _chromeVisible = true;
            TocLayout tl = GetTocLayout();
            int row = _chapterIndex * tl.RowHeight;
            _tocScroll = Math.Max(0, row - tl.List.Height / 2 + tl.RowHeight / 2);
            ClampTocScroll(tl);
            Invalidate();
        }

        private void ToggleSettings()
        {
            if (_book == null)
                return;
            if (_overlay != Overlay.Settings)
                _settingsScroll = 0;
            _overlay = _overlay == Overlay.Settings ? Overlay.None : Overlay.Settings;
            _chromeVisible = true;
            Invalidate();
        }

        private void ShowAbout()
        {
            using (AboutForm form = new AboutForm())
                form.ShowDialog(this);
        }

        private void CloseOverlay()
        {
            if (_overlay == Overlay.None)
                return;
            if (_overlay == Overlay.ShelfMenu)
            {
                CloseShelfMenu();
                return;
            }
            _overlay = Overlay.None;
            Invalidate();
        }

        private void ApplyTheme(string id)
        {
            ReaderTheme t;
            if (string.Equals(id, "custom", StringComparison.OrdinalIgnoreCase))
            {
                Color c = Ui.ParseColor(_settings.CustomColor,
                    Color.FromArgb(0xF7, 0xF0, 0xDD));
                t = ThemeCatalog.FromCustom(c);
            }
            else
            {
                t = ThemeCatalog.Get(id);
            }
            if (t == null)
                return;
            _theme = t;
            _settings.Theme = t.Id;
            if (t.Id == "custom")
            {
                _settings.CustomColor = Ui.ColorToHtml(t.Background);
            }
            else if (t.IsDark)
            {
                _settings.LastDarkTheme = t.Id;
            }
            else
            {
                _settings.LastLightTheme = t.Id;
            }
            BackColor = t.Background;
            Native.SetDarkTitleBar(Handle, t.IsDark);
            QueueSave();
            Invalidate();
        }

        private void ToggleDarkMode()
        {
            if (_theme.IsDark)
                ApplyTheme(_settings.LastLightTheme);
            else
                ApplyTheme(_settings.LastDarkTheme);
        }

        private void ChooseFont()
        {
            try
            {
                using (FontDialog dlg = new FontDialog())
                {
                    dlg.Font = _bodyFont;
                    dlg.ShowColor = false;
                    dlg.ShowEffects = false;
                    dlg.AllowVerticalFonts = false;
                    dlg.FontMustExist = true;
                    dlg.MinSize = 10;
                    dlg.MaxSize = 36;
                    if (dlg.ShowDialog(this) == DialogResult.OK)
                    {
                        ApplyTypography(dlg.Font.FontFamily.Name, dlg.Font.SizeInPoints,
                            _settings.LineSpacing, true);
                    }
                }
            }
            catch (Exception ex)
            {
                ShowToast("无法打开字体选择器：" + ex.Message);
            }
        }

        private void ApplyTypography(string family, float size, float spacing, bool toast)
        {
            if (string.IsNullOrEmpty(family))
                family = Ui.ResolveFontFamily(_settings.FontFamily, false);
            else
                family = Ui.ResolveFontFamily(family, false);
            size = Math.Max(10f, Math.Min(36f, size));
            spacing = Math.Max(1.2f, Math.Min(2.4f, spacing));
            int offset = CurrentAbsoluteOffset();

            Font body = null;
            Font title = null;
            try
            {
                body = new Font(family, size, FontStyle.Regular, GraphicsUnit.Point);
                title = CreateTitleFont(body, size);
            }
            catch
            {
                if (body != null) body.Dispose();
                body = null;
            }
            if (body == null)
            {
                ShowToast("系统里没有这个字体");
                return;
            }

            if (_bodyFont != null) _bodyFont.Dispose();
            if (_titleFont != null) _titleFont.Dispose();
            _bodyFont = body;
            _titleFont = title;
            _settings.FontFamily = family;
            _settings.FontSize = size;
            _settings.LineSpacing = spacing;
            if (_book != null)
                BuildLayoutForOffset(offset);
            QueueSave();
            Invalidate();
            if (toast)
                ShowToast("字体已更新");
        }

        private void ChangeFontSize(float delta)
        {
            ApplyTypography(_settings.FontFamily, _settings.FontSize + delta,
                _settings.LineSpacing, false);
        }

        private void ResetTypography()
        {
            ApplyTypography(Ui.ResolveFontFamily("", false), 15f, 1.7f, true);
        }

        private void SaveProgress()
        {
            if (_book == null || string.IsNullOrEmpty(_bookPath))
                return;
            int offset = CurrentAbsoluteOffset();
            double percent = _book.Text.Length <= 1 ? 0 : offset / (double)_book.Text.Length;
            _settings.TouchBook(_bookPath, _book.Title, offset, percent);
            QueueSave();
        }

        private void QueueSave()
        {
            if (_saveTimer == null)
                return;
            _saveTimer.Stop();
            _saveTimer.Start();
        }

        private void SaveWindowState()
        {
            if (_fullscreen)
            {
                _settings.WindowWidth = Logical(_preFullscreenBounds.Width);
                _settings.WindowHeight = Logical(_preFullscreenBounds.Height);
                _settings.Maximized = _preFullscreenState == FormWindowState.Maximized;
                return;
            }
            if (WindowState == FormWindowState.Maximized)
            {
                Rectangle r = RestoreBounds;
                if (r.Width >= MinimumSize.Width && r.Height >= MinimumSize.Height)
                {
                    _settings.WindowWidth = Logical(r.Width);
                    _settings.WindowHeight = Logical(r.Height);
                }
                _settings.Maximized = true;
            }
            else if (WindowState == FormWindowState.Normal)
            {
                _settings.WindowWidth = Logical(Width);
                _settings.WindowHeight = Logical(Height);
                _settings.Maximized = false;
            }
        }

        private int Logical(int pixels)
        {
            if (_dpiScale <= 0)
                return pixels;
            return (int)Math.Round(pixels / _dpiScale);
        }

        private void SaveNow()
        {
            SaveWindowState();
            _settings.Save();
        }

        private void ShowToast(string text)
        {
            _toastText = text ?? "";
            _toastUntil = DateTime.Now.AddMilliseconds(2500);
            _toastTimer.Stop();
            _toastTimer.Start();
            Invalidate();
        }

        private void ShowError(string message)
        {
            try
            {
                MessageBox.Show(this, message, "云牍", MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
            }
            catch
            {
            }
        }

        private void OpenFileDialog()
        {
            try
            {
                using (OpenFileDialog dlg = new OpenFileDialog())
                {
                    dlg.Title = "打开小说";
                    dlg.Filter = "小说 (*.txt;*.epub;*.md;*.log)|*.txt;*.epub;*.md;*.log|所有文件 (*.*)|*.*";
                    dlg.CheckFileExists = true;
                    dlg.Multiselect = false;
                    dlg.RestoreDirectory = true;
                    if (dlg.ShowDialog(this) == DialogResult.OK)
                        OpenBook(dlg.FileName);
                }
            }
            catch (Exception ex)
            {
                ShowToast("无法打开文件选择器：" + ex.Message);
            }
        }

        private void ToggleFullscreen()
        {
            if (!_fullscreen)
            {
                _preFullscreenStyle = FormBorderStyle;
                _preFullscreenBounds = Bounds;
                _preFullscreenState = WindowState;
                _fullscreen = true;
                _suppressRelayout = true;
                FormBorderStyle = FormBorderStyle.None;
                WindowState = FormWindowState.Normal;
                Bounds = Screen.FromControl(this).Bounds;
                _suppressRelayout = false;
                _chromeVisible = false;
            }
            else
            {
                _fullscreen = false;
                _suppressRelayout = true;
                FormBorderStyle = _preFullscreenStyle;
                Bounds = _preFullscreenBounds;
                WindowState = _preFullscreenState;
                _suppressRelayout = false;
                _chromeVisible = true;
            }
            EnsureContentRect();
            if (_book != null)
                RelayoutPreservingOffset();
            Invalidate();
        }

        private void InvalidateRecentCache()
        {
            _recentVisible = null;
            _recentVisibleAt = DateTime.MinValue;
        }

        private List<BookMark> GetVisibleRecents()
        {
            if (_recentVisible != null &&
                (DateTime.Now - _recentVisibleAt).TotalSeconds < 3)
                return _recentVisible;
            List<BookMark> list = new List<BookMark>();
            for (int i = 0; i < _settings.Books.Count && list.Count < 6; i++)
            {
                BookMark b = _settings.Books[i];
                try
                {
                    if (File.Exists(b.Path))
                        list.Add(b);
                }
                catch
                {
                }
            }
            _recentVisible = list;
            _recentVisibleAt = DateTime.Now;
            return list;
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            EnsureContentRect();
            if (_book != null && !_suppressRelayout && _relayoutTimer != null)
            {
                _relayoutTimer.Stop();
                _relayoutTimer.Start();
            }
            Invalidate();
        }

        protected override void OnResizeEnd(EventArgs e)
        {
            base.OnResizeEnd(e);
            if (_relayoutTimer != null)
                _relayoutTimer.Stop();
            if (_book != null)
                RelayoutPreservingOffset();
            SaveWindowState();
            QueueSave();
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            _loadToken++;
            _importToken++;
            if (_saveTimer != null) _saveTimer.Stop();
            if (_relayoutTimer != null) _relayoutTimer.Stop();
            if (_book != null)
                SaveProgress();
            SaveWindowState();
            _settings.Save();
            _brightnessController.Restore();
            base.OnFormClosing(e);
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            if (_saveTimer != null) _saveTimer.Dispose();
            if (_toastTimer != null) _toastTimer.Dispose();
            if (_relayoutTimer != null) _relayoutTimer.Dispose();
            if (_pageInfoTimer != null) _pageInfoTimer.Dispose();
            if (_hintTimer != null) _hintTimer.Dispose();
            if (_bodyFont != null) _bodyFont.Dispose();
            if (_titleFont != null) _titleFont.Dispose();
            if (_uiFont != null) _uiFont.Dispose();
            if (_uiSmall != null) _uiSmall.Dispose();
            if (_uiSmallBold != null) _uiSmallBold.Dispose();
            if (_uiMedium != null) _uiMedium.Dispose();
            if (_uiBold != null) _uiBold.Dispose();
            if (_uiTitle != null) _uiTitle.Dispose();
            if (_welcomeTitle != null) _welcomeTitle.Dispose();
            if (_shelfTitle != null) _shelfTitle.Dispose();
            if (_shelfCard != null) _shelfCard.Dispose();
            _brightnessController.Dispose();
            _coverCache.Clear();
            if (_backgroundBitmap != null) _backgroundBitmap.Dispose();
            if (_brandLogo != null) _brandLogo.Dispose();
            base.OnFormClosed(e);
        }

        protected override void OnDragEnter(DragEventArgs e)
        {
            base.OnDragEnter(e);
            if (e.Data != null && e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                string[] files = e.Data.GetData(DataFormats.FileDrop) as string[];
                e.Effect = files != null && files.Length > 0
                    ? DragDropEffects.Copy : DragDropEffects.None;
            }
        }

        protected override void OnDragDrop(DragEventArgs e)
        {
            base.OnDragDrop(e);
            if (e.Data == null || !e.Data.GetDataPresent(DataFormats.FileDrop))
                return;
            string[] files = e.Data.GetData(DataFormats.FileDrop) as string[];
            if (files != null && files.Length > 0)
            {
                if (_book == null || files.Length > 1)
                    ImportBooks(files);
                else
                    OpenBook(files[0]);
            }
        }
    }
}
