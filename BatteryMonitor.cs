using ArctisBatteryMonitor.Models;
using ArctisBatteryMonitor.Services;
using Timer = System.Windows.Forms.Timer;

namespace ArctisBatteryMonitor
{
    internal class BatteryMonitor : ApplicationContext
    {
        private readonly TimingConfig _timing;

        private readonly NotifyIcon _notifyIcon = new()
        {
            Text = "Arctis Battery Monitor",
            ContextMenuStrip = new ContextMenuStrip()
        };

        private readonly HeadsetService _headsetService = new();
        private readonly SettingsService _settingsService = new();
        private readonly Timer _animationTimer = new();
        private readonly Dictionary<string, Icon> _iconCache = new();

        private readonly ToolStripMenuItem _notifNoDevice;
        private readonly ToolStripMenuItem _notifConnected;
        private readonly ToolStripMenuItem _notifDisconnected;
        private readonly ToolStripMenuItem _notifAll;

        private CancellationTokenSource _cts = new();
        private HeadsetState _currentState = HeadsetState.Searching;
        private int _animationFrame = 1;
        private bool _searchNotificationShown;

        public BatteryMonitor()
        {
            _timing = _settingsService.Timing;
            _animationTimer.Interval = _timing.AnimationIntervalMs;
            _animationTimer.Tick += OnAnimationTick;
            _animationTimer.Start();

            var settings = _settingsService.Settings;

            _notifNoDevice = new ToolStripMenuItem("No device found") { Checked = settings.NotifyNoDevice, CheckOnClick = true };
            _notifConnected = new ToolStripMenuItem("Connected") { Checked = settings.NotifyConnected, CheckOnClick = true };
            _notifDisconnected = new ToolStripMenuItem("Disconnected") { Checked = settings.NotifyDisconnected, CheckOnClick = true };

            bool allEnabled = settings.NotifyNoDevice && settings.NotifyConnected && settings.NotifyDisconnected;
            _notifAll = new ToolStripMenuItem("Enable all") { Checked = allEnabled, CheckOnClick = true };

            _notifAll.CheckedChanged += OnToggleAll;
            _notifNoDevice.CheckedChanged += OnToggleIndividual;
            _notifConnected.CheckedChanged += OnToggleIndividual;
            _notifDisconnected.CheckedChanged += OnToggleIndividual;

            var notificationsMenu = new ToolStripMenuItem("Notifications");
            notificationsMenu.DropDownItems.Add(_notifAll);
            notificationsMenu.DropDownItems.Add(new ToolStripSeparator());
            notificationsMenu.DropDownItems.Add(_notifNoDevice);
            notificationsMenu.DropDownItems.Add(_notifConnected);
            notificationsMenu.DropDownItems.Add(_notifDisconnected);

            notificationsMenu.DropDown.Closing += (_, args) =>
            {
                if (args.CloseReason == ToolStripDropDownCloseReason.ItemClicked)
                    args.Cancel = true;
            };

            _notifyIcon.ContextMenuStrip.Items.Add("Reconnect", null, OnReconnect);
            _notifyIcon.ContextMenuStrip.Items.Add(notificationsMenu);
            _notifyIcon.ContextMenuStrip.Items.Add(new ToolStripSeparator());
            _notifyIcon.ContextMenuStrip.Items.Add("Exit", null, OnExit);

            _notifyIcon.Visible = true;

            _ = MonitorLoopAsync(_cts.Token);
        }

        private bool _suppressToggleEvents;

        private void OnToggleAll(object? sender, EventArgs e)
        {
            if (_suppressToggleEvents) return;

            _suppressToggleEvents = true;
            bool enabled = _notifAll.Checked;
            _notifNoDevice.Checked = enabled;
            _notifConnected.Checked = enabled;
            _notifDisconnected.Checked = enabled;
            _suppressToggleEvents = false;

            SaveNotificationSettings();
        }

        private void OnToggleIndividual(object? sender, EventArgs e)
        {
            if (_suppressToggleEvents) return;

            _suppressToggleEvents = true;
            _notifAll.Checked = _notifNoDevice.Checked && _notifConnected.Checked && _notifDisconnected.Checked;
            _suppressToggleEvents = false;

            SaveNotificationSettings();
        }

        private void SaveNotificationSettings()
        {
            _settingsService.Settings.NotifyNoDevice = _notifNoDevice.Checked;
            _settingsService.Settings.NotifyConnected = _notifConnected.Checked;
            _settingsService.Settings.NotifyDisconnected = _notifDisconnected.Checked;
            _settingsService.Save();
        }

        private void ShowNotification(string title, string message, ToolTipIcon tipIcon, ToolStripMenuItem guard)
        {
            if (guard.Checked)
                _notifyIcon.ShowBalloonTip(2_000, title, message, tipIcon);
        }

        private Icon GetIcon(string path)
        {
            if (!_iconCache.TryGetValue(path, out var icon))
            {
                icon = new Icon(path);
                _iconCache[path] = icon;
            }
            return icon;
        }

        private void OnAnimationTick(object? sender, EventArgs e)
        {
            if (_currentState is HeadsetState.Connected or HeadsetState.Disconnected)
                return;

            _notifyIcon.Icon = GetIcon($"Resources/headset_connecting_{_animationFrame}.ico");
            _animationFrame = _animationFrame % _timing.AnimationFrames + 1;
        }

        private async Task MonitorLoopAsync(CancellationToken ct)
        {
            while (!ct.IsCancellationRequested)
            {
                var status = _headsetService.GetStatus();
                var previousState = _currentState;

                switch (status.State)
                {
                    case HeadsetState.Searching:
                        _currentState = HeadsetState.Searching;
                        _animationTimer.Start();

                        if (!_searchNotificationShown)
                        {
                            ShowNotification("No devices found", "Searching for devices...", ToolTipIcon.Info, _notifNoDevice);
                            _searchNotificationShown = true;
                        }

                        await DelayOrCancel(_timing.RetryDelayMs, ct);
                        break;

                    case HeadsetState.Connecting:
                        _currentState = HeadsetState.Connecting;
                        _animationTimer.Start();
                        await DelayOrCancel(_timing.RetryDelayMs, ct);
                        break;

                    case HeadsetState.Connected:
                        _currentState = HeadsetState.Connected;
                        _animationTimer.Stop();
                        _notifyIcon.Icon = GetIcon($"Resources/battery-{status.BatteryLevel}.ico");
                        _searchNotificationShown = false;

                        if (previousState != HeadsetState.Connected)
                        {
                            string message = _headsetService.ConnectedDevices.Count > 1
                                ? $"Multiple devices detected — defaulted to {status.Device!.Name}"
                                : status.Device!.Name;

                            string title = _headsetService.ConnectedDevices.Count > 1
                                ? "Multiple devices detected"
                                : "Successfully connected";

                            ShowNotification(title, message, ToolTipIcon.Info, _notifConnected);
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
            _notifyIcon.Icon = GetIcon("Resources/headset_disconnected.ico");

            if (previousState == HeadsetState.Connected)
            {
                ShowNotification("Disconnected", status.Device?.Name ?? "Unknown device", ToolTipIcon.Info, _notifDisconnected);
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

        private void OnReconnect(object? sender, EventArgs e)
        {
            _cts.Cancel();
            _cts.Dispose();
            _cts = new CancellationTokenSource();

            _headsetService.Reset();
            _currentState = HeadsetState.Searching;
            _searchNotificationShown = false;
            _animationTimer.Start();

            _ = MonitorLoopAsync(_cts.Token);
        }

        private void OnExit(object? sender, EventArgs e)
        {
            _cts.Cancel();
            _cts.Dispose();
            _animationTimer.Stop();
            _animationTimer.Dispose();

            _notifyIcon.Visible = false;
            _notifyIcon.Dispose();

            foreach (var icon in _iconCache.Values)
                icon.Dispose();
            _iconCache.Clear();

            Application.Exit();
        }
    }
}
