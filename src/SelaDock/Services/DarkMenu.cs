using System.Drawing;
using Forms = System.Windows.Forms;

namespace SelaDock.Services;

/// <summary>
/// Dark styling for the WinForms tray <see cref="Forms.ContextMenuStrip"/> so it matches the dock's
/// glass look instead of the default light Windows menu. WinForms can't do real transparency here,
/// so this uses a deep, near-black palette with a subtle hover highlight to read as the same theme.
/// </summary>
internal static class DarkMenu
{
    // Matches the dock palette (Theme.xaml DockBg / highlight), opaque equivalents.
    public static readonly Color Back = Color.FromArgb(24, 25, 30);
    public static readonly Color Hover = Color.FromArgb(48, 50, 60);
    public static readonly Color Text = Color.FromArgb(244, 245, 250);
    public static readonly Color SubText = Color.FromArgb(170, 174, 184);
    public static readonly Color Border = Color.FromArgb(60, 62, 72);
    public static readonly Color Sep = Color.FromArgb(70, 72, 82);

    public static void Apply(Forms.ToolStrip menu)
    {
        menu.RenderMode = Forms.ToolStripRenderMode.Professional;
        menu.Renderer = new Renderer();
        menu.BackColor = Back;
        menu.ForeColor = Text;
        Style(menu.Items);
    }

    private static void Style(Forms.ToolStripItemCollection items)
    {
        foreach (Forms.ToolStripItem item in items)
        {
            item.BackColor = Back;
            item.ForeColor = Text;
            if (item is Forms.ToolStripMenuItem mi && mi.HasDropDownItems)
            {
                mi.DropDown.BackColor = Back;
                if (mi.DropDown is Forms.ToolStripDropDownMenu ddm) ddm.Renderer = new Renderer();
                Style(mi.DropDownItems);
            }
        }
    }

    private sealed class Colors : Forms.ProfessionalColorTable
    {
        public Colors() => UseSystemColors = false;
        public override Color MenuItemSelected => Hover;
        public override Color MenuItemSelectedGradientBegin => Hover;
        public override Color MenuItemSelectedGradientEnd => Hover;
        public override Color MenuItemBorder => Hover;
        public override Color MenuBorder => Border;
        public override Color ToolStripDropDownBackground => Back;
        public override Color ImageMarginGradientBegin => Back;
        public override Color ImageMarginGradientMiddle => Back;
        public override Color ImageMarginGradientEnd => Back;
        public override Color MenuItemPressedGradientBegin => Hover;
        public override Color MenuItemPressedGradientEnd => Hover;
        public override Color SeparatorDark => Sep;
        public override Color SeparatorLight => Sep;
        public override Color CheckBackground => Hover;
        public override Color CheckSelectedBackground => Hover;
    }

    private sealed class Renderer : Forms.ToolStripProfessionalRenderer
    {
        public Renderer() : base(new Colors()) { }

        protected override void OnRenderItemText(Forms.ToolStripItemTextRenderEventArgs e)
        {
            e.TextColor = e.Item.Enabled ? Text : SubText;
            base.OnRenderItemText(e);
        }

        protected override void OnRenderArrow(Forms.ToolStripArrowRenderEventArgs e)
        {
            e.ArrowColor = Text; // submenu chevron
            base.OnRenderArrow(e);
        }
    }
}
