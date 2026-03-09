using System.Globalization;
using ArctisBatteryMonitor.Services;
using Serilog;
using Velopack;

namespace ArctisBatteryMonitor
{
    internal static class Program
    {
        [STAThread]
        static void Main()
        {
            Directory.SetCurrentDirectory(AppContext.BaseDirectory);

            VelopackApp.Build().Run();

            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Debug()
                .WriteTo.File("logs/arctis-battery-monitor-.log",
                    rollingInterval: RollingInterval.Day,
                    retainedFileCountLimit: 4,
                    outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{Level:u3}] {Message:lj}{NewLine}{Exception}")
                .CreateLogger();

            Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
            Application.ThreadException += OnThreadException;
            AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;

            Log.Information("Application starting");

            var settingsService = new SettingsService();
            StartupService.Apply(settingsService.Settings.StartWithWindows);
            SettingsService.ApplyCulture(settingsService.Settings.Language);

            ApplicationConfiguration.Initialize();
            Application.Run(new BatteryMonitor(settingsService));

            Log.Information("Application exiting");
            Log.CloseAndFlush();
        }

        private static void OnThreadException(object sender, ThreadExceptionEventArgs e)
        {
            Log.Fatal(e.Exception, "Unhandled UI thread exception");
            Log.CloseAndFlush();
            ShowCrashNotification(e.Exception);
        }

        private static void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            var ex = e.ExceptionObject as Exception;
            Log.Fatal(ex, "Unhandled domain exception");
            Log.CloseAndFlush();
            ShowCrashNotification(ex);
        }

        private static void ShowCrashNotification(Exception? ex)
        {
            try
            {
                using var icon = new NotifyIcon
                {
                    Icon = SystemIcons.Error,
                    Visible = true
                };
                icon.ShowBalloonTip(
                    5_000,
                    "Arctis Battery Monitor crashed",
                    ex?.Message ?? "An unexpected error occurred. Check logs for details.",
                    ToolTipIcon.Error);

                Thread.Sleep(5_000);
                icon.Visible = false;
            }
            catch
            {
                // Last resort — nothing we can do
            }
        }
    }
}
