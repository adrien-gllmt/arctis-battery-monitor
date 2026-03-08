using ArctisBatteryMonitor.Models;
using ArctisBatteryMonitor.Resources.Localization;
using ArctisBatteryMonitor.Services;
using Serilog;

namespace ArctisBatteryMonitor.UI
{
    internal class TrayMenuBuilder
    {
        private readonly SettingsService _settingsService;
        private readonly MenuRenderer _renderer;

        // Exposed for BatteryMonitor: notification guards and headset submenu
        public ToolStripMenuItem SelectHeadsetMenu { get; private set; } = null!;
        public ToolStripMenuItem NotifNoDevice { get; private set; } = null!;
        public ToolStripMenuItem NotifConnected { get; private set; } = null!;
        public ToolStripMenuItem NotifDisconnected { get; private set; } = null!;

        private ToolStripMenuItem _notifAll = null!;
        private ToolStripMenuItem _notificationsMenu = null!;
        private ToolStripMenuItem _reconnectItem = null!;
        private ToolStripMenuItem _languageMenu = null!;
        private ToolStripMenuItem _languageSystemItem = null!;
        private ToolStripMenuItem _languageEnglishItem = null!;
        private ToolStripMenuItem _languageFrenchItem = null!;
        private ToolStripMenuItem _startWithWindowsItem = null!;
        private ToolStripMenuItem _exitItem = null!;

        private bool _suppressToggleEvents;

        public TrayMenuBuilder(SettingsService settingsService, MenuRenderer renderer)
        {
            _settingsService = settingsService;
            _renderer = renderer;
        }

        public void Build(ContextMenuStrip strip, Action onReconnect, Action onExit)
        {
            var settings = _settingsService.Settings;

            // Notification submenu
            NotifNoDevice = new ToolStripMenuItem(Strings.NoDeviceFound) { Checked = settings.NotifyNoDevice, CheckOnClick = true };
            NotifConnected = new ToolStripMenuItem(Strings.MenuNotifConnected) { Checked = settings.NotifyConnected, CheckOnClick = true };
            NotifDisconnected = new ToolStripMenuItem(Strings.MenuNotifDisconnected) { Checked = settings.NotifyDisconnected, CheckOnClick = true };

            bool allEnabled = settings.NotifyNoDevice && settings.NotifyConnected && settings.NotifyDisconnected;
            _notifAll = new ToolStripMenuItem(Strings.MenuNotifEnableAll) { Checked = allEnabled, CheckOnClick = true };

            _notifAll.CheckedChanged += OnToggleAll;
            NotifNoDevice.CheckedChanged += OnToggleIndividual;
            NotifConnected.CheckedChanged += OnToggleIndividual;
            NotifDisconnected.CheckedChanged += OnToggleIndividual;

            _notificationsMenu = new ToolStripMenuItem(Strings.MenuNotifications);
            _notificationsMenu.DropDownItems.Add(_notifAll);
            _notificationsMenu.DropDownItems.Add(new ToolStripSeparator());
            _notificationsMenu.DropDownItems.Add(NotifNoDevice);
            _notificationsMenu.DropDownItems.Add(NotifConnected);
            _notificationsMenu.DropDownItems.Add(NotifDisconnected);
            _notificationsMenu.DropDown.Renderer = _renderer;
            _notificationsMenu.DropDown.Closing += (_, args) =>
            {
                if (args.CloseReason == ToolStripDropDownCloseReason.ItemClicked)
                    args.Cancel = true;
            };

            // Headset submenu
            SelectHeadsetMenu = new ToolStripMenuItem(Strings.MenuSelectHeadset);
            SelectHeadsetMenu.DropDown.Renderer = _renderer;

            // Language submenu
            var current = settings.Language;
            _languageSystemItem = new ToolStripMenuItem(Strings.MenuLanguageSystem) { Checked = string.IsNullOrEmpty(current), CheckOnClick = false };
            _languageEnglishItem = new ToolStripMenuItem("English") { Checked = current == "en", CheckOnClick = false };
            _languageFrenchItem = new ToolStripMenuItem("Français") { Checked = current == "fr", CheckOnClick = false };
            _languageSystemItem.Click += (_, _) => OnSelectLanguage(null);
            _languageEnglishItem.Click += (_, _) => OnSelectLanguage("en");
            _languageFrenchItem.Click += (_, _) => OnSelectLanguage("fr");

            _languageMenu = new ToolStripMenuItem(Strings.MenuLanguage);
            _languageMenu.DropDown.Renderer = _renderer;
            _languageMenu.DropDownItems.Add(_languageSystemItem);
            _languageMenu.DropDownItems.Add(new ToolStripSeparator());
            _languageMenu.DropDownItems.Add(_languageEnglishItem);
            _languageMenu.DropDownItems.Add(_languageFrenchItem);

            // Top-level items
            _reconnectItem = new ToolStripMenuItem(Strings.MenuReconnect);
            _reconnectItem.Click += (_, _) => onReconnect();

            _startWithWindowsItem = new ToolStripMenuItem(Strings.MenuStartWithWindows) { Checked = settings.StartWithWindows, CheckOnClick = true };
            _startWithWindowsItem.CheckedChanged += OnToggleStartWithWindows;

            _exitItem = new ToolStripMenuItem(Strings.MenuExit);
            _exitItem.Click += (_, _) => onExit();

            // Assemble strip
            strip.Items.Add(_reconnectItem);
            strip.Items.Add(SelectHeadsetMenu);
            strip.Items.Add(_notificationsMenu);
            strip.Items.Add(_languageMenu);
            strip.Items.Add(_startWithWindowsItem);
            strip.Items.Add(new ToolStripSeparator());
            strip.Items.Add(_exitItem);
        }

        public void RefreshTexts()
        {
            _reconnectItem.Text = Strings.MenuReconnect;
            SelectHeadsetMenu.Text = Strings.MenuSelectHeadset;
            _notificationsMenu.Text = Strings.MenuNotifications;
            _notifAll.Text = Strings.MenuNotifEnableAll;
            NotifNoDevice.Text = Strings.NoDeviceFound;
            NotifConnected.Text = Strings.MenuNotifConnected;
            NotifDisconnected.Text = Strings.MenuNotifDisconnected;
            _languageMenu.Text = Strings.MenuLanguage;
            _languageSystemItem.Text = Strings.MenuLanguageSystem;
            _startWithWindowsItem.Text = Strings.MenuStartWithWindows;
            _exitItem.Text = Strings.MenuExit;

            // Refresh "No device found" placeholder in headset submenu if currently shown
            if (SelectHeadsetMenu.DropDownItems is [ToolStripMenuItem { Enabled: false } placeholder])
                placeholder.Text = Strings.NoDeviceFound;
        }

        private void OnSelectLanguage(string? language)
        {
            if (_settingsService.Settings.Language == language) return;

            _settingsService.Settings.Language = language;
            _settingsService.Save();
            SettingsService.ApplyCulture(language);

            _languageSystemItem.Checked = string.IsNullOrEmpty(language);
            _languageEnglishItem.Checked = language == "en";
            _languageFrenchItem.Checked = language == "fr";

            RefreshTexts();
            Log.Information("Language changed to {Language}", language ?? "system");
        }

        private void OnToggleStartWithWindows(object? sender, EventArgs e)
        {
            _settingsService.Settings.StartWithWindows = _startWithWindowsItem.Checked;
            _settingsService.Save();
            StartupService.Apply(_startWithWindowsItem.Checked);
        }

        private void OnToggleAll(object? sender, EventArgs e)
        {
            if (_suppressToggleEvents) return;

            _suppressToggleEvents = true;
            bool enabled = _notifAll.Checked;
            NotifNoDevice.Checked = enabled;
            NotifConnected.Checked = enabled;
            NotifDisconnected.Checked = enabled;
            _suppressToggleEvents = false;

            SaveNotificationSettings();
        }

        private void OnToggleIndividual(object? sender, EventArgs e)
        {
            if (_suppressToggleEvents) return;

            _suppressToggleEvents = true;
            _notifAll.Checked = NotifNoDevice.Checked && NotifConnected.Checked && NotifDisconnected.Checked;
            _suppressToggleEvents = false;

            SaveNotificationSettings();
        }

        private void SaveNotificationSettings()
        {
            _settingsService.Settings.NotifyNoDevice = NotifNoDevice.Checked;
            _settingsService.Settings.NotifyConnected = NotifConnected.Checked;
            _settingsService.Settings.NotifyDisconnected = NotifDisconnected.Checked;
            _settingsService.Save();
            Log.Debug("Notification settings saved");
        }
    }
}
