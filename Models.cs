using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Xml;

namespace LightReader
{
    internal sealed class BookMark
    {
        public string Path = "";
        public string Title = "";
        public string Author = "";
        public string CoverPath = "";
        public string Format = "TXT";
        public int Offset;
        public double Percent;
        public DateTime LastRead = DateTime.MinValue;
        public DateTime Added = DateTime.MinValue;
        public long Size;

        public DateTime EffectiveDate
        {
            get
            {
                if (LastRead > DateTime.MinValue)
                    return LastRead;
                return Added;
            }
        }

        public BookMark Clone()
        {
            BookMark b = new BookMark();
            b.Path = Path;
            b.Title = Title;
            b.Author = Author;
            b.CoverPath = CoverPath;
            b.Format = Format;
            b.Offset = Offset;
            b.Percent = Percent;
            b.LastRead = LastRead;
            b.Added = Added;
            b.Size = Size;
            return b;
        }
    }

    internal sealed class ReaderSettings
    {
        public string Theme = ThemeCatalog.LightDefault;
        public string LastLightTheme = ThemeCatalog.LightDefault;
        public string LastDarkTheme = ThemeCatalog.DarkDefault;
        public string CustomColor = "#F7F0DD";
        public string BackgroundImage = "";
        public string FontFamily = "";
        public float FontSize = 15f;
        public float LineSpacing = 1.7f;
        public int WindowWidth;
        public int WindowHeight;
        public bool Maximized;
        public bool Hints = true;
        public List<BookMark> Books = new List<BookMark>();

        private static string _settingsFolder;

        public static string SettingsFolder
        {
            get
            {
                if (_settingsFolder != null)
                    return _settingsFolder;
                string root = Environment.GetFolderPath(
                    Environment.SpecialFolder.ApplicationData);
                if (string.IsNullOrEmpty(root))
                    root = Path.GetDirectoryName(
                        System.Windows.Forms.Application.ExecutablePath);
                string dir = Path.Combine(root, "CloudTablet");
                string oldDir = Path.Combine(root, "LightReader");
                try
                {
                    if (!Directory.Exists(dir) && Directory.Exists(oldDir))
                        CopyDirectory(oldDir, dir);
                    Directory.CreateDirectory(dir);
                }
                catch
                {
                }
                _settingsFolder = dir;
                return dir;
            }
        }

        private static void CopyDirectory(string source, string target)
        {
            Directory.CreateDirectory(target);
            string[] files = Directory.GetFiles(source);
            for (int i = 0; i < files.Length; i++)
                File.Copy(files[i], Path.Combine(target,
                    Path.GetFileName(files[i])), true);
            string[] dirs = Directory.GetDirectories(source);
            for (int i = 0; i < dirs.Length; i++)
                CopyDirectory(dirs[i], Path.Combine(target,
                    Path.GetFileName(dirs[i])));
        }

        public static string SettingsFile
        {
            get { return Path.Combine(SettingsFolder, "settings.xml"); }
        }

        public static string CoversFolder
        {
            get { return Path.Combine(SettingsFolder, "covers"); }
        }

        public static ReaderSettings Load()
        {
            ReaderSettings s = new ReaderSettings();
            try
            {
                string file = SettingsFile;
                if (!File.Exists(file))
                    return s;

                XmlDocument doc = new XmlDocument();
                doc.Load(file);
                XmlElement root = doc.DocumentElement;
                if (root == null)
                    return s;

                XmlElement g = root.SelectSingleNode("general") as XmlElement;
                if (g != null)
                {
                    s.Theme = Attribute(g, "theme", s.Theme);
                    s.LastLightTheme = Attribute(g, "lastLight", s.LastLightTheme);
                    s.LastDarkTheme = Attribute(g, "lastDark", s.LastDarkTheme);
                    s.CustomColor = Attribute(g, "customColor", s.CustomColor);
                    s.BackgroundImage = Attribute(g, "backgroundImage", "");
                    s.FontFamily = Attribute(g, "font", s.FontFamily);
                    s.FontSize = FloatAttribute(g, "size", s.FontSize);
                    s.LineSpacing = FloatAttribute(g, "line", s.LineSpacing);
                    s.WindowWidth = IntAttribute(g, "width", 0);
                    s.WindowHeight = IntAttribute(g, "height", 0);
                    s.Maximized = BoolAttribute(g, "maximized", false);
                    s.Hints = BoolAttribute(g, "hints", true);
                }

                XmlNodeList nodes = root.SelectNodes("books/book");
                if (nodes != null)
                {
                    for (int i = 0; i < nodes.Count && s.Books.Count < 200; i++)
                    {
                        XmlElement e = nodes[i] as XmlElement;
                        if (e == null)
                            continue;
                        string path = Attribute(e, "path", "");
                        if (string.IsNullOrEmpty(path))
                            continue;
                        BookMark b = new BookMark();
                        b.Path = path;
                        b.Title = Attribute(e, "title", Path.GetFileNameWithoutExtension(path));
                        b.Author = Attribute(e, "author", "");
                        b.CoverPath = Attribute(e, "cover", "");
                        b.Format = Attribute(e, "format", GuessFormat(path));
                        b.Offset = IntAttribute(e, "offset", 0);
                        b.Percent = FloatAttribute(e, "percent", 0f);
                        b.Size = LongAttribute(e, "size", 0);
                        b.LastRead = DateAttribute(e, "lastRead");
                        b.Added = DateAttribute(e, "added");
                        s.Books.Add(b);
                    }
                }
            }
            catch
            {
                // A damaged settings file must never stop the reader from opening.
            }
            s.Normalize();
            return s;
        }

        public void Normalize()
        {
            if (string.Equals(Theme, "custom", StringComparison.OrdinalIgnoreCase))
            {
                if (!Ui.IsValidHtmlColor(CustomColor))
                    Theme = ThemeCatalog.LightDefault;
            }
            else if (ThemeCatalog.Get(Theme) == null)
            {
                Theme = ThemeCatalog.LightDefault;
            }
            if (ThemeCatalog.Get(LastLightTheme) == null ||
                ThemeCatalog.Get(LastLightTheme).IsDark)
                LastLightTheme = ThemeCatalog.LightDefault;
            if (ThemeCatalog.Get(LastDarkTheme) == null ||
                !ThemeCatalog.Get(LastDarkTheme).IsDark)
                LastDarkTheme = ThemeCatalog.DarkDefault;
            if (!Ui.IsValidHtmlColor(CustomColor))
                CustomColor = "#F7F0DD";

            if (FontSize < 10f) FontSize = 15f;
            if (FontSize > 36f) FontSize = 36f;
            if (LineSpacing < 1.2f) LineSpacing = 1.7f;
            if (LineSpacing > 2.4f) LineSpacing = 2.4f;
            if (WindowWidth < 640 || WindowWidth > 10000) WindowWidth = 0;
            if (WindowHeight < 480 || WindowHeight > 10000) WindowHeight = 0;
            if (Books == null) Books = new List<BookMark>();

            for (int i = Books.Count - 1; i >= 0; i--)
            {
                BookMark b = Books[i];
                if (b == null || string.IsNullOrEmpty(b.Path))
                {
                    Books.RemoveAt(i);
                    continue;
                }
                if (string.IsNullOrEmpty(b.Format))
                    b.Format = GuessFormat(b.Path);
                if (b.Added == DateTime.MinValue)
                    b.Added = b.LastRead > DateTime.MinValue ? b.LastRead : DateTime.Now;
                if (b.Title == null) b.Title = "";
                if (b.Author == null) b.Author = "";
                if (b.CoverPath == null) b.CoverPath = "";
            }
            SortBooks();
            if (Books.Count > 200)
                Books.RemoveRange(200, Books.Count - 200);
        }

        public void SortBooks()
        {
            Books.Sort(delegate(BookMark a, BookMark b)
            {
                int c = b.EffectiveDate.CompareTo(a.EffectiveDate);
                if (c != 0) return c;
                return string.Compare(a.Title, b.Title, StringComparison.CurrentCultureIgnoreCase);
            });
        }

        public BookMark FindBook(string path)
        {
            if (string.IsNullOrEmpty(path))
                return null;
            for (int i = 0; i < Books.Count; i++)
            {
                if (string.Equals(Books[i].Path, path, StringComparison.OrdinalIgnoreCase))
                    return Books[i];
            }
            return null;
        }

        public BookMark UpsertBook(string path, string title, string author,
            string coverPath, string format, long size)
        {
            BookMark b = FindBook(path);
            if (b == null)
            {
                b = new BookMark();
                b.Path = path;
                b.Added = DateTime.Now;
                Books.Add(b);
            }
            if (!string.IsNullOrEmpty(title))
                b.Title = title;
            if (!string.IsNullOrEmpty(author))
                b.Author = author;
            if (!string.IsNullOrEmpty(coverPath))
                b.CoverPath = coverPath;
            if (!string.IsNullOrEmpty(format))
                b.Format = format;
            if (size > 0)
                b.Size = size;
            if (b.Added == DateTime.MinValue)
                b.Added = DateTime.Now;
            SortBooks();
            return b;
        }

        public BookMark TouchBook(string path, string title, int offset, double percent)
        {
            BookMark b = FindBook(path);
            if (b == null)
            {
                b = new BookMark();
                b.Path = path;
                b.Added = DateTime.Now;
                b.Format = GuessFormat(path);
                Books.Add(b);
            }
            if (!string.IsNullOrEmpty(title))
                b.Title = title;
            b.Offset = Math.Max(0, offset);
            b.Percent = Math.Max(0, Math.Min(1, percent));
            b.LastRead = DateTime.Now;
            if (b.Added == DateTime.MinValue)
                b.Added = b.LastRead;
            SortBooks();
            return b;
        }

        public void RemoveBook(string path)
        {
            for (int i = Books.Count - 1; i >= 0; i--)
            {
                if (string.Equals(Books[i].Path, path, StringComparison.OrdinalIgnoreCase))
                    Books.RemoveAt(i);
            }
        }

        public void Save()
        {
            try
            {
                Directory.CreateDirectory(SettingsFolder);
                XmlDocument doc = new XmlDocument();
                XmlElement root = doc.CreateElement("lightreader");
                root.SetAttribute("version", "2");
                doc.AppendChild(root);

                XmlElement g = doc.CreateElement("general");
                g.SetAttribute("theme", Theme);
                g.SetAttribute("lastLight", LastLightTheme);
                g.SetAttribute("lastDark", LastDarkTheme);
                g.SetAttribute("customColor", CustomColor ?? "#F7F0DD");
                g.SetAttribute("backgroundImage", BackgroundImage ?? "");
                g.SetAttribute("font", FontFamily ?? "");
                g.SetAttribute("size", FontSize.ToString("0.###", CultureInfo.InvariantCulture));
                g.SetAttribute("line", LineSpacing.ToString("0.###", CultureInfo.InvariantCulture));
                g.SetAttribute("width", WindowWidth.ToString(CultureInfo.InvariantCulture));
                g.SetAttribute("height", WindowHeight.ToString(CultureInfo.InvariantCulture));
                g.SetAttribute("maximized", Maximized ? "true" : "false");
                g.SetAttribute("hints", Hints ? "true" : "false");
                root.AppendChild(g);

                XmlElement books = doc.CreateElement("books");
                for (int i = 0; i < Books.Count; i++)
                {
                    BookMark b = Books[i];
                    XmlElement e = doc.CreateElement("book");
                    e.SetAttribute("path", b.Path ?? "");
                    e.SetAttribute("title", b.Title ?? "");
                    e.SetAttribute("author", b.Author ?? "");
                    e.SetAttribute("cover", b.CoverPath ?? "");
                    e.SetAttribute("format", b.Format ?? "");
                    e.SetAttribute("offset", Math.Max(0, b.Offset).ToString(CultureInfo.InvariantCulture));
                    e.SetAttribute("percent", Math.Max(0, Math.Min(1, b.Percent))
                        .ToString("0.######", CultureInfo.InvariantCulture));
                    e.SetAttribute("lastRead", b.LastRead.ToString("o", CultureInfo.InvariantCulture));
                    e.SetAttribute("added", b.Added.ToString("o", CultureInfo.InvariantCulture));
                    e.SetAttribute("size", Math.Max(0, b.Size).ToString(CultureInfo.InvariantCulture));
                    books.AppendChild(e);
                }
                root.AppendChild(books);

                string tmp = SettingsFile + ".tmp";
                doc.Save(tmp);
                File.Copy(tmp, SettingsFile, true);
                File.Delete(tmp);
            }
            catch
            {
                // Losing a bookmark is preferable to crashing while closing.
            }
        }

        public static string GuessFormat(string path)
        {
            string ext = Path.GetExtension(path ?? "");
            if (ext.Equals(".epub", StringComparison.OrdinalIgnoreCase))
                return "EPUB";
            if (ext.Equals(".md", StringComparison.OrdinalIgnoreCase))
                return "MD";
            return "TXT";
        }

        private static string Attribute(XmlElement e, string name, string fallback)
        {
            string v = e.GetAttribute(name);
            return string.IsNullOrEmpty(v) ? fallback : v;
        }

        private static int IntAttribute(XmlElement e, string name, int fallback)
        {
            int v;
            if (int.TryParse(e.GetAttribute(name), NumberStyles.Integer,
                CultureInfo.InvariantCulture, out v))
                return v;
            return fallback;
        }

        private static long LongAttribute(XmlElement e, string name, long fallback)
        {
            long v;
            if (long.TryParse(e.GetAttribute(name), NumberStyles.Integer,
                CultureInfo.InvariantCulture, out v))
                return v;
            return fallback;
        }

        private static float FloatAttribute(XmlElement e, string name, float fallback)
        {
            float v;
            if (float.TryParse(e.GetAttribute(name), NumberStyles.Float,
                CultureInfo.InvariantCulture, out v))
                return v;
            return fallback;
        }

        private static bool BoolAttribute(XmlElement e, string name, bool fallback)
        {
            string v = e.GetAttribute(name);
            if (string.IsNullOrEmpty(v))
                return fallback;
            return string.Equals(v, "true", StringComparison.OrdinalIgnoreCase) || v == "1";
        }

        private static DateTime DateAttribute(XmlElement e, string name)
        {
            string v = e.GetAttribute(name);
            DateTime t;
            if (!string.IsNullOrEmpty(v) &&
                DateTime.TryParse(v, CultureInfo.InvariantCulture,
                    DateTimeStyles.RoundtripKind, out t))
                return t;
            return DateTime.MinValue;
        }
    }
}
