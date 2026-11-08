using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace LightReader
{
    internal sealed class LineData
    {
        public string Text = "";
        public int Start;
        public int End;
        public int Indent;
    }

    internal sealed class PageData
    {
        public List<LineData> Lines = new List<LineData>();
        public int Start;
        public int End;
        public bool First;
    }

    internal sealed class ChapterLayout
    {
        public List<PageData> Pages = new List<PageData>();
        public int LineHeight;
        public int Leading;
        public int TitleBlockHeight;
        public Font BodyFont;
        public Font TitleFont;
        public int Width;
        public int Height;

        public int PageIndexForOffset(int offset)
        {
            if (Pages.Count == 0)
                return 0;
            for (int i = 0; i < Pages.Count; i++)
            {
                if (offset < Pages[i].End || i == Pages.Count - 1)
                    return i;
            }
            return Pages.Count - 1;
        }
    }

    internal sealed class TextLayoutEngine
    {
        private readonly Font _body;
        private readonly Font _title;
        private readonly int _width;
        private readonly int _height;
        private readonly float _lineSpacing;
        private readonly float _scale;
        private readonly Dictionary<string, int> _measured = new Dictionary<string, int>();
        private int _cjkWidth;
        private int _spaceWidth;
        private int _indent;

        private const string NoStartPunctuation = "，。！？；：、）》」』】〕〉…·,.!?;:%)]}”’";

        public TextLayoutEngine(Font body, Font title, int width, int height,
            float lineSpacing, float scale)
        {
            _body = body;
            _title = title;
            _width = Math.Max(80, width);
            _height = Math.Max(80, height);
            _lineSpacing = Math.Max(1.1f, lineSpacing);
            _scale = Math.Max(0.75f, scale);
            _cjkWidth = (int)Math.Round(
                MeasureRaw("国国国国国国国国国国") / 10.0);
            if (_cjkWidth < 2)
                _cjkWidth = Math.Max(8, _body.Height);
            int a = MeasureRaw("中 中");
            int b = MeasureRaw("中中");
            _spaceWidth = Math.Max(2, a - b);
            _indent = _cjkWidth * 2;
        }

        public ChapterLayout Build(string chapterText, int start, int length, string title)
        {
            if (start < 0) start = 0;
            if (start > chapterText.Length) start = chapterText.Length;
            if (length < 0) length = 0;
            if (start + length > chapterText.Length)
                length = chapterText.Length - start;
            string body = chapterText.Substring(start, length);

            ChapterLayout layout = new ChapterLayout();
            layout.BodyFont = _body;
            layout.TitleFont = _title;
            layout.Width = _width;
            layout.Height = _height;
            layout.LineHeight = Math.Max(_body.Height,
                (int)Math.Round(_body.GetHeight() * _lineSpacing));
            layout.Leading = Math.Max(0, (layout.LineHeight - _body.Height) / 2);

            int titleBlock = 0;
            if (!string.IsNullOrEmpty(title))
            {
                titleBlock = S(8) + _title.Height + S(26);
                int maxTitle = Math.Max(layout.LineHeight * 2, _height / 3);
                if (titleBlock > maxTitle)
                    titleBlock = maxTitle;
            }
            layout.TitleBlockHeight = titleBlock;

            List<LineData> lines = new List<LineData>();
            int p = 0;
            while (p < body.Length)
            {
                int nl = body.IndexOf('\n', p);
                int end = nl < 0 ? body.Length : nl;
                LayoutParagraph(body, p, end, _width, lines);
                if (nl < 0)
                    break;
                p = nl + 1;
            }
            if (lines.Count == 0)
            {
                LineData blank = new LineData();
                blank.Start = 0;
                blank.End = 0;
                lines.Add(blank);
            }

            int capacityFirst = Math.Max(layout.LineHeight, _height - titleBlock);
            PageData page = new PageData();
            page.First = true;
            layout.Pages.Add(page);
            int used = 0;
            int capacity = capacityFirst;

            for (int i = 0; i < lines.Count; i++)
            {
                LineData line = lines[i];
                if (used + layout.LineHeight > capacity && page.Lines.Count > 0)
                {
                    SetPageBounds(page);
                    page = new PageData();
                    page.First = false;
                    layout.Pages.Add(page);
                    used = 0;
                    capacity = _height;
                }
                if (page.Lines.Count == 0)
                    page.Start = line.Start;
                page.Lines.Add(line);
                used += layout.LineHeight;
            }
            SetPageBounds(page);
            return layout;
        }

        private static void SetPageBounds(PageData page)
        {
            if (page.Lines.Count == 0)
            {
                page.Start = 0;
                page.End = 0;
                return;
            }
            page.Start = page.Lines[0].Start;
            page.End = page.Lines[page.Lines.Count - 1].End;
        }

        private void LayoutParagraph(string body, int start, int end, int maxWidth,
            List<LineData> lines)
        {
            if (start >= end)
            {
                LineData blank = new LineData();
                blank.Start = start;
                blank.End = start;
                lines.Add(blank);
                return;
            }

            int contentStart = start;
            while (contentStart < end && IsSpaceLike(body[contentStart]))
                contentStart++;
            if (contentStart >= end)
            {
                LineData blank = new LineData();
                blank.Start = start;
                blank.End = end;
                lines.Add(blank);
                return;
            }

            // Normalize the leading indentation of every paragraph. This makes
            // plain TXT files (which often use four ASCII spaces or full-width
            // spaces) line up consistently, and gives unindented books the
            // familiar two-character first-line indent.
            start = contentStart;
            int indent = _indent;

            int before = lines.Count;
            int i = start;
            bool firstLine = true;
            int guard = 0;
            while (i < end)
            {
                guard++;
                if (guard > 100000)
                    break;

                int lineStart = i;
                int avail = maxWidth - (firstLine ? indent : 0);
                if (avail < _cjkWidth * 3)
                    avail = maxWidth;
                int scan = i;
                int width = 0;

                while (scan < end)
                {
                    int tokenLen = TokenLength(body, scan, end);
                    string token = body.Substring(scan, tokenLen);
                    int tokenWidth = TokenWidth(token);
                    if (width + tokenWidth > avail && scan > lineStart)
                        break;
                    if (width == 0 && tokenWidth > avail)
                    {
                        int take = 1;
                        while (scan + take < end &&
                            TokenWidth(body.Substring(scan, take + 1)) <= avail)
                            take++;
                        scan += Math.Max(1, take);
                        width = avail;
                        break;
                    }
                    width += tokenWidth;
                    scan += tokenLen;
                }

                if (scan == lineStart)
                    scan++;

                // Chinese typography: closing punctuation should not begin a line.
                if (scan < end && width + _cjkWidth <= avail + _cjkWidth &&
                    NoStartPunctuation.IndexOf(body[scan]) >= 0)
                {
                    scan++;
                }

                int drawEnd = scan;
                while (drawEnd > lineStart && body[drawEnd - 1] == ' ')
                    drawEnd--;
                string text = body.Substring(lineStart, drawEnd - lineStart);
                LineData line = new LineData();
                line.Text = text;
                line.Start = lineStart;
                line.End = scan;
                line.Indent = firstLine ? indent : 0;
                lines.Add(line);
                firstLine = false;

                i = scan;
                while (i < end && body[i] == ' ' && i > lineStart)
                    i++;
            }

            if (lines.Count == before)
            {
                LineData blank = new LineData();
                blank.Start = start;
                blank.End = end;
                lines.Add(blank);
            }
        }

        private int TokenWidth(string token)
        {
            if (token.Length == 0)
                return 0;
            if (token.Length == 1)
            {
                char c = token[0];
                if (c == ' ')
                    return _spaceWidth;
                if (c == '\u3000')
                    return _cjkWidth;
                if (IsWide(c))
                    return _cjkWidth;
            }
            if (token[0] == ' ')
                return _spaceWidth * token.Length;
            int w;
            if (_measured.TryGetValue(token, out w))
                return w;
            w = MeasureRaw(token);
            if (w < 1)
                w = _cjkWidth;
            if (_measured.Count < 30000)
                _measured[token] = w;
            return w;
        }

        private int MeasureRaw(string text)
        {
            return TextRenderer.MeasureText(text, _body,
                new Size(int.MaxValue, int.MaxValue),
                TextFormatFlags.NoPadding | TextFormatFlags.SingleLine |
                TextFormatFlags.NoPrefix).Width;
        }

        private int S(int px)
        {
            return (int)Math.Round(px * _scale);
        }

        private static bool IsSpaceLike(char c)
        {
            return c == ' ' || c == '\t' || c == '\u3000' || c == '\u00A0';
        }

        private static bool IsWide(char c)
        {
            return (c >= 0x1100 && c <= 0x115F) ||
                (c >= 0x2E80 && c <= 0xA4CF && c != 0x303F) ||
                (c >= 0xAC00 && c <= 0xD7A3) ||
                (c >= 0xF900 && c <= 0xFAFF) ||
                (c >= 0xFE10 && c <= 0xFE19) ||
                (c >= 0xFE30 && c <= 0xFE6F) ||
                (c >= 0xFF00 && c <= 0xFF60) ||
                (c >= 0xFFE0 && c <= 0xFFE6);
        }

        private static bool IsWordChar(char c)
        {
            if (c >= 128)
                return char.IsLetter(c) && c < 0x2E80;
            return char.IsLetterOrDigit(c) || c == '\'' || c == '-' || c == '_' ||
                c == '@' || c == '#' || c == '/' || c == '\\' || c == '.' ||
                c == '+' || c == '=' || c == '&' || c == '~';
        }

        private static int TokenLength(string text, int start, int end)
        {
            char c = text[start];
            if (char.IsHighSurrogate(c) && start + 1 < end &&
                char.IsLowSurrogate(text[start + 1]))
                return 2;
            if (c == ' ')
            {
                int i = start + 1;
                while (i < end && text[i] == ' ')
                    i++;
                return i - start;
            }
            if (c == '\u3000' || IsWide(c))
                return 1;
            if (IsWordChar(c))
            {
                int i = start + 1;
                while (i < end)
                {
                    char n = text[i];
                    if (n == ' ' || n == '\u3000' || IsWide(n))
                        break;
                    if (!IsWordChar(n))
                        break;
                    if (char.IsHighSurrogate(n))
                        break;
                    i++;
                }
                return i - start;
            }
            return 1;
        }
    }
}
