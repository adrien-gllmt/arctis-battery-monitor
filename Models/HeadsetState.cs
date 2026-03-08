namespace ArctisBatteryMonitor.Models
{
    public enum HeadsetState
    {
        Searching,
        Connecting,
        Connected,
        Disconnected
    }

    public record HeadsetInfo(string Name, int ProductId, byte[] OutputBuffer);

    public record HeadsetStatus(
        HeadsetState State,
        HeadsetInfo? Device,
        double BatteryLevel,
        string ChargingStatus);
}
