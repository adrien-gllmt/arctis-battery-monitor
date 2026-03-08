namespace ArctisBatteryMonitor.Models
{
    internal class AppSettings
    {
        public bool NotifyNoDevice { get; set; }
        public bool NotifyConnected { get; set; }
        public bool NotifyDisconnected { get; set; }
        public int? PreferredDeviceProductId { get; set; }
        public string? Language { get; set; }
        public bool StartWithWindows { get; set; } = true;
    }

    internal class TimingConfig
    {
        public int RetryDelayMs { get; set; } = 15_000;
        public int RefreshDelayMs { get; set; } = 30_000;
        public int DisconnectedRefreshDelayMs { get; set; } = 5_000;
        public int AnimationIntervalMs { get; set; } = 300;
        public int AnimationFrames { get; set; } = 4;
    }
}
