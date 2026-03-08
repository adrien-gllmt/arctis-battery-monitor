using Serilog;
using Velopack;
using Velopack.Sources;

namespace ArctisBatteryMonitor.Services
{
    internal class UpdateService
    {
        private readonly UpdateManager _mgr;
        private UpdateInfo? _pendingUpdate;

        public bool IsInstalled => _mgr.IsInstalled;

        public UpdateService()
        {
            var source = new GithubSource("https://github.com/adrien-gllmt/arctis-battery-monitor", null, false);
            _mgr = new UpdateManager(source);
        }

        public async Task<string?> CheckAndDownloadAsync()
        {
            try
            {
                var update = await _mgr.CheckForUpdatesAsync();
                if (update == null) return null;

                await _mgr.DownloadUpdatesAsync(update);
                _pendingUpdate = update;
                return update.TargetFullRelease.Version.ToString();
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Failed to check for updates");
                return null;
            }
        }

        public void ApplyUpdate()
        {
            if (_pendingUpdate != null)
                _mgr.ApplyUpdatesAndRestart(_pendingUpdate);
        }
    }
}
