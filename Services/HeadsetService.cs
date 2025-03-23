using HidSharp;

namespace ArctisBatteryMonitor.Services
{
    internal class HeadsetService
    {
        public List<(string Name, int ProductId, byte[] outputBuffer)> connectedDevices = [];
        private static readonly List<(string name, int productId, byte[] outputBuffer)> _knownHeadsets =
        [
            ("Arctis Pro Wireless", 0x1290, [0x40, 0xaa]),
            ("Arctis 7 2017", 0x1260, [0x06, 0x18]),
            ("Arctis 7 2019", 0x12ad, [0x06, 0x18]),
            ("Arctis Pro 2019", 0x1252, [0x06, 0x18]),
            ("Arctis Pro GameDac", 0x1280, [0x06, 0x18]),
            ("Arctis 9", 0x12c2, [0x00, 0x20]),
            ("Arctis 1 Wireless", 0x12b3, [0x06, 0x12]),
            ("Arctis 1 Xbox", 0x12b6, [0x06, 0x12]),
            ("Arctis 7X", 0x12d7, [0x06, 0x12]),
            ("Arctis 7+", 0x220e, [0x00, 0xb0]),
            ("Arctis 7P+", 0x2212, [0x00, 0xb0]),
            ("Arctis 7X+", 0x2216, [0x00, 0xb0]),
            ("Arctis 7 Destiny Plus", 0x2236, [0x00, 0xb0]),
            ("Arctis Nova 7", 0x2202, [0x00, 0xb0]),
            ("Arctis Nova 7X", 0x2206, [0x00, 0xb0]),
            ("Arctis Nova 7X v2", 0x2258, [0x00, 0xb0]),
            ("Arctis Nova 7P", 0x220a, [0x00, 0xb0]),
            ("Arctis Nova 7 Diablo IV", 0x223a, [0x00, 0xb0]),
            ("Arctis Nova 5", 0x2232, [0x00, 0xb0]),
            ("Arctis Nova 5X", 0x2253, [0x00, 0xb0])
        ];

        private static readonly int _vendorId = 0x1038;

        private (string name, int productId, byte[] outputBuffer) _chosenDevice;
        public (string name, int productId, byte[] outputBuffer) ChosenDevice { get => _chosenDevice; }
        private HidDevice? _device;

        public bool _isConnected = false;
        public bool isInit = false;

        public double batteryLevel;
        public string chargingStatus = "Disconnected";

        public HeadsetService()
        {
            GetConnectedHeadsets();
            _chosenDevice = connectedDevices.First();
        }

        public void GetConnectedHeadsets() {
            foreach ((string Name, int ProductId, byte[] outputBuffer) in _knownHeadsets)
            {
                if (DeviceList.Local.GetHidDevices(_vendorId, ProductId).Any())
                {
                    connectedDevices.Clear();
                    connectedDevices.Add((Name, ProductId, outputBuffer));
                }
            }
        }

        private void ScanInterfaces(IEnumerable<HidDevice> chosenDevice)
        {
            foreach (var device in chosenDevice) {
                if (device is null) continue;

                if (device.TryOpen(out HidStream hidStream))
                {
                    byte[] inputBuffer;
                    try
                    {
                        WriteOutputReport(hidStream);
                        hidStream.ReadTimeout = 1000;
                        inputBuffer = new byte[device.GetMaxInputReportLength()];
                        int bytesRead = hidStream.Read(inputBuffer);
                    }
                    catch (Exception)
                    {
                        hidStream.Close();
                        continue;
                    }

                    hidStream.Close();
                    _device = device;
                    _isConnected = inputBuffer[4] == 0 ? false : true;
                    break;
                }
            }
        }

        private void WriteOutputReport(HidStream hidStream)
        {
            hidStream.Write(_chosenDevice.outputBuffer, 0, _chosenDevice.outputBuffer.Length);
        }

        private void ReadInputReport(HidDevice device, HidStream hidStream)
        {
            byte[] inputBuffer = new byte[device.GetMaxInputReportLength()];
            int bytesRead = hidStream.Read(inputBuffer);
            if (bytesRead > 0)
            {
                batteryLevel = (inputBuffer[3] / 4.0) * 100.0;
                chargingStatus = inputBuffer[4] switch
                {
                    1 => "Charging",
                    3 => "Discharging",
                    _ => "Disconnected"
                };
            }
        }

        public void GetBatteryLevel()
        {
            ScanInterfaces(DeviceList.Local.GetHidDevices(_vendorId, _chosenDevice.productId));

            if (_device is null || !_isConnected) {
                chargingStatus = "Disconnected";
                return;
            }

            if(_device.TryOpen(out HidStream hidStream))
            {
                WriteOutputReport(hidStream);
                ReadInputReport(_device, hidStream);
            }
        }
    }
}
