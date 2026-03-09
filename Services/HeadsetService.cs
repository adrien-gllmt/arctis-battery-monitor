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

        private static class ChargingStatusCode
        {
            public const int Charging = 1;
            public const int Discharging = 3;
        }

        public int? PreferredProductId { get; set; }

        private HeadsetInfo? _chosenDevice;
        public HeadsetInfo? ChosenDevice => _chosenDevice;

        private List<HeadsetInfo> _connectedDevices = [];
        public IReadOnlyList<HeadsetInfo> ConnectedDevices => _connectedDevices;

        public void ScanForDevices()
        {
            _connectedDevices.Clear();
            Log.Debug("Scanning for HID devices (vendor 0x{VendorId:X4})", VendorId);

            foreach (var headset in KnownHeadsets.All)
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
                                ChargingStatusCode.Charging    => "Charging",
                                ChargingStatusCode.Discharging => "Discharging",
                                _                              => "Disconnected"
                            };

                            Log.Debug("{Name}: battery {Battery}%, {Status}", _chosenDevice.Name, batteryLevel, chargingStatus);
                            return new HeadsetStatus(HeadsetState.Connected, _chosenDevice, batteryLevel, chargingStatus);
                        }
                    }
                    catch (TimeoutException)
                    {
                        Log.Debug("HID read timed out for {Name}", _chosenDevice.Name);
                        continue;
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
