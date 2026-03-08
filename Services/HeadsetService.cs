using ArctisBatteryMonitor.Models;
using HidSharp;

namespace ArctisBatteryMonitor.Services
{
    internal class HeadsetService
    {
        private const int VendorId = 0x1038;
        private const int BatteryLevelIndex = 3;
        private const int ChargingStatusIndex = 4;
        private const double BatteryMaxRaw = 4.0;

        private static readonly List<HeadsetInfo> KnownHeadsets =
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
            new("Arctis Nova 5X", 0x2253, [0x00, 0xb0])
        ];

        private HeadsetInfo? _chosenDevice;
        public HeadsetInfo? ChosenDevice => _chosenDevice;

        private List<HeadsetInfo> _connectedDevices = [];
        public IReadOnlyList<HeadsetInfo> ConnectedDevices => _connectedDevices;

        public void ScanForDevices()
        {
            _connectedDevices.Clear();

            foreach (var headset in KnownHeadsets)
            {
                if (DeviceList.Local.GetHidDevices(VendorId, headset.ProductId).Any())
                {
                    _connectedDevices.Add(headset);
                }
            }

            if (_connectedDevices.Count > 0)
                _chosenDevice = _connectedDevices[0];
        }

        public HeadsetStatus GetStatus()
        {
            if (_chosenDevice is null)
            {
                ScanForDevices();

                if (_chosenDevice is null)
                    return new HeadsetStatus(HeadsetState.Searching, null, 0, "Disconnected");
            }

            var devices = DeviceList.Local.GetHidDevices(VendorId, _chosenDevice.ProductId);
            bool isConnected = false;

            foreach (var device in devices)
            {
                if (!device.TryOpen(out HidStream? hidStream))
                    continue;

                using (hidStream)
                {
                    try
                    {
                        hidStream.Write(_chosenDevice.OutputBuffer, 0, _chosenDevice.OutputBuffer.Length);
                        hidStream.ReadTimeout = 1000;

                        var inputBuffer = new byte[device.GetMaxInputReportLength()];
                        int bytesRead = hidStream.Read(inputBuffer);

                        if (bytesRead > 0)
                        {
                            isConnected = inputBuffer[ChargingStatusIndex] != 0;

                            if (isConnected)
                            {
                                double batteryLevel = (inputBuffer[BatteryLevelIndex] / BatteryMaxRaw) * 100.0;
                                string chargingStatus = inputBuffer[ChargingStatusIndex] switch
                                {
                                    1 => "Charging",
                                    3 => "Discharging",
                                    _ => "Disconnected"
                                };

                                return new HeadsetStatus(HeadsetState.Connected, _chosenDevice, batteryLevel, chargingStatus);
                            }
                        }
                    }
                    catch
                    {
                        continue;
                    }
                }
            }

            return _chosenDevice is not null
                ? new HeadsetStatus(HeadsetState.Connecting, _chosenDevice, 0, "Disconnected")
                : new HeadsetStatus(HeadsetState.Searching, null, 0, "Disconnected");
        }

        public void Reset()
        {
            _chosenDevice = null;
            _connectedDevices.Clear();
        }
    }
}
