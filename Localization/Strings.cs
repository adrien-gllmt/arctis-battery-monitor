using System.Globalization;
using System.Resources;

namespace ArctisBatteryMonitor.Localization
{
    internal static class Strings
    {
        private static readonly ResourceManager Rm = new(
            "ArctisBatteryMonitor.Resources.Localization.Strings",
            typeof(Strings).Assembly);

        private static string Get(string key) =>
            Rm.GetString(key, CultureInfo.CurrentUICulture) ?? key;

        // Menu
        public static string MenuReconnect         => Get(nameof(MenuReconnect));
        public static string MenuSelectHeadset     => Get(nameof(MenuSelectHeadset));
        public static string NoDeviceFound         => Get(nameof(NoDeviceFound));
        public static string MenuNotifications     => Get(nameof(MenuNotifications));
        public static string MenuNotifConnected    => Get(nameof(MenuNotifConnected));
        public static string MenuNotifDisconnected => Get(nameof(MenuNotifDisconnected));
        public static string MenuNotifEnableAll    => Get(nameof(MenuNotifEnableAll));
        public static string MenuLanguage          => Get(nameof(MenuLanguage));
        public static string MenuLanguageSystem    => Get(nameof(MenuLanguageSystem));
        public static string MenuExit              => Get(nameof(MenuExit));

        // Notifications
        public static string NotifNoDeviceTitle     => Get(nameof(NotifNoDeviceTitle));
        public static string NotifNoDeviceMessage   => Get(nameof(NotifNoDeviceMessage));
        public static string NotifMultipleTitle     => Get(nameof(NotifMultipleTitle));
        public static string NotifMultipleMessage   => Get(nameof(NotifMultipleMessage));
        public static string NotifConnectedTitle    => Get(nameof(NotifConnectedTitle));
        public static string NotifDisconnectedTitle => Get(nameof(NotifDisconnectedTitle));
    }
}
