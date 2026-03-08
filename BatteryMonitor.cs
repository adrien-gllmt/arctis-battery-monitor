using System.Globalization;
using ArctisBatteryMonitor.Resources.Localization;
using ArctisBatteryMonitor.Models;
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
            ContextMenuStrip = new ContextMenuStrip { Renderer = Renderer, ShowImageMargin = false, ShowCheckMargin = true }
        };

        private readonly HeadsetService _headsetService = new();
        private readonly SettingsService _settingsService;
        private readonly Timer _animationTimer = new();
        private readonly Dictionary<string, Icon> _iconCache = new();

        // Notification submenu items
        private readonly ToolStripMenuItem _notifNoDevice;
        private readonly ToolStripMenuItem _notifConnected;
        private readonly ToolStripMenuItem _notifDisconnected;
        private readonly ToolStripMenuItem _notifAll;

        // Top-level menu items (kept for in-place text refresh)
        private readonly ToolStripMenuItem _reconnectItem;
        private readonly ToolStripMenuItem _selectHeadsetMenu;
        private readonly ToolStripMenuItem _notificationsMenu;
        private readonly ToolStripMenuItem _languageMenu;
        private readonly ToolStripMenuItem _languageSystemItem;
        private readonly ToolStripMenuItem _languageEnglishItem;
        private readonly ToolStripMenuItem _languageFrenchItem;
        private readonly ToolStripMenuItem _startWithWindowsItem;
        private readonly ToolStripMenuItem _exitItem;

        private CancellationTokenSource _cts = new();
        private HeadsetState _currentState = HeadsetState.Searching;
        private int _animationFrame = 1;
        private bool _searchNotificationShown;
        private int _lastKnownDeviceCount = -1;
        private HeadsetInfo? _lastChosenDevice;

        private bool _suppressToggleEvents;

        public BatteryMonitor(SettingsService settingsService)
        {
            _settingsService = settingsService;
            _timing = _settingsService.Timing;
            _headsetService.PreferredProductId = _settingsService.Settings.PreferredDeviceProductId;
            _animationTimer.Interval = _timing.AnimationIntervalMs;
            _animationTimer.Tick += OnAnimationTick;
            _animationTimer.Start();

            var settings = _settingsService.Settings;

            _notifNoDevice = new ToolStripMenuItem(Strings.NoDeviceFound) { Checked = settings.NotifyNoDevice, CheckOnClick = true };
            _notifConnected = new ToolStripMenuItem(Strings.MenuNotifConnected) { Checked = settings.NotifyConnected, CheckOnClick = true };
            _notifDisconnected = new ToolStripMenuItem(Strings.MenuNotifDisconnected) { Checked = settings.NotifyDisconnected, CheckOnClick = true };

            bool allEnabled = settings.NotifyNoDevice && settings.NotifyConnected && settings.NotifyDisconnected;
            _notifAll = new ToolStripMenuItem(Strings.MenuNotifEnableAll) { Checked = allEnabled, CheckOnClick = true };

            _notifAll.CheckedChanged += OnToggleAll;
            _notifNoDevice.CheckedChanged += OnToggleIndividual;
            _notifConnected.CheckedChanged += OnToggleIndividual;
            _notifDisconnected.CheckedChanged += OnToggleIndividual;

            _notificationsMenu = new ToolStripMenuItem(Strings.MenuNotifications);
            _notificationsMenu.DropDownItems.Add(_notifAll);
            _notificationsMenu.DropDownItems.Add(new ToolStripSeparator());
            _notificationsMenu.DropDownItems.Add(_notifNoDevice);
            _notificationsMenu.DropDownItems.Add(_notifConnected);
            _notificationsMenu.DropDownItems.Add(_notifDisconnected);
            _notificationsMenu.DropDown.Renderer = Renderer;
            _notificationsMenu.DropDown.Closing += (_, args) =>
            {
                if (args.CloseReason == ToolStripDropDownCloseReason.ItemClicked)
                    args.Cancel = true;
            };

            _selectHeadsetMenu = new ToolStripMenuItem(Strings.MenuSelectHeadset);
            _selectHeadsetMenu.DropDown.Renderer = Renderer;

            var current = settings.Language;
            _languageSystemItem = new ToolStripMenuItem(Strings.MenuLanguageSystem) { Checked = string.IsNullOrEmpty(current), CheckOnClick = false };
            _languageEnglishItem = new ToolStripMenuItem("English") { Checked = current == "en", CheckOnClick = false };
            _languageFrenchItem = new ToolStripMenuItem("Français") { Checked = current == "fr", CheckOnClick = false };
            _languageSystemItem.Click += (_, _) => OnSelectLanguage(null);
            _languageEnglishItem.Click += (_, _) => OnSelectLanguage("en");
            _languageFrenchItem.Click += (_, _) => OnSelectLanguage("fr");

            _languageMenu = new ToolStripMenuItem(Strings.MenuLanguage);
            _languageMenu.DropDown.Renderer = Renderer;
            _languageMenu.DropDownItems.Add(_languageSystemItem);
            _languageMenu.DropDownItems.Add(new ToolStripSeparator());
            _languageMenu.DropDownItems.Add(_languageEnglishItem);
            _languageMenu.DropDownItems.Add(_languageFrenchItem);

            _reconnectItem = new ToolStripMenuItem(Strings.MenuReconnect);
            _reconnectItem.Click += OnReconnect;

            _startWithWindowsItem = new ToolStripMenuItem(Strings.MenuStartWithWindows) { Checked = settings.StartWithWindows, CheckOnClick = true };
            _startWithWindowsItem.CheckedChanged += OnToggleStartWithWindows;

            _exitItem = new ToolStripMenuItem(Strings.MenuExit);
            _exitItem.Click += OnExit;

            _notifyIcon.ContextMenuStrip.Items.Add(_reconnectItem);
            _notifyIcon.ContextMenuStrip.Items.Add(_selectHeadsetMenu);
            _notifyIcon.ContextMenuStrip.Items.Add(_notificationsMenu);
            _notifyIcon.ContextMenuStrip.Items.Add(_languageMenu);
            _notifyIcon.ContextMenuStrip.Items.Add(_startWithWindowsItem);
            _notifyIcon.ContextMenuStrip.Items.Add(new ToolStripSeparator());
            _notifyIcon.ContextMenuStrip.Items.Add(_exitItem);

            _notifyIcon.Visible = true;

            Log.Information("BatteryMonitor initialized, starting monitor loop");
            _ = MonitorLoopAsync(_cts.Token);
        }

        private void OnSelectLanguage(string? language)
        {
            if (_settingsService.Settings.Language == language) return;

            _settingsService.Settings.Language = language;
            _settingsService.Save();

            if (string.IsNullOrEmpty(language))
            {
                CultureInfo.DefaultThreadCurrentUICulture = null;
                Thread.CurrentThread.CurrentUICulture = CultureInfo.InstalledUICulture;
            }
            else
            {
                var culture = new CultureInfo(language);
                Thread.CurrentThread.CurrentUICulture = culture;
                CultureInfo.DefaultThreadCurrentUICulture = culture;
            }

            _languageSystemItem.Checked = string.IsNullOrEmpty(language);
            _languageEnglishItem.Checked = language == "en";
            _languageFrenchItem.Checked = language == "fr";

            RefreshMenuTexts();
            Log.Information("Language changed to {Language}", language ?? "system");
        }

        private void OnToggleStartWithWindows(object? sender, EventArgs e)
        {
            _settingsService.Settings.StartWithWindows = _startWithWindowsItem.Checked;
            _settingsService.Save();
            StartupService.Apply(_startWithWindowsItem.Checked);
        }

        private void RefreshMenuTexts()
        {
            _reconnectItem.Text = Strings.MenuReconnect;
            _selectHeadsetMenu.Text = Strings.MenuSelectHeadset;
            _notificationsMenu.Text = Strings.MenuNotifications;
            _notifAll.Text = Strings.MenuNotifEnableAll;
            _notifNoDevice.Text = Strings.NoDeviceFound;
            _notifConnected.Text = Strings.MenuNotifConnected;
            _notifDisconnected.Text = Strings.MenuNotifDisconnected;
            _languageMenu.Text = Strings.MenuLanguage;
            _languageSystemItem.Text = Strings.MenuLanguageSystem;
            _startWithWindowsItem.Text = Strings.MenuStartWithWindows;
            _exitItem.Text = Strings.MenuExit;

            // Refresh "No device found" placeholder in headset submenu if currently shown
            if (_selectHeadsetMenu.DropDownItems is [ToolStripMenuItem { Enabled: false } placeholder])
                placeholder.Text = Strings.NoDeviceFound;
        }

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
            Log.Debug("Notification settings saved");
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

            _notifyIcon.Icon = GetIcon($"Resources/Images/headset_connecting_{_animationFrame}.ico");
            _animationFrame = _animationFrame % _timing.AnimationFrames + 1;
        }

        private void RebuildHeadsetSubmenuIfNeeded()
        {
            var devices = _headsetService.ConnectedDevices;
            var chosen = _headsetService.ChosenDevice;

            if (devices.Count == _lastKnownDeviceCount && chosen == _lastChosenDevice) return;

            _lastKnownDeviceCount = devices.Count;
            _lastChosenDevice = chosen;
            _selectHeadsetMenu.DropDownItems.Clear();

            if (devices.Count == 0)
            {
                _selectHeadsetMenu.DropDownItems.Add(new ToolStripMenuItem(Strings.NoDeviceFound) { Enabled = false });
                return;
            }

            foreach (var device in devices)
            {
                var item = new ToolStripMenuItem(device.Name) { Checked = device == chosen, CheckOnClick = false };
                item.Click += (_, _) => OnSelectHeadset(device);
                _selectHeadsetMenu.DropDownItems.Add(item);
            }
        }

        private void OnSelectHeadset(HeadsetInfo device)
        {
            Log.Information("User selected headset: {Name}", device.Name);
            _headsetService.SelectDevice(device);
            _headsetService.PreferredProductId = device.ProductId;
            _settingsService.Settings.PreferredDeviceProductId = device.ProductId;
            _settingsService.Save();

            foreach (ToolStripMenuItem item in _selectHeadsetMenu.DropDownItems)
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
                            ShowNotification(Strings.NotifNoDeviceTitle, Strings.NotifNoDeviceMessage, ToolTipIcon.Info, _notifNoDevice);
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
                        _notifyIcon.Icon = GetIcon($"Resources/Images/battery-{status.BatteryLevel}.ico");
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
            _notifyIcon.Icon = GetIcon("Resources/Images/headset_disconnected.ico");

            if (previousState == HeadsetState.Connected)
            {
                Log.Information("State -> Disconnected: {Name}", status.Device?.Name ?? "Unknown device");
                ShowNotification(Strings.NotifDisconnectedTitle, status.Device?.Name ?? "Unknown device", ToolTipIcon.Info, _notifDisconnected);
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

        private void OnExit(object? sender, EventArgs e)
        {
            Log.Information("Exit requested, shutting down");
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
