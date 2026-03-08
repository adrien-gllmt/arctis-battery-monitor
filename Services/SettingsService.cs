using System.Text.Json;

namespace ArctisBatteryMonitor.Services
{
    internal class AppSettings
    {
        public bool NotifyNoDevice { get; set; }
        public bool NotifyConnected { get; set; }
        public bool NotifyDisconnected { get; set; }
    }

    internal class TimingConfig
    {
        public int RetryDelayMs { get; set; } = 15_000;
        public int RefreshDelayMs { get; set; } = 30_000;
        public int DisconnectedRefreshDelayMs { get; set; } = 5_000;
        public int AnimationIntervalMs { get; set; } = 300;
        public int AnimationFrames { get; set; } = 4;
    }

    internal class SettingsService
    {
        private static readonly string SettingsDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "ArctisBatteryMonitor");

        private static readonly string SettingsPath = Path.Combine(SettingsDir, "settings.json");

        private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

        public AppSettings Settings { get; private set; } = new();
        public TimingConfig Timing { get; private set; } = new();

        public SettingsService()
        {
            LoadSettings();
            LoadConfig();
        }

        private void LoadConfig()
        {
            try
            {
                if (File.Exists("config.json"))
                {
                    var json = File.ReadAllText("config.json");
                    Timing = JsonSerializer.Deserialize<TimingConfig>(json, JsonOptions) ?? new TimingConfig();
                }
            }
            catch
            {
                Timing = new TimingConfig();
            }
        }

        private void LoadSettings()
        {
            try
            {
                if (File.Exists(SettingsPath))
                {
                    var json = File.ReadAllText(SettingsPath);
                    Settings = JsonSerializer.Deserialize<AppSettings>(json, JsonOptions) ?? new AppSettings();
                }
            }
            catch
            {
                Settings = new AppSettings();
            }
        }

        public void Save()
        {
            try
            {
                Directory.CreateDirectory(SettingsDir);
                var json = JsonSerializer.Serialize(Settings, JsonOptions);
                File.WriteAllText(SettingsPath, json);
            }
            catch
            {
                // Best effort — don't crash the app over settings
            }
        }
    }
}
