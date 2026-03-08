namespace ArctisBatteryMonitor.Models
{
    internal static class KnownHeadsets
    {
        public static readonly IReadOnlyList<HeadsetInfo> All =
        [
            new("Arctis Pro Wireless", 0x1290, [0x40, 0xaa]),
            new("Arctis 7 2017", 0x1260, [0x06, 0x18]),
            new("Arctis 7 2019", 0x12ad, [0x06, 0x18]),
            new("Arctis Pro 2019", 0x1252, [0x06, 0x18]),
            new("Arctis Pro GameDac", 0x1280, [0x06, 0x18]),
            new("Arctis 9", 0x12c2, [0x00, 0x20]),
            new("Arctis 1 Wireless", 0x12b3, [0x06, 0x12]),
            new("Arctis 1 Xbox", 0x12b6, [0x06, 0x12]),
            new("Arctis 7X", 0x12d7, [0x06, 0x12]),
            new("Arctis 7+", 0x220e, [0x00, 0xb0]),
            new("Arctis 7P+", 0x2212, [0x00, 0xb0]),
            new("Arctis 7X+", 0x2216, [0x00, 0xb0]),
            new("Arctis 7 Destiny Plus", 0x2236, [0x00, 0xb0]),
            new("Arctis Nova 7", 0x2202, [0x00, 0xb0]),
            new("Arctis Nova 7X", 0x2206, [0x00, 0xb0]),
            new("Arctis Nova 7X v2", 0x2258, [0x00, 0xb0]),
            new("Arctis Nova 7P", 0x220a, [0x00, 0xb0]),
            new("Arctis Nova 7 Diablo IV", 0x223a, [0x00, 0xb0]),
            new("Arctis Nova 5", 0x2232, [0x00, 0xb0]),
            new("Arctis Nova 5X", 0x2253, [0x00, 0xb0]),
        ];
    }
}
