using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Media.Imaging;
using WpfPixelFormats = System.Windows.Media.PixelFormats;

namespace LightReader
{
    /// <summary>
    /// WebP decoding through WIC/WPF. This class is only touched when a WebP
    /// file is actually used, so JPG/PNG reading (and application startup) does
    /// not load PresentationCore on systems where WPF is unavailable.
    /// </summary>
    internal static class WebPImageDecoder
    {
        public static Bitmap Load(Stream stream, int maxWidth, int maxHeight)
        {
            try
            {
                BitmapDecoder decoder = BitmapDecoder.Create(stream,
                    BitmapCreateOptions.PreservePixelFormat,
                    BitmapCacheOption.OnLoad);
                BitmapSource frame = decoder.Frames[0];
                BitmapSource src = frame;
                if (frame.Format != WpfPixelFormats.Bgra32)
                    src = new FormatConvertedBitmap(frame,
                        WpfPixelFormats.Bgra32, null, 0);
                int w = src.PixelWidth;
                int h = src.PixelHeight;
                int stride = w * 4;
                byte[] pixels = new byte[stride * h];
                src.CopyPixels(pixels, stride, 0);
                using (Bitmap raw = new Bitmap(w, h, PixelFormat.Format32bppArgb))
                {
                    BitmapData data = raw.LockBits(
                        new Rectangle(0, 0, w, h), ImageLockMode.WriteOnly,
                        PixelFormat.Format32bppArgb);
                    try
                    {
                        Marshal.Copy(pixels, 0, data.Scan0, pixels.Length);
                    }
                    finally
                    {
                        raw.UnlockBits(data);
                    }
                    return ImageLoader.Resize(raw, maxWidth, maxHeight);
                }
            }
            catch (Exception ex)
            {
                throw new NotSupportedException(
                    "无法解码这张图片。JPG/PNG 可直接使用；WebP 需要系统带有 WebP 解码组件（Windows 10/11 通常已安装）。",
                    ex);
            }
        }
    }
}
