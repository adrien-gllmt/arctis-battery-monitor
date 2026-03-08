using Serilog;
using Velopack;
using Velopack.Sources;

namespace ArctisBatteryMonitor.Services
{
    internal class UpdateService
    {
        private const string RepoUrl = "https://github.com/adrien-gllmt/arctis-battery-monitor";

        private UpdateInfo? _pendingUpdate;
        public bool IsInstalled { get; }

        public UpdateService()
        {
            IsInstalled = new UpdateManager(new GithubSource(RepoUrl, null, false)).IsInstalled;
        }

        public async Task<string?> CheckAndDownloadAsync()
        {
            try
            {
                var mgr = new UpdateManager(new GithubSource(RepoUrl, null, false));
                var update = await mgr.CheckForUpdatesAsync();
                if (update == null) return null;

                await mgr.DownloadUpdatesAsync(update);
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
                new UpdateManager(new GithubSource(RepoUrl, null, false)).ApplyUpdatesAndRestart(_pendingUpdate);
        }
    }
}
