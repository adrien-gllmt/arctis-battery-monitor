using Microsoft.Win32;
using Serilog;

namespace ArctisBatteryMonitor.Services
{
    internal static class StartupService
    {
        private const string AppName = "ArctisBatteryMonitor";
        private const string RunKey = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run";

        public static void Apply(bool enable)
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(RunKey, writable: true)!;
                if (enable)
                    key.SetValue(AppName, Application.ExecutablePath);
                else
                    key.DeleteValue(AppName, throwOnMissingValue: false);

                Log.Information("Start with Windows set to {Value}", enable);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to update startup registry key");
            }
        }
    }
}
