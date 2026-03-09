using ArctisBatteryMonitor.Models;
using ArctisBatteryMonitor.Resources.Localization;
using ArctisBatteryMonitor.Services;
using ArctisBatteryMonitor.UI;
using Serilog;
using Timer = System.Windows.Forms.Timer;

namespace ArctisBatteryMonitor
{
    internal class BatteryMonitor : ApplicationContext
    {
        private readonly TimingConfig _timing;

        private static readonly MenuRenderer Renderer = new();

        private readonly NotifyIcon _notifyIcon = new()
        {
            Text = "Arctis Battery Monitor",
            Icon = new Icon("Resources/Images/headset_connecting_1.ico"),
            ContextMenuStrip = new ContextMenuStrip { Renderer = Renderer, ShowImageMargin = false, ShowCheckMargin = true }
        };

        private readonly HeadsetService _headsetService = new();
        private readonly SettingsService _settingsService;
        private readonly UpdateService _updateService = new();
        private readonly TrayMenuBuilder _menu;
        private readonly Timer _animationTimer = new();
        private readonly IconCache _iconCache = new();

        private CancellationTokenSource _cts = new();
        private HeadsetState _currentState = HeadsetState.Searching;
        private int _animationFrame = 1;
        private bool _searchNotificationShown;
        private int _lastKnownDeviceCount = -1;
        private HeadsetInfo? _lastChosenDevice;

        public BatteryMonitor(SettingsService settingsService)
        {
            _settingsService = settingsService;
            _timing = _settingsService.Timing;
            _headsetService.PreferredProductId = _settingsService.Settings.PreferredDeviceProductId;

            _menu = new TrayMenuBuilder(_settingsService, Renderer);
            _menu.Build(_notifyIcon.ContextMenuStrip, OnReconnect, OnExit, () => CheckForUpdatesAsync());

            if (_updateService.IsInstalled)
                _ = CheckForUpdatesAsync(silent: true);

            _animationTimer.Interval = _timing.AnimationIntervalMs;
            _animationTimer.Tick += OnAnimationTick;
            _animationTimer.Start();

            _notifyIcon.Visible = true;

            Log.Information("BatteryMonitor initialized, starting monitor loop");
            _ = MonitorLoopAsync(_cts.Token);
        }

        private async Task CheckForUpdatesAsync(bool silent = false)
        {
            _menu.SetCheckingForUpdates(true);
            var version = await _updateService.CheckAndDownloadAsync();

            if (version != null)
            {
                _menu.SetUpdateAvailable(version, () => _updateService.ApplyUpdate());
                _notifyIcon.ShowBalloonTip(3_000, Strings.NotifUpdateTitle,
                    string.Format(Strings.NotifUpdateMessage, version), ToolTipIcon.Info);
            }
            else
            {
                _menu.SetCheckingForUpdates(false);
                if (!silent)
                    _notifyIcon.ShowBalloonTip(3_000, Strings.NotifUpToDateTitle,
                        Strings.NotifUpToDateMessage, ToolTipIcon.Info);
            }
        }

        private void ShowNotification(string title, string message, ToolTipIcon tipIcon, ToolStripMenuItem guard)
        {
            if (guard.Checked)
                _notifyIcon.ShowBalloonTip(2_000, title, message, tipIcon);
        }

        private void OnAnimationTick(object? sender, EventArgs e)
        {
            if (_currentState is HeadsetState.Connected or HeadsetState.Disconnected)
                return;

            _notifyIcon.Icon = _iconCache.Get($"Resources/Images/headset_connecting_{_animationFrame}.ico");
            _animationFrame = _animationFrame % _timing.AnimationFrames + 1;
        }

        private void RebuildHeadsetSubmenuIfNeeded()
        {
            var devices = _headsetService.ConnectedDevices;
            var chosen = _headsetService.ChosenDevice;

            if (devices.Count == _lastKnownDeviceCount && chosen == _lastChosenDevice) return;

            _lastKnownDeviceCount = devices.Count;
            _lastChosenDevice = chosen;

            foreach (ToolStripItem item in _menu.SelectHeadsetMenu.DropDownItems)
                item.Dispose();
            _menu.SelectHeadsetMenu.DropDownItems.Clear();

            if (devices.Count == 0)
            {
                _menu.SelectHeadsetMenu.DropDownItems.Add(new ToolStripMenuItem(Strings.NoDeviceFound) { Enabled = false });
                return;
            }

            foreach (var device in devices)
            {
                var item = new ToolStripMenuItem(device.Name) { Checked = device == chosen, CheckOnClick = false };
                item.Click += (_, _) => OnSelectHeadset(device);
                _menu.SelectHeadsetMenu.DropDownItems.Add(item);
            }
        }

        private void OnSelectHeadset(HeadsetInfo device)
        {
            Log.Information("User selected headset: {Name}", device.Name);
            _headsetService.SelectDevice(device);
            _headsetService.PreferredProductId = device.ProductId;
            _settingsService.Settings.PreferredDeviceProductId = device.ProductId;
            _settingsService.Save();

            foreach (ToolStripMenuItem item in _menu.SelectHeadsetMenu.DropDownItems)
                item.Checked = item.Text == device.Name;

            _cts.Cancel();
            _cts.Dispose();
            _cts = new CancellationTokenSource();
            _currentState = HeadsetState.Connecting;
            _animationTimer.Start();
            _ = MonitorLoopAsync(_cts.Token);
        }

        private async Task MonitorLoopAsync(CancellationToken ct)
        {
            while (!ct.IsCancellationRequested)
            {
                var status = _headsetService.GetStatus();
                var previousState = _currentState;
                RebuildHeadsetSubmenuIfNeeded();

                switch (status.State)
                {
                    case HeadsetState.Searching:
                        _currentState = HeadsetState.Searching;
                        _animationTimer.Start();

                        if (!_searchNotificationShown)
                        {
                            Log.Information("State -> Searching, no devices found");
                            ShowNotification(Strings.NotifNoDeviceTitle, Strings.NotifNoDeviceMessage, ToolTipIcon.Info, _menu.NotifNoDevice);
                            _searchNotificationShown = true;
                        }

                        await DelayOrCancel(_timing.RetryDelayMs, ct);
                        break;

                    case HeadsetState.Connecting:
                        if (previousState != HeadsetState.Connecting)
                            Log.Information("State -> Connecting to {Name}", status.Device?.Name);
                        _currentState = HeadsetState.Connecting;
                        _animationTimer.Start();
                        await DelayOrCancel(_timing.RetryDelayMs, ct);
                        break;

                    case HeadsetState.Connected:
                        _currentState = HeadsetState.Connected;
                        _animationTimer.Stop();
                        _notifyIcon.Icon = _iconCache.Get($"Resources/Images/battery-{status.BatteryLevel}.ico");
                        _searchNotificationShown = false;

                        if (previousState != HeadsetState.Connected)
                        {
                            Log.Information("State -> Connected: {Name}, battery {Battery}% ({Charging})",
                                status.Device!.Name, status.BatteryLevel, status.ChargingStatus);

                            string message = _headsetService.ConnectedDevices.Count > 1
                                ? string.Format(Strings.NotifMultipleMessage, _headsetService.ConnectedDevices.Count, status.Device.Name)
                                : status.Device.Name;

                            string title = _headsetService.ConnectedDevices.Count > 1
                                ? Strings.NotifMultipleTitle
                                : Strings.NotifConnectedTitle;

                            ShowNotification(title, message, ToolTipIcon.Info, _menu.NotifConnected);
                        }

                        await DelayOrCancel(_timing.RefreshDelayMs, ct);
                        break;

                    default:
                        HandleDisconnected(status, previousState);
                        await DelayOrCancel(_timing.DisconnectedRefreshDelayMs, ct);
                        break;
                }
            }
        }

        private void HandleDisconnected(HeadsetStatus status, HeadsetState previousState)
        {
            _currentState = HeadsetState.Disconnected;
            _notifyIcon.Icon = _iconCache.Get("Resources/Images/headset_disconnected.ico");

            if (previousState == HeadsetState.Connected)
            {
                Log.Information("State -> Disconnected: {Name}", status.Device?.Name ?? "Unknown device");
                ShowNotification(Strings.NotifDisconnectedTitle, status.Device?.Name ?? "Unknown device", ToolTipIcon.Info, _menu.NotifDisconnected);
                _headsetService.Reset();
            }
        }

        private static async Task DelayOrCancel(int ms, CancellationToken ct)
        {
            try
            {
                await Task.Delay(ms, ct);
            }
            catch (TaskCanceledException)
            {
                // Expected on cancellation — exit gracefully
            }
        }

        private void OnReconnect()
        {
            Log.Information("Manual reconnect requested");
            _cts.Cancel();
            _cts.Dispose();
            _cts = new CancellationTokenSource();

            _headsetService.Reset();
            _currentState = HeadsetState.Searching;
            _searchNotificationShown = false;
            _lastKnownDeviceCount = -1;
            _animationTimer.Start();

            _ = MonitorLoopAsync(_cts.Token);
        }

        private void OnExit()
        {
            Log.Information("Exit requested, shutting down");
            _cts.Cancel();
            _cts.Dispose();
            _animationTimer.Stop();
            _animationTimer.Dispose();

            _notifyIcon.Visible = false;
            _notifyIcon.Dispose();
            _notifyIcon.ContextMenuStrip?.Dispose();

            _iconCache.Dispose();

            Application.Exit();
        }
    }
}
