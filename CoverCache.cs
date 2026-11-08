using System;
using System.Collections.Generic;
using System.Drawing;

namespace LightReader
{
    internal sealed class CoverCache
    {
        private sealed class Entry
        {
            public Bitmap Image;
            public DateTime Used;
        }

        private readonly Dictionary<string, Entry> _map =
            new Dictionary<string, Entry>(StringComparer.OrdinalIgnoreCase);
        private readonly int _max;

        public CoverCache(int max)
        {
            _max = Math.Max(4, max);
        }

        public Bitmap Get(string path)
        {
            if (string.IsNullOrEmpty(path))
                return null;
            Entry e;
            if (_map.TryGetValue(path, out e))
            {
                e.Used = DateTime.Now;
                return e.Image;
            }
            Bitmap bmp = null;
            try
            {
                bmp = ImageLoader.LoadScaled(path, 300, 400);
            }
            catch
            {
                return null;
            }
            if (bmp == null)
                return null;
            e = new Entry();
            e.Image = bmp;
            e.Used = DateTime.Now;
            _map[path] = e;
            Trim();
            return bmp;
        }

        public void Remove(string path)
        {
            if (string.IsNullOrEmpty(path))
                return;
            Entry e;
            if (_map.TryGetValue(path, out e))
            {
                _map.Remove(path);
                try { e.Image.Dispose(); } catch { }
            }
        }

        public void Clear()
        {
            foreach (Entry e in _map.Values)
            {
                try { e.Image.Dispose(); } catch { }
            }
            _map.Clear();
        }

        private void Trim()
        {
            while (_map.Count > _max)
            {
                string oldest = null;
                DateTime t = DateTime.MaxValue;
                foreach (KeyValuePair<string, Entry> pair in _map)
                {
                    if (pair.Value.Used < t)
                    {
                        t = pair.Value.Used;
                        oldest = pair.Key;
                    }
                }
                if (oldest == null) break;
                Entry e = _map[oldest];
                _map.Remove(oldest);
                try { e.Image.Dispose(); } catch { }
            }
        }
    }
}
