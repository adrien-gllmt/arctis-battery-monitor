namespace ArctisBatteryMonitor.UI
{
    internal class IconCache : IDisposable
    {
        private readonly Dictionary<string, Icon> _cache = new();

        public Icon Get(string path)
        {
            if (!_cache.TryGetValue(path, out var icon))
            {
                icon = new Icon(path);
                _cache[path] = icon;
            }
            return icon;
        }

        public void Dispose()
        {
            foreach (var icon in _cache.Values)
                icon.Dispose();
            _cache.Clear();
        }
    }
}
