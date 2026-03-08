using ArctisBatteryMonitor.Models;
using HidSharp;
using Serilog;

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

        public int? PreferredProductId { get; set; }

        private HeadsetInfo? _chosenDevice;
        public HeadsetInfo? ChosenDevice => _chosenDevice;

        private List<HeadsetInfo> _connectedDevices = [];
        public IReadOnlyList<HeadsetInfo> ConnectedDevices => _connectedDevices;

        public void ScanForDevices()
        {
            _connectedDevices.Clear();
            Log.Debug("Scanning for HID devices (vendor 0x{VendorId:X4})", VendorId);

            foreach (var headset in KnownHeadsets)
            {
                if (DeviceList.Local.GetHidDevices(VendorId, headset.ProductId).Any())
                {
                    Log.Information("Found device: {Name} (0x{ProductId:X4})", headset.Name, headset.ProductId);
                    _connectedDevices.Add(headset);
                }
            }

            if (_connectedDevices.Count > 0)
            {
                _chosenDevice = _connectedDevices.FirstOrDefault(d => d.ProductId == PreferredProductId)
                                ?? _connectedDevices[0];
                Log.Information("Selected device: {Name}, {Count} total found", _chosenDevice.Name, _connectedDevices.Count);
            }
            else
            {
                Log.Debug("No devices found");
            }
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

            foreach (var device in devices)
            {
                if (!device.TryOpen(out HidStream? hidStream))
                {
                    Log.Debug("Failed to open HID stream for {Name}", _chosenDevice.Name);
                    continue;
                }

                using (hidStream)
                {
                    try
                    {
                        hidStream.Write(_chosenDevice.OutputBuffer, 0, _chosenDevice.OutputBuffer.Length);
                        hidStream.ReadTimeout = 1000;

                        var inputBuffer = new byte[device.GetMaxInputReportLength()];
                        int bytesRead = hidStream.Read(inputBuffer);

                        if (bytesRead > 0 && inputBuffer[ChargingStatusIndex] != 0)
                        {
                            double batteryLevel = (inputBuffer[BatteryLevelIndex] / BatteryMaxRaw) * 100.0;
                            string chargingStatus = inputBuffer[ChargingStatusIndex] switch
                            {
                                1 => "Charging",
                                3 => "Discharging",
                                _ => "Disconnected"
                            };

                            Log.Debug("{Name}: battery {Battery}%, {Status}", _chosenDevice.Name, batteryLevel, chargingStatus);
                            return new HeadsetStatus(HeadsetState.Connected, _chosenDevice, batteryLevel, chargingStatus);
                        }
                    }
                    catch (Exception ex)
                    {
                        Log.Warning(ex, "HID read error for {Name}", _chosenDevice.Name);
                        continue;
                    }
                }
            }

            return _chosenDevice is not null
                ? new HeadsetStatus(HeadsetState.Connecting, _chosenDevice, 0, "Disconnected")
                : new HeadsetStatus(HeadsetState.Searching, null, 0, "Disconnected");
        }

        public void SelectDevice(HeadsetInfo device)
        {
            Log.Information("User selected device: {Name}", device.Name);
            _chosenDevice = device;
        }

        public void Reset()
        {
            Log.Debug("HeadsetService reset");
            _chosenDevice = null;
            _connectedDevices.Clear();
        }
    }
}
