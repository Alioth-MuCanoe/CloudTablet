using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace LightReader
{
    internal sealed class ChapterInfo
    {
        public string Title;
        public int Start;
        public int Length;
        public int Number;

        public int End
        {
            get { return Start + Length; }
        }
    }

    internal sealed class BookData
    {
        public string Path;
        public string Title;
        public string Author = "";
        public string CoverPath = "";
        public string Format = "TXT";
        public string Text;
        public string EncodingName;
        public List<ChapterInfo> Chapters = new List<ChapterInfo>();

        public int ChapterIndexForOffset(int offset)
        {
            if (Chapters.Count == 0)
                return 0;
            if (offset <= Chapters[0].Start)
                return 0;
            int lo = 0;
            int hi = Chapters.Count - 1;
            while (lo < hi)
            {
                int mid = (lo + hi + 1) / 2;
                if (Chapters[mid].Start <= offset)
                    lo = mid;
                else
                    hi = mid - 1;
            }
            return lo;
        }
    }

    internal sealed class BookMetadata
    {
        public string Path = "";
        public string Title = "";
        public string Author = "";
        public string CoverPath = "";
        public string Format = "TXT";
        public long Size;
    }

    internal static class BookLoader
    {
        private const int MaxFileBytes = 96 * 1024 * 1024;
        private const int MaxEpubBytes = 256 * 1024 * 1024;
        private const int MaxChapterChars = 16000;
        private const int SplitTargetChars = 10500;

        private static readonly Regex HeadingRegex = new Regex(
            @"^\s*(?:" +
            @"第\s*[0-9零〇一二三四五六七八九十百千万两]{1,12}\s*[章节回卷部集篇]" +
            @"|序章|序言|自序|前言|楔子|引子|尾声|后记|终章|番外|外传" +
            @"|chapter\s+[0-9ivxlcdm]+" +
            @"|prologue|epilogue" +
            @")(?:\s*[：:、.．·\-—]?\s*.*)?$",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private static readonly string CommonChinese =
            "的一是在不了有和人这中大为上个国我以要他时来用们生到作地于出就分对成会可主发年动同工也能下过子说产种面而方后多定行学法所民得经十三之进着等部度家电力里如水化高自二理起小物现实加量都两体制机当使点从业本去把性好应开它合还因由其些然前外天政四日那社义事平形相全表间样与关各重新线内数正心反你明看原又么利比或但质气第向道命此变条只没结解问意建月公无系军很情者最立代想已通并提直题党程展五果料象员革位入常文总次品式活设及管特件长求老头基资边流路级少图山统接知较将组见计别她手角期根论运农指几九区强放决西被干做必战先回则任取据处理世车价";

        public static BookData Load(string path)
        {
            FileInfo fi = new FileInfo(path);
            if (!fi.Exists)
                throw new FileNotFoundException("找不到这个文件。", path);
            if (fi.Length <= 0)
                throw new InvalidDataException("文件是空的。");

            if (Path.GetExtension(path).Equals(".epub", StringComparison.OrdinalIgnoreCase))
            {
                if (fi.Length > MaxEpubBytes)
                    throw new InvalidDataException("EPUB 超过 256 MB，文件过大。");
                return EpubLoader.Load(Path.GetFullPath(path));
            }

            if (fi.Length > MaxFileBytes)
                throw new InvalidDataException("文件超过 96 MB，建议先按章节拆分成多个 TXT。");

            byte[] bytes = File.ReadAllBytes(path);
            string encodingName;
            string text = Decode(bytes, out encodingName);
            text = Normalize(text);
            if (text.Trim().Length == 0)
                throw new InvalidDataException("文件里没有可显示的文本。");

            BookData book = new BookData();
            book.Path = Path.GetFullPath(path);
            book.Title = Path.GetFileNameWithoutExtension(path).Trim();
            if (book.Title.Length == 0)
                book.Title = "未命名";
            book.Format = ReaderSettings.GuessFormat(path);
            book.Text = text;
            book.EncodingName = encodingName;
            book.Chapters = ParseChapters(text);
            if (book.Chapters.Count == 0)
            {
                ChapterInfo only = new ChapterInfo();
                only.Title = "正文";
                only.Start = 0;
                only.Length = text.Length;
                only.Number = 1;
                book.Chapters.Add(only);
            }
            return book;
        }

        public static BookMetadata LoadMetadata(string path)
        {
            FileInfo fi = new FileInfo(path);
            if (!fi.Exists)
                throw new FileNotFoundException("找不到这个文件。", path);
            if (fi.Length <= 0)
                throw new InvalidDataException("文件是空的。");

            if (Path.GetExtension(path).Equals(".epub", StringComparison.OrdinalIgnoreCase))
            {
                if (fi.Length > MaxEpubBytes)
                    throw new InvalidDataException("EPUB 超过 256 MB，文件过大。");
                return EpubLoader.ReadMetadata(Path.GetFullPath(path));
            }

            BookMetadata meta = new BookMetadata();
            meta.Path = Path.GetFullPath(path);
            meta.Title = Path.GetFileNameWithoutExtension(path).Trim();
            if (meta.Title.Length == 0)
                meta.Title = "未命名";
            meta.Format = ReaderSettings.GuessFormat(path);
            meta.Size = fi.Length;
            return meta;
        }

        private static string Normalize(string text)
        {
            if (text.Length > 0 && text[0] == '\uFEFF')
                text = text.Substring(1);
            text = text.Replace("\r\n", "\n").Replace('\r', '\n');
            text = text.Replace("\t", "    ");
            text = text.Replace("\0", "");
            text = text.Replace("\u000B", "\n");
            text = text.Replace("\u000C", "\n");
            return text;
        }

        private static string Decode(byte[] b, out string encodingName)
        {
            encodingName = "UTF-8";
            if (b.Length >= 3 && b[0] == 0xEF && b[1] == 0xBB && b[2] == 0xBF)
            {
                encodingName = "UTF-8";
                return Encoding.UTF8.GetString(b, 3, b.Length - 3);
            }
            if (b.Length >= 2 && b[0] == 0xFF && b[1] == 0xFE)
            {
                encodingName = "UTF-16 LE";
                return Encoding.Unicode.GetString(b, 2, b.Length - 2);
            }
            if (b.Length >= 2 && b[0] == 0xFE && b[1] == 0xFF)
            {
                encodingName = "UTF-16 BE";
                return Encoding.BigEndianUnicode.GetString(b, 2, b.Length - 2);
            }

            int sample = Math.Min(b.Length, 8192);
            int evenZero = 0;
            int oddZero = 0;
            for (int i = 0; i + 1 < sample; i += 2)
            {
                if (b[i] == 0) evenZero++;
                if (b[i + 1] == 0) oddZero++;
            }
            int pairs = Math.Max(1, sample / 2);
            if (oddZero > pairs / 5 && oddZero > evenZero * 4)
            {
                encodingName = "UTF-16 LE";
                return Encoding.Unicode.GetString(b);
            }
            if (evenZero > pairs / 5 && evenZero > oddZero * 4)
            {
                encodingName = "UTF-16 BE";
                return Encoding.BigEndianUnicode.GetString(b);
            }

            string utf8 = TryDecode(b, new UTF8Encoding(false, true));
            if (utf8 != null && !LooksBroken(utf8))
            {
                encodingName = "UTF-8";
                return utf8;
            }

            string gb = TryDecode(b, CodePage(54936) ?? CodePage(936));
            string big5 = TryDecode(b, CodePage(950));
            if (gb == null && big5 == null)
            {
                encodingName = "系统默认";
                return Encoding.Default.GetString(b);
            }
            if (gb == null)
            {
                encodingName = "BIG5";
                return big5;
            }
            if (big5 == null)
            {
                encodingName = "GB18030";
                return gb;
            }

            double gs = ChineseScore(gb);
            double bs = ChineseScore(big5);
            if (bs > gs + 0.035)
            {
                encodingName = "BIG5";
                return big5;
            }
            encodingName = "GB18030";
            return gb;
        }

        private static Encoding CodePage(int cp)
        {
            try
            {
                return Encoding.GetEncoding(cp, EncoderFallback.ExceptionFallback,
                    DecoderFallback.ExceptionFallback);
            }
            catch
            {
                return null;
            }
        }

        private static string TryDecode(byte[] bytes, Encoding enc)
        {
            if (enc == null)
                return null;
            try
            {
                return enc.GetString(bytes);
            }
            catch
            {
                return null;
            }
        }

        private static bool LooksBroken(string s)
        {
            int bad = 0;
            int control = 0;
            int sample = Math.Min(s.Length, 20000);
            for (int i = 0; i < sample; i++)
            {
                char c = s[i];
                if (c == '\uFFFD')
                    bad++;
                else if (char.IsControl(c) && c != '\n' && c != '\r' && c != '\t')
                    control++;
            }
            return bad > 2 || control > Math.Max(4, sample / 100);
        }

        private static double ChineseScore(string s)
        {
            int cjk = 0;
            int common = 0;
            int step = Math.Max(1, s.Length / 30000);
            for (int i = 0; i < s.Length; i += step)
            {
                char c = s[i];
                if (c >= 0x4E00 && c <= 0x9FFF)
                {
                    cjk++;
                    if (CommonChinese.IndexOf(c) >= 0)
                        common++;
                }
            }
            if (cjk == 0)
                return 0;
            return (double)common / cjk;
        }

        private sealed class Heading
        {
            public int LineStart;
            public int BodyStart;
            public string Title;
        }

        private static List<ChapterInfo> ParseChapters(string text)
        {
            List<Heading> headings = new List<Heading>();
            int pos = 0;
            while (pos <= text.Length)
            {
                int nl = text.IndexOf('\n', pos);
                int lineEnd = nl < 0 ? text.Length : nl;
                string raw = text.Substring(pos, lineEnd - pos);
                string t = raw.Trim();
                if (IsHeading(t))
                {
                    Heading h = new Heading();
                    h.LineStart = pos;
                    h.BodyStart = lineEnd < text.Length ? lineEnd + 1 : lineEnd;
                    h.Title = CleanTitle(t);
                    headings.Add(h);
                }
                if (nl < 0)
                    break;
                pos = nl + 1;
            }

            List<ChapterInfo> chapters = new List<ChapterInfo>();
            if (headings.Count == 0)
            {
                AddVirtualSections(chapters, text, 0, text.Length);
                return chapters;
            }

            if (headings[0].LineStart > 0)
            {
                string preface = text.Substring(0, headings[0].LineStart);
                string trimmed = preface.Trim();
                if (trimmed.Length > 0 && !LooksLikeMetadata(trimmed))
                {
                    AddChapter(chapters, "卷首", text, 0, headings[0].LineStart, false);
                }
            }

            for (int i = 0; i < headings.Count; i++)
            {
                int start = headings[i].BodyStart;
                int end = i + 1 < headings.Count ? headings[i + 1].LineStart : text.Length;
                if (end < start)
                    end = start;
                while (end > start && char.IsWhiteSpace(text[end - 1]))
                    end--;
                if (end == start && chapters.Count > 0)
                    continue;
                AddChapter(chapters, headings[i].Title, text, start, end - start, true);
            }

            if (chapters.Count == 0)
            {
                ChapterInfo only = new ChapterInfo();
                only.Title = "正文";
                only.Start = 0;
                only.Length = text.Length;
                only.Number = 1;
                chapters.Add(only);
            }
            return chapters;
        }

        private static bool LooksLikeMetadata(string preface)
        {
            if (preface.Length > 180)
                return false;
            if (preface.IndexOf("书名", StringComparison.Ordinal) >= 0 ||
                preface.IndexOf("作者", StringComparison.Ordinal) >= 0 ||
                preface.IndexOf("简介", StringComparison.Ordinal) >= 0 ||
                preface.IndexOf("内容简介", StringComparison.Ordinal) >= 0 ||
                preface.IndexOf("标签", StringComparison.Ordinal) >= 0)
                return true;
            return false;
        }

        private static bool IsHeading(string t)
        {            if (t.Length == 0 || t.Length > 60)
                return false;
            if (t.EndsWith("。") || t.EndsWith("！") || t.EndsWith("？") ||
                t.EndsWith("!") || t.EndsWith("?"))
                return false;
            if (!HeadingRegex.IsMatch(t))
                return false;
            // A line that is only “第一章” is still a heading; a line of prose
            // that merely contains the word is not, because the match is anchored.
            return true;
        }

        private static string CleanTitle(string t)
        {
            t = t.Trim();
            if (t.Length > 60)
                t = t.Substring(0, 60);
            return t;
        }

        private static void AddChapter(List<ChapterInfo> chapters, string title,
            string text, int start, int length, bool splitLarge)
        {
            if (start < 0) start = 0;
            if (length < 0) length = 0;
            if (start + length > text.Length)
                length = text.Length - start;
            if (length == 0)
            {
                AddOne(chapters, title, start, 0);
                return;
            }
            if (!splitLarge || length <= MaxChapterChars)
            {
                AddOne(chapters, title, start, length);
                return;
            }

            int at = start;
            int remaining = length;
            int part = 0;
            while (remaining > 0)
            {
                part++;
                int take = Math.Min(remaining, SplitTargetChars);
                if (remaining > MaxChapterChars)
                {
                    int limit = Math.Min(remaining, MaxChapterChars);
                    int split = FindSplit(text, at, limit, take);
                    if (split > 0)
                        take = split;
                }
                if (take <= 0)
                    take = Math.Min(remaining, SplitTargetChars);
                string partTitle = title;
                if (part > 1)
                    partTitle = title + (part == 2 ? "（续）" : "（续" + part + "）");
                AddOne(chapters, partTitle, at, take);
                at += take;
                remaining -= take;
                while (at < text.Length && text[at] == '\n' && remaining > 0)
                {
                    at++;
                    remaining--;
                }
            }
        }

        private static void AddOne(List<ChapterInfo> chapters, string title, int start, int length)
        {
            ChapterInfo c = new ChapterInfo();
            c.Title = string.IsNullOrEmpty(title) ? "正文" : title;
            c.Start = start;
            c.Length = length;
            c.Number = chapters.Count + 1;
            chapters.Add(c);
        }

        private static int FindSplit(string text, int start, int maxTake, int target)
        {
            int lo = Math.Min(maxTake - 1, Math.Max(1, target));
            int hi = Math.Min(maxTake - 1, text.Length - start - 1);
            if (hi <= 0)
                return 0;
            int pos = text.LastIndexOf('\n', start + hi, hi);
            if (pos >= start + 800)
                return pos - start;
            pos = text.IndexOf('\n', start + lo);
            if (pos >= 0 && pos < start + hi)
                return pos - start;
            return hi;
        }

        private static void AddVirtualSections(List<ChapterInfo> chapters, string text,
            int start, int length)
        {
            if (length <= 14000)
            {
                AddOne(chapters, "正文", start, length);
                return;
            }
            int at = start;
            int remaining = length;
            int n = 0;
            while (remaining > 0)
            {
                n++;
                int take = Math.Min(remaining, 9000);
                if (remaining > 12000)
                {
                    int split = FindSplit(text, at, 12000, take);
                    if (split > 0)
                        take = split;
                }
                if (take <= 0)
                    take = Math.Min(remaining, 9000);
                AddOne(chapters, "第 " + n + " 节", at, take);
                at += take;
                remaining -= take;
                while (at < text.Length && text[at] == '\n' && remaining > 0)
                {
                    at++;
                    remaining--;
                }
            }
        }
    }
}
