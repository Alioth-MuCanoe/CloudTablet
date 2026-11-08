using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Text;

namespace LightReader
{
    internal static class ImageLoader
    {
        public static Bitmap LoadScaled(string path, int maxWidth, int maxHeight)
        {
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
                throw new FileNotFoundException("找不到图片文件。", path);
            using (FileStream fs = new FileStream(path, FileMode.Open,
                FileAccess.Read, FileShare.Read))
                return LoadScaled(fs, path, maxWidth, maxHeight);
        }

        public static Bitmap LoadScaled(Stream stream, string name,
            int maxWidth, int maxHeight)
        {
            if (stream == null)
                throw new ArgumentNullException("stream");
            bool webp = Path.GetExtension(name ?? "").Equals(".webp",
                StringComparison.OrdinalIgnoreCase);
            Bitmap source = null;
            if (!webp)
            {
                try
                {
                    if (stream.CanSeek) stream.Position = 0;
                    using (Image img = Image.FromStream(stream))
                        source = Resize(img, maxWidth, maxHeight);
                }
                catch
                {
                    source = null;
                }
            }
            if (source == null)
            {
                if (stream.CanSeek) stream.Position = 0;
                source = WebPImageDecoder.Load(stream, maxWidth, maxHeight);
            }
            return source;
        }

        internal static Bitmap Resize(Image img, int maxWidth, int maxHeight)
        {
            int w = img.Width;
            int h = img.Height;
            if (w <= 0 || h <= 0)
                throw new InvalidDataException("图片尺寸无效。");
            double scale = Math.Min(1.0, Math.Min(
                maxWidth / (double)w, maxHeight / (double)h));
            int nw = Math.Max(1, (int)Math.Round(w * scale));
            int nh = Math.Max(1, (int)Math.Round(h * scale));
            Bitmap bmp = new Bitmap(nw, nh, PixelFormat.Format32bppArgb);
            using (Graphics g = Graphics.FromImage(bmp))
            {
                g.Clear(Color.Transparent);
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                g.SmoothingMode = SmoothingMode.HighQuality;
                g.DrawImage(img, new Rectangle(0, 0, nw, nh));
            }
            return bmp;
        }

        public static string CoverPathForBook(string bookPath)
        {
            string hash = Hash(bookPath.ToLowerInvariant());
            return Path.Combine(ReaderSettings.CoversFolder, hash + ".jpg");
        }

        public static string SaveCover(Bitmap bmp, string bookPath)
        {
            Directory.CreateDirectory(ReaderSettings.CoversFolder);
            string target = CoverPathForBook(bookPath);
            using (Bitmap canvas = new Bitmap(360, 480, PixelFormat.Format24bppRgb))
            using (Graphics g = Graphics.FromImage(canvas))
            {
                g.Clear(Color.White);
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                double scale = Math.Max(360.0 / bmp.Width, 480.0 / bmp.Height);
                int dw = (int)Math.Round(bmp.Width * scale);
                int dh = (int)Math.Round(bmp.Height * scale);
                g.DrawImage(bmp, (360 - dw) / 2, (480 - dh) / 2, dw, dh);

                ImageCodecInfo jpeg = null;
                ImageCodecInfo[] codecs = ImageCodecInfo.GetImageEncoders();
                for (int i = 0; i < codecs.Length; i++)
                {
                    if (codecs[i].FormatID == ImageFormat.Jpeg.Guid)
                    {
                        jpeg = codecs[i];
                        break;
                    }
                }
                if (jpeg == null)
                    canvas.Save(target, ImageFormat.Jpeg);
                else
                {
                    using (EncoderParameters ep = new EncoderParameters(1))
                    {
                        ep.Param[0] = new EncoderParameter(
                            System.Drawing.Imaging.Encoder.Quality, 86L);
                        canvas.Save(target, jpeg, ep);
                    }
                }
            }
            return target;
        }

        public static string SaveBackground(Bitmap bmp)
        {
            Directory.CreateDirectory(ReaderSettings.SettingsFolder);
            string target = Path.Combine(ReaderSettings.SettingsFolder, "background.jpg");
            double scale = Math.Min(1.0, Math.Min(1920.0 / bmp.Width,
                1200.0 / bmp.Height));
            int w = Math.Max(1, (int)Math.Round(bmp.Width * scale));
            int h = Math.Max(1, (int)Math.Round(bmp.Height * scale));
            using (Bitmap canvas = new Bitmap(w, h, PixelFormat.Format24bppRgb))
            using (Graphics g = Graphics.FromImage(canvas))
            {
                g.Clear(Color.White);
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                g.DrawImage(bmp, new Rectangle(0, 0, w, h));
                SaveJpeg(canvas, target, 90L);
            }
            try
            {
                string[] old = Directory.GetFiles(ReaderSettings.SettingsFolder,
                    "background.*");
                for (int i = 0; i < old.Length; i++)
                {
                    if (!string.Equals(Path.GetFullPath(old[i]),
                        Path.GetFullPath(target), StringComparison.OrdinalIgnoreCase))
                        File.Delete(old[i]);
                }
            }
            catch
            {
            }
            return target;
        }

        private static void SaveJpeg(Bitmap bmp, string target, long quality)
        {
            ImageCodecInfo jpeg = null;
            ImageCodecInfo[] codecs = ImageCodecInfo.GetImageEncoders();
            for (int i = 0; i < codecs.Length; i++)
            {
                if (codecs[i].FormatID == ImageFormat.Jpeg.Guid)
                {
                    jpeg = codecs[i];
                    break;
                }
            }
            if (jpeg == null)
            {
                bmp.Save(target, ImageFormat.Jpeg);
                return;
            }
            using (EncoderParameters ep = new EncoderParameters(1))
            {
                ep.Param[0] = new EncoderParameter(
                    System.Drawing.Imaging.Encoder.Quality, quality);
                bmp.Save(target, jpeg, ep);
            }
        }

        public static void DeleteCover(string path)
        {
            try
            {
                if (!string.IsNullOrEmpty(path) && File.Exists(path) &&
                    path.StartsWith(ReaderSettings.CoversFolder,
                        StringComparison.OrdinalIgnoreCase))
                    File.Delete(path);
            }
            catch
            {
            }
        }

        private static string Hash(string text)
        {
            using (System.Security.Cryptography.SHA1 sha =
                System.Security.Cryptography.SHA1.Create())
            {
                byte[] data = sha.ComputeHash(Encoding.UTF8.GetBytes(text ?? ""));
                StringBuilder sb = new StringBuilder();
                for (int i = 0; i < 8 && i < data.Length; i++)
                    sb.Append(data[i].ToString("x2"));
                return sb.ToString();
            }
        }
    }
}
