using System;
using System.IO;
using System.Runtime.InteropServices;

namespace LightReader
{
    internal sealed class BrightnessController
    {
        [DllImport("user32.dll")]
        private static extern IntPtr GetDC(IntPtr hwnd);

        [DllImport("user32.dll")]
        private static extern int ReleaseDC(IntPtr hwnd, IntPtr hdc);

        [DllImport("gdi32.dll")]
        private static extern bool GetDeviceGammaRamp(IntPtr hdc, ushort[] ramp);

        [DllImport("gdi32.dll")]
        private static extern bool SetDeviceGammaRamp(IntPtr hdc, ushort[] ramp);

        private IntPtr _dc = IntPtr.Zero;
        private ushort[] _base;
        private bool _changed;

        private static string MarkerFile
        {
            get { return Path.Combine(ReaderSettings.SettingsFolder, "gamma.bin"); }
        }

        public static void RestoreStale()
        {
            try
            {
                if (!File.Exists(MarkerFile))
                    return;
                byte[] data = File.ReadAllBytes(MarkerFile);
                if (data.Length == 768 * 2)
                {
                    ushort[] ramp = new ushort[768];
                    for (int i = 0; i < 768; i++)
                        ramp[i] = (ushort)(data[i * 2] | (data[i * 2 + 1] << 8));
                    IntPtr dc = GetDC(IntPtr.Zero);
                    if (dc != IntPtr.Zero)
                    {
                        SetDeviceGammaRamp(dc, ramp);
                        ReleaseDC(IntPtr.Zero, dc);
                    }
                }
                File.Delete(MarkerFile);
            }
            catch
            {
            }
        }

        public bool Apply(int percent)
        {
            if (percent < 20) percent = 20;
            if (percent > 100) percent = 100;
            if (!Capture())
                return false;
            if (percent >= 100)
            {
                Restore();
                return true;
            }
            ushort[] ramp = new ushort[768];
            double factor = percent / 100.0;
            for (int i = 0; i < ramp.Length; i++)
            {
                int v = (int)Math.Round(_base[i] * factor);
                if (v < 0) v = 0;
                if (v > 65535) v = 65535;
                ramp[i] = (ushort)v;
            }
            bool ok = SetDeviceGammaRamp(_dc, ramp);
            if (ok) _changed = true;
            return ok;
        }

        public void Restore()
        {
            if (_dc != IntPtr.Zero && _base != null && _changed)
            {
                try { SetDeviceGammaRamp(_dc, _base); }
                catch { }
                _changed = false;
            }
            try
            {
                if (File.Exists(MarkerFile))
                    File.Delete(MarkerFile);
            }
            catch
            {
            }
        }

        public void Dispose()
        {
            Restore();
            if (_dc != IntPtr.Zero)
            {
                try { ReleaseDC(IntPtr.Zero, _dc); }
                catch { }
                _dc = IntPtr.Zero;
            }
            _base = null;
        }

        private bool Capture()
        {
            if (_base != null)
                return true;
            try
            {
                _dc = GetDC(IntPtr.Zero);
                if (_dc == IntPtr.Zero)
                    return false;
                ushort[] ramp = new ushort[768];
                if (!GetDeviceGammaRamp(_dc, ramp))
                {
                    ReleaseDC(IntPtr.Zero, _dc);
                    _dc = IntPtr.Zero;
                    return false;
                }
                _base = ramp;
                try
                {
                    Directory.CreateDirectory(ReaderSettings.SettingsFolder);
                    byte[] data = new byte[768 * 2];
                    for (int i = 0; i < 768; i++)
                    {
                        data[i * 2] = (byte)(ramp[i] & 0xFF);
                        data[i * 2 + 1] = (byte)((ramp[i] >> 8) & 0xFF);
                    }
                    File.WriteAllBytes(MarkerFile, data);
                }
                catch
                {
                }
                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}
