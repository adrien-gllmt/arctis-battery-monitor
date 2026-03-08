namespace ArctisBatteryMonitor.UI
{
    internal class MenuRenderer : ToolStripProfessionalRenderer
    {
        private static readonly Color Background = Color.FromArgb(32, 32, 32);
        private static readonly Color HoverBackground = Color.FromArgb(55, 55, 55);
        private static readonly Color TextColor = Color.FromArgb(230, 230, 230);
        private static readonly Color SeparatorColor = Color.FromArgb(60, 60, 60);
        private static readonly Color CheckColor = Color.FromArgb(100, 180, 255);
        private static readonly Color BorderColor = Color.FromArgb(50, 50, 50);

        private const int MenuPadding = 4;

        public MenuRenderer() : base(new MenuColorTable()) { }

        protected override void OnRenderToolStripBackground(ToolStripRenderEventArgs e)
        {
            using var brush = new SolidBrush(Background);
            e.Graphics.FillRectangle(brush, e.AffectedBounds);
        }

        protected override void OnRenderToolStripBorder(ToolStripRenderEventArgs e)
        {
            var rect = new Rectangle(0, 0, e.ToolStrip.Width - 1, e.ToolStrip.Height - 1);
            using var pen = new Pen(BorderColor);
            e.Graphics.DrawRectangle(pen, rect);
        }

        protected override void OnRenderMenuItemBackground(ToolStripItemRenderEventArgs e)
        {
            if (e.Item.Selected || e.Item.Pressed)
            {
                var rect = new Rectangle(MenuPadding, 1, e.Item.Width - MenuPadding * 2, e.Item.Height - 2);
                using var brush = new SolidBrush(HoverBackground);
                e.Graphics.FillRectangle(brush, rect);
            }
        }

        protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
        {
            e.TextColor = e.Item.Enabled ? TextColor : Color.FromArgb(100, 100, 100);
            base.OnRenderItemText(e);
        }

        protected override void OnRenderItemCheck(ToolStripItemImageRenderEventArgs e)
        {
            e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

            int dotSize = 8;
            int x = e.ImageRectangle.X + (e.ImageRectangle.Width - dotSize) / 2;
            int y = e.ImageRectangle.Y + (e.ImageRectangle.Height - dotSize) / 2;

            using var brush = new SolidBrush(CheckColor);
            e.Graphics.FillEllipse(brush, x, y, dotSize, dotSize);
        }

        protected override void OnRenderSeparator(ToolStripSeparatorRenderEventArgs e)
        {
            int y = e.Item.Height / 2;
            using var pen = new Pen(SeparatorColor);
            e.Graphics.DrawLine(pen, MenuPadding + 8, y, e.Item.Width - MenuPadding - 8, y);
        }

        protected override void OnRenderArrow(ToolStripArrowRenderEventArgs e)
        {
            e.ArrowColor = TextColor;
            base.OnRenderArrow(e);
        }
    }

    internal class MenuColorTable : ProfessionalColorTable
    {
        private static readonly Color Bg = Color.FromArgb(32, 32, 32);

        public override Color ToolStripDropDownBackground => Bg;
        public override Color MenuBorder => Color.FromArgb(50, 50, 50);
        public override Color MenuItemBorder => Color.Transparent;
        public override Color MenuItemSelected => Color.Transparent;
        public override Color MenuItemSelectedGradientBegin => Color.Transparent;
        public override Color MenuItemSelectedGradientEnd => Color.Transparent;
        public override Color MenuItemPressedGradientBegin => Bg;
        public override Color MenuItemPressedGradientEnd => Bg;
        public override Color ImageMarginGradientBegin => Bg;
        public override Color ImageMarginGradientMiddle => Bg;
        public override Color ImageMarginGradientEnd => Bg;
        public override Color SeparatorDark => Color.FromArgb(60, 60, 60);
        public override Color SeparatorLight => Color.Transparent;
    }
}
