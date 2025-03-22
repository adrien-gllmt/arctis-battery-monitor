using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace arctis_battery_monitor
{
    internal class BatteryMonitor : ApplicationContext
    {
        private readonly NotifyIcon _notifyIcon;

        public BatteryMonitor()
        {
            _notifyIcon = new NotifyIcon
            {
                Icon = new System.Drawing.Icon("Resources/battery-100.ico"),
                Text = "Arctis Battery Monitor",

                ContextMenuStrip = new ContextMenuStrip()
            };

            _notifyIcon.ContextMenuStrip.Items.Add("Reconnect", null, Reconnect);
            _notifyIcon.ContextMenuStrip.Items.Add(new ToolStripSeparator());
            _notifyIcon.ContextMenuStrip.Items.Add("Exit", null, Exit);

            _notifyIcon.Visible = true;
        }

        private void Exit(object sender, EventArgs e)
        {
            _notifyIcon.Visible = false;
            _notifyIcon.Dispose();
            Application.Exit();
        }

        private void Reconnect(object sender, EventArgs e)
        {

        }
    }
}
