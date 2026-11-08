using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;

namespace LightReader
{
    internal static class EpubLoader
    {
        private const int MaxChapterChars = 16000;
        private const int SplitTargetChars = 12000;

        private sealed class ManifestItem
        {
            public string Id = "";
            public string Href = "";
            public string MediaType = "";
            public string Properties = "";
        }

        private sealed class Package
        {
            public string OpfPath = "";
            public string OpfDir = "";
            public string Title = "";
            public string Author = "";
            public string CoverHref = "";
            public List<ManifestItem> Items = new List<ManifestItem>();
            public List<string> Spine = new List<string>();
            public Dictionary<string, ZipArchiveEntry> Entries;
        }

        public static BookData Load(string path)
        {
            using (FileStream fs = Open(path))
            using (ZipArchive zip = new ZipArchive(fs, ZipArchiveMode.Read, false,
                Encoding.UTF8))
            {
                Package pkg = ReadPackage(zip, path);
                BookData book = new BookData();
                book.Path = Path.GetFullPath(path);
                book.Title = string.IsNullOrEmpty(pkg.Title)
                    ? Path.GetFileNameWithoutExtension(path) : pkg.Title;
                book.Author = pkg.Author ?? "";
                book.Format = "EPUB";
                book.EncodingName = "EPUB";
                book.CoverPath = ExtractCover(zip, pkg, path);

                StringBuilder sb = new StringBuilder();
                List<ChapterInfo> chapters = new List<ChapterInfo>();
                int index = 0;
                for (int i = 0; i < pkg.Spine.Count; i++)
                {
                    ManifestItem item = FindItem(pkg, pkg.Spine[i]);
                    if (item == null || !IsHtml(item))
                        continue;
                    string html = ReadEntryText(zip, Resolve(pkg.OpfDir, item.Href));
                    if (html == null)
                        continue;
                    string title;
                    string text = ExtractText(html, out title);
                    if (string.IsNullOrEmpty(text) || text.Trim().Length == 0)
                        continue;
                    index++;
                    if (string.IsNullOrEmpty(title))
                        title = "第 " + index + " 章";
                    if (text.Trim() == "[图片]" && chapters.Count == 0)
                        continue;
                    AddChapterParts(chapters, sb, title, text);
                }

                if (chapters.Count == 0 && pkg.Items != null)
                {
                    // Some books have an empty or missing spine; fall back to
                    // every XHTML item in manifest order.
                    for (int i = 0; i < pkg.Items.Count; i++)
                    {
                        ManifestItem item = pkg.Items[i];
                        if (!IsHtml(item))
                            continue;
                        string html = ReadEntryText(zip, Resolve(pkg.OpfDir, item.Href));
                        if (html == null)
                            continue;
                        string title;
                        string text = ExtractText(html, out title);
                        if (string.IsNullOrEmpty(text) || text.Trim().Length == 0)
                            continue;
                        index++;
                        if (string.IsNullOrEmpty(title))
                            title = "第 " + index + " 章";
                        AddChapterParts(chapters, sb, title, text);
                    }
                }

                if (chapters.Count == 0)
                    throw new InvalidDataException(
                        "这个 EPUB 里没有找到可读的正文（可能是漫画、固定版式或受 DRM 保护）。");
                book.Text = sb.ToString();
                book.Chapters = chapters;
                return book;
            }
        }

        public static BookMetadata ReadMetadata(string path)
        {
            using (FileStream fs = Open(path))
            using (ZipArchive zip = new ZipArchive(fs, ZipArchiveMode.Read, false,
                Encoding.UTF8))
            {
                Package pkg = ReadPackage(zip, path);
                BookMetadata meta = new BookMetadata();
                meta.Path = Path.GetFullPath(path);
                meta.Title = string.IsNullOrEmpty(pkg.Title)
                    ? Path.GetFileNameWithoutExtension(path) : pkg.Title;
                meta.Author = pkg.Author ?? "";
                meta.Format = "EPUB";
                meta.Size = new FileInfo(path).Length;
                meta.CoverPath = ExtractCover(zip, pkg, path);
                return meta;
            }
        }

        private static FileStream Open(string path)
        {
            return new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        }

        private static Package ReadPackage(ZipArchive zip, string bookPath)
        {
            Dictionary<string, ZipArchiveEntry> map =
                new Dictionary<string, ZipArchiveEntry>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < zip.Entries.Count; i++)
            {
                ZipArchiveEntry e = zip.Entries[i];
                if (string.IsNullOrEmpty(e.Name))
                    continue;
                string key = NormalizePath(e.FullName);
                if (!map.ContainsKey(key))
                    map[key] = e;
            }

            string opfPath = null;
            ZipArchiveEntry container = GetEntry(map, "META-INF/container.xml");
            if (container != null)
            {
                try
                {
                    XmlDocument c = LoadXml(container);
                    XmlElement rootfile = FindFirst(c.DocumentElement, "rootfile");
                    if (rootfile != null)
                        opfPath = rootfile.GetAttribute("full-path");
                }
                catch
                {
                }
            }
            if (string.IsNullOrEmpty(opfPath))
            {
                for (int i = 0; i < zip.Entries.Count; i++)
                {
                    if (zip.Entries[i].FullName.EndsWith(".opf",
                        StringComparison.OrdinalIgnoreCase))
                    {
                        opfPath = zip.Entries[i].FullName;
                        break;
                    }
                }
            }
            if (string.IsNullOrEmpty(opfPath))
                throw new InvalidDataException("这个 EPUB 缺少 OPF 文件，可能已损坏。");
            opfPath = NormalizePath(opfPath);

            ZipArchiveEntry opf = GetEntry(map, opfPath);
            if (opf == null)
                throw new InvalidDataException("EPUB 的 OPF 文件不存在。");

            XmlDocument doc = LoadXml(opf);
            Package pkg = new Package();
            pkg.OpfPath = opfPath;
            pkg.OpfDir = DirectoryOf(opfPath);
            pkg.Entries = map;
            XmlElement package = doc.DocumentElement;
            XmlElement metadata = FindFirst(package, "metadata");
            XmlElement manifest = FindFirst(package, "manifest");
            XmlElement spine = FindFirst(package, "spine");

            pkg.Title = TextOf(FindFirst(metadata, "title")).Trim();
            pkg.Author = TextOf(FindFirst(metadata, "creator")).Trim();

            if (manifest != null)
            {
                List<XmlElement> items = FindAll(manifest, "item");
                for (int i = 0; i < items.Count; i++)
                {
                    XmlElement e = items[i];
                    ManifestItem item = new ManifestItem();
                    item.Id = e.GetAttribute("id");
                    item.Href = e.GetAttribute("href");
                    item.MediaType = e.GetAttribute("media-type");
                    item.Properties = e.GetAttribute("properties");
                    if (!string.IsNullOrEmpty(item.Href))
                        pkg.Items.Add(item);
                }
            }
            if (spine != null)
            {
                List<XmlElement> refs = FindAll(spine, "itemref");
                for (int i = 0; i < refs.Count; i++)
                {
                    if (string.Equals(refs[i].GetAttribute("linear"), "no",
                        StringComparison.OrdinalIgnoreCase))
                        continue;
                    string idref = refs[i].GetAttribute("idref");
                    if (!string.IsNullOrEmpty(idref))
                        pkg.Spine.Add(idref);
                }
            }

            // EPUB 2 cover meta, EPUB 3 cover-image property, then a loose
            // fallback for books with non-standard metadata.
            string coverId = "";
            if (metadata != null)
            {
                List<XmlElement> metas = FindAll(metadata, "meta");
                for (int i = 0; i < metas.Count; i++)
                {
                    if (string.Equals(metas[i].GetAttribute("name"), "cover",
                        StringComparison.OrdinalIgnoreCase))
                    {
                        coverId = metas[i].GetAttribute("content");
                        break;
                    }
                }
            }
            ManifestItem cover = null;
            if (!string.IsNullOrEmpty(coverId))
                cover = FindItem(pkg, coverId);
            if (cover == null)
            {
                for (int i = 0; i < pkg.Items.Count; i++)
                {
                    if (pkg.Items[i].Properties.IndexOf("cover-image",
                        StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        cover = pkg.Items[i];
                        break;
                    }
                }
            }
            if (cover == null)
            {
                for (int i = 0; i < pkg.Items.Count; i++)
                {
                    ManifestItem item = pkg.Items[i];
                    if (item.MediaType.StartsWith("image/",
                        StringComparison.OrdinalIgnoreCase) &&
                        (item.Id.IndexOf("cover", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        item.Href.IndexOf("cover", StringComparison.OrdinalIgnoreCase) >= 0))
                    {
                        cover = item;
                        break;
                    }
                }
            }
            if (cover != null)
                pkg.CoverHref = Resolve(pkg.OpfDir, cover.Href);
            return pkg;
        }

        private static string ExtractCover(ZipArchive zip, Package pkg, string bookPath)
        {
            if (string.IsNullOrEmpty(pkg.CoverHref))
                return "";
            string existing = ImageLoader.CoverPathForBook(bookPath);
            if (File.Exists(existing))
                return existing;
            ZipArchiveEntry entry = GetEntry(pkg.Entries, pkg.CoverHref);
            if (entry == null)
                return "";
            string lower = pkg.CoverHref.ToLowerInvariant();
            if (lower.EndsWith(".svg") || lower.EndsWith(".gif"))
                return "";
            try
            {
                using (Stream s = entry.Open())
                using (MemoryStream ms = new MemoryStream())
                {
                    s.CopyTo(ms);
                    ms.Position = 0;
                    using (Bitmap bmp = ImageLoader.LoadScaled(ms, lower, 360, 480))
                        return ImageLoader.SaveCover(bmp, bookPath);
                }
            }
            catch
            {
                return "";
            }
        }

        private static void AddChapterParts(List<ChapterInfo> chapters,
            StringBuilder sb, string title, string text)
        {
            int at = 0;
            int part = 0;
            while (at < text.Length)
            {
                part++;
                int take = Math.Min(text.Length - at, SplitTargetChars);
                if (text.Length - at > MaxChapterChars)
                {
                    int limit = Math.Min(text.Length - at, MaxChapterChars);
                    int split = text.LastIndexOf('\n', at + limit - 1, limit);
                    if (split >= at + 2000)
                        take = split - at;
                }
                if (take <= 0) take = 1;
                string t = text.Substring(at, take).Trim('\n');
                if (t.Length > 0)
                {
                    ChapterInfo c = new ChapterInfo();
                    c.Title = part == 1 ? title : title + "（续）";
                    c.Start = sb.Length;
                    c.Length = t.Length;
                    c.Number = chapters.Count + 1;
                    chapters.Add(c);
                    sb.Append(t);
                    sb.Append("\n\n");
                }
                at += take;
                while (at < text.Length && text[at] == '\n') at++;
            }
        }

        private static bool IsHtml(ManifestItem item)
        {
            string type = item.MediaType ?? "";
            if (type.IndexOf("xhtml", StringComparison.OrdinalIgnoreCase) >= 0 ||
                type.IndexOf("html", StringComparison.OrdinalIgnoreCase) >= 0)
                return true;
            string href = (item.Href ?? "").ToLowerInvariant();
            return href.EndsWith(".xhtml") || href.EndsWith(".html") ||
                href.EndsWith(".htm");
        }

        private static ManifestItem FindItem(Package pkg, string id)
        {
            for (int i = 0; i < pkg.Items.Count; i++)
            {
                if (string.Equals(pkg.Items[i].Id, id, StringComparison.Ordinal))
                    return pkg.Items[i];
            }
            return null;
        }

        private static string ExtractText(string html, out string title)
        {
            title = "";
            string body = html ?? "";
            Match bodyMatch = Regex.Match(body, @"<body\b[^>]*>(.*?)</body>",
                RegexOptions.IgnoreCase | RegexOptions.Singleline);
            if (bodyMatch.Success)
                body = bodyMatch.Groups[1].Value;

            title = FirstTagText(body, "h1");
            if (string.IsNullOrEmpty(title)) title = FirstTagText(body, "h2");
            if (string.IsNullOrEmpty(title)) title = FirstTagText(body, "h3");
            if (string.IsNullOrEmpty(title))
                title = FirstTagText(html, "title");
            title = StripTags(title).Trim();
            if (title.Length > 60) title = title.Substring(0, 60);

            body = Regex.Replace(body, @"<!--.*?-->", "", RegexOptions.Singleline);
            body = Regex.Replace(body, @"<(script|style|head|svg|noscript)\b[^>]*>.*?</\1>",
                "", RegexOptions.IgnoreCase | RegexOptions.Singleline);
            body = Regex.Replace(body, @"<img\b[^>]*>", "\n[图片]\n",
                RegexOptions.IgnoreCase);
            body = Regex.Replace(body,
                @"</?(p|div|br|li|tr|h[1-6]|blockquote|section|article|figcaption|pre|hr)\b[^>]*>",
                "\n", RegexOptions.IgnoreCase);
            body = Regex.Replace(body, @"<[^>]+>", " ");
            body = WebUtility.HtmlDecode(body);
            body = body.Replace('\u00A0', ' ').Replace('\t', ' ');
            body = Regex.Replace(body, @"[ ]{2,}", " ");
            body = Regex.Replace(body, @" *\n *", "\n");
            body = Regex.Replace(body, @"\n{2,}", "\n");
            body = body.Trim();

            // Many EPUBs repeat the chapter heading both in <title>/<h1> and
            // at the start of the body. The reader already draws the title,
            // so drop the duplicate first line.
            if (!string.IsNullOrEmpty(title) && body.Length > 0)
            {
                int nl = body.IndexOf('\n');
                string first = (nl < 0 ? body : body.Substring(0, nl)).Trim();
                if (string.Equals(first, title.Trim(),
                    StringComparison.OrdinalIgnoreCase))
                {
                    body = nl < 0 ? "" : body.Substring(nl + 1).TrimStart('\n');
                }
            }
            return body.Trim();
        }

        private static string FirstTagText(string html, string tag)
        {
            Match m = Regex.Match(html ?? "", "<" + tag + @"\b[^>]*>(.*?)</" + tag + ">",
                RegexOptions.IgnoreCase | RegexOptions.Singleline);
            if (!m.Success)
                return "";
            return StripTags(m.Groups[1].Value).Trim();
        }

        private static string StripTags(string html)
        {
            if (string.IsNullOrEmpty(html))
                return "";
            string s = Regex.Replace(html, @"<[^>]+>", " ");
            s = WebUtility.HtmlDecode(s);
            return Regex.Replace(s, @"\s+", " ").Trim();
        }

        private static XmlDocument LoadXml(ZipArchiveEntry entry)
        {
            using (Stream s = entry.Open())
            {
                XmlReaderSettings settings = new XmlReaderSettings();
                settings.DtdProcessing = DtdProcessing.Prohibit;
                settings.XmlResolver = null;
                using (XmlReader reader = XmlReader.Create(s, settings))
                {
                    XmlDocument doc = new XmlDocument();
                    doc.Load(reader);
                    return doc;
                }
            }
        }

        private static string ReadEntryText(ZipArchive zip, string path)
        {
            ZipArchiveEntry e = GetEntry(zip, path);
            if (e == null)
                return null;
            using (Stream s = e.Open())
            using (StreamReader r = new StreamReader(s, Encoding.UTF8, true))
                return r.ReadToEnd();
        }

        private static ZipArchiveEntry GetEntry(ZipArchive zip, string path)
        {
            string key = NormalizePath(path);
            for (int i = 0; i < zip.Entries.Count; i++)
            {
                if (string.Equals(NormalizePath(zip.Entries[i].FullName), key,
                    StringComparison.OrdinalIgnoreCase))
                    return zip.Entries[i];
            }
            return null;
        }

        private static ZipArchiveEntry GetEntry(Dictionary<string, ZipArchiveEntry> map,
            string path)
        {
            ZipArchiveEntry e;
            return map.TryGetValue(NormalizePath(path), out e) ? e : null;
        }

        private static string Resolve(string baseDir, string href)
        {
            if (string.IsNullOrEmpty(href))
                return "";
            href = Uri.UnescapeDataString(href.Replace('\\', '/'));
            int cut = href.IndexOfAny(new char[] { '#', '?' });
            if (cut >= 0) href = href.Substring(0, cut);
            if (href.StartsWith("/", StringComparison.Ordinal))
                return NormalizePath(href.TrimStart('/'));
            return NormalizePath(string.IsNullOrEmpty(baseDir) ? href : baseDir + "/" + href);
        }

        private static string NormalizePath(string path)
        {
            if (string.IsNullOrEmpty(path))
                return "";
            path = path.Replace('\\', '/');
            string[] parts = path.Split('/');
            List<string> stack = new List<string>();
            for (int i = 0; i < parts.Length; i++)
            {
                string p = parts[i];
                if (p.Length == 0 || p == ".")
                    continue;
                if (p == "..")
                {
                    if (stack.Count > 0) stack.RemoveAt(stack.Count - 1);
                }
                else
                {
                    stack.Add(p);
                }
            }
            return string.Join("/", stack.ToArray());
        }

        private static string DirectoryOf(string path)
        {
            int i = path.LastIndexOf('/');
            return i < 0 ? "" : path.Substring(0, i);
        }

        private static XmlElement FindFirst(XmlNode node, string localName)
        {
            if (node == null)
                return null;
            XmlElement e = node as XmlElement;
            if (e != null && string.Equals(e.LocalName, localName,
                StringComparison.OrdinalIgnoreCase))
                return e;
            for (int i = 0; i < node.ChildNodes.Count; i++)
            {
                XmlElement found = FindFirst(node.ChildNodes[i], localName);
                if (found != null)
                    return found;
            }
            return null;
        }

        private static List<XmlElement> FindAll(XmlNode node, string localName)
        {
            List<XmlElement> list = new List<XmlElement>();
            FindAll(node, localName, list);
            return list;
        }

        private static void FindAll(XmlNode node, string localName, List<XmlElement> list)
        {
            if (node == null)
                return;
            XmlElement e = node as XmlElement;
            if (e != null && string.Equals(e.LocalName, localName,
                StringComparison.OrdinalIgnoreCase))
                list.Add(e);
            for (int i = 0; i < node.ChildNodes.Count; i++)
                FindAll(node.ChildNodes[i], localName, list);
        }

        private static string TextOf(XmlElement e)
        {
            return e == null ? "" : e.InnerText;
        }
    }
}
