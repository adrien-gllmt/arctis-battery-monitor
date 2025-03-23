using System.ComponentModel;
using ArctisBatteryMonitor.Services;
using Timer = System.Timers.Timer;

namespace ArctisBatteryMonitor
{
    internal class BatteryMonitor : ApplicationContext
    {
        private readonly NotifyIcon _notifyIcon = new()
        {
            Text = "Arctis Battery Monitor",
            ContextMenuStrip = new ContextMenuStrip()
        };
        private readonly HeadsetService _headsetService = new();
        private readonly BackgroundWorker HeadsetServiceWorker = new() { WorkerSupportsCancellation = true };

        private static readonly Timer connectingIconsTimer = new(300);

        private static readonly int retryDelay = 15_000;
        private static readonly string retryText = "Retrying in 15 seconds";
        private static readonly int refreshDelay = 30_000;

        private static int connectingIconState = 1;
        private static List<int> possibleConnectingIconStates = [1, 2, 3, 4];
        private bool hasBeenDisconnected = false;

        public BatteryMonitor()
        {
            connectingIconsTimer.Elapsed += SwitchIcon;
            connectingIconsTimer.Start();

            HeadsetServiceWorker.DoWork += HeadsetMonitor;

            _notifyIcon.ContextMenuStrip.Items.Add("Reconnect", null, Reconnect);
            _notifyIcon.ContextMenuStrip.Items.Add(new ToolStripSeparator());
            _notifyIcon.ContextMenuStrip.Items.Add("Exit", null, Exit);

            _notifyIcon.Visible = true;

            HeadsetServiceWorker.RunWorkerAsync();
        }

        private void SwitchIcon(object sender, EventArgs e)
        {
            if (_headsetService._isConnected)
            {
                connectingIconsTimer.Stop();
                return;
            }

            _notifyIcon.Icon = new System.Drawing.Icon($"Resources/headset_connecting_{connectingIconState}.ico");
            connectingIconState = possibleConnectingIconStates.Count == connectingIconState ? possibleConnectingIconStates[0] : connectingIconState + 1;
        }

        private void Reconnect(object sender, EventArgs e)
        {
            HeadsetServiceWorker.CancelAsync();
            if (!HeadsetServiceWorker.IsBusy) HeadsetServiceWorker.RunWorkerAsync();
        }

        private void Exit(object sender, EventArgs e)
        {
            _notifyIcon.Visible = false;
            _notifyIcon.Dispose();
            Application.Exit();
        }

        private void HeadsetMonitor (object sender, DoWorkEventArgs e)
        {
            while (!HeadsetServiceWorker.CancellationPending)
            {
                if (_headsetService.connectedDevices.Count == 0)
                {
                    while (_headsetService.connectedDevices.Count == 0)
                    {
                        _notifyIcon.ShowBalloonTip(2_000, "No devices found", retryText, ToolTipIcon.Info);
                        Thread.Sleep(retryDelay);
                        _headsetService.GetConnectedHeadsets();
                    }
                }

                _headsetService.GetBatteryLevel();

                if (!hasBeenDisconnected && !_headsetService.isInit && !_headsetService._isConnected)
                {
                    while (!_headsetService._isConnected)
                    {
                        _notifyIcon.ShowBalloonTip(2_000, "Unable to connect", retryText, ToolTipIcon.Info);
                        Thread.Sleep(retryDelay);
                        _headsetService.GetBatteryLevel();
                    }
                }

                if (!_headsetService.isInit && _headsetService._isConnected && _headsetService.chargingStatus != "Disconnected")
                {
                    _headsetService._isConnected = true;
                    _headsetService.isInit = true;

                    _notifyIcon.Icon = new System.Drawing.Icon($"Resources/battery-{_headsetService.batteryLevel}.ico");

                    if (_headsetService.connectedDevices.Count > 1)
                    {
                        _notifyIcon.ShowBalloonTip(2_000, "Multiple devices detected", $"Defaulted to {_headsetService.ChosenDevice.name}", ToolTipIcon.Info);
                        continue;
                    }

                    _notifyIcon.ShowBalloonTip(2_000, "Successfully connected", $"{_headsetService.ChosenDevice.name}", ToolTipIcon.Info);
                }

                if (_headsetService.isInit && !_headsetService._isConnected && _headsetService.chargingStatus == "Disconnected")
                {
                    _notifyIcon.ShowBalloonTip(2_000, "Disconnected", $"{_headsetService.ChosenDevice.name}", ToolTipIcon.Info);
                    _headsetService._isConnected = false;
                    _headsetService.isInit = false;
                    hasBeenDisconnected = true;
                    _notifyIcon.Icon = new System.Drawing.Icon("Resources/headset_disconnected.ico");
                }

                Thread.Sleep(hasBeenDisconnected ? 5_000 : refreshDelay);
            }
        }
    }
}
