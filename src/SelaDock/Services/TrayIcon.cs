using System.IO;
using System.Windows;
using Forms = System.Windows.Forms;

namespace SelaDock.Services;

/// <summary>Notification-area icon with a small context menu to control the dock and quit.</summary>
public sealed class TrayIcon : IDisposable
{
    private readonly Forms.NotifyIcon _icon;
    private readonly System.Drawing.Icon? _ownedIcon;

    public TrayIcon(Action toggleAutoHide, Func<bool> isAutoHide,
        Action<DockSize> setSize, Func<DockSize> currentSize, Action exit)
    {
        _ownedIcon = LoadIcon();

        var menu = new Forms.ContextMenuStrip();

        // Dock size submenu
        var sizeMenu = new Forms.ToolStripMenuItem("Dock size");
        var sizeItems = new (DockSize Size, string Label)[]
        {
            (DockSize.Auto, "Auto (fit screen)"),
            (DockSize.Small, "Small"),
            (DockSize.Medium, "Medium"),
            (DockSize.Large, "Large"),
        };
        foreach (var (size, label) in sizeItems)
        {
            var it = new Forms.ToolStripMenuItem(label) { Tag = size, CheckOnClick = false };
            it.Click += (_, _) => setSize(size);
            sizeMenu.DropDownItems.Add(it);
        }
        sizeMenu.DropDownOpening += (_, _) =>
        {
            foreach (Forms.ToolStripMenuItem it in sizeMenu.DropDownItems)
                it.Checked = (DockSize)it.Tag! == currentSize();
        };
        menu.Items.Add(sizeMenu);

        var autoHide = new Forms.ToolStripMenuItem("Auto-hide dock") { CheckOnClick = true };
        autoHide.Click += (_, _) => toggleAutoHide();
        menu.Items.Add(autoHide);

        menu.Opening += (_, _) => autoHide.Checked = isAutoHide();

        menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add(new Forms.ToolStripMenuItem("Quit SelaDock", null, (_, _) => exit()));

        DarkMenu.Apply(menu); // dark theme to match the dock

        _icon = new Forms.NotifyIcon
        {
            Icon = _ownedIcon ?? System.Drawing.SystemIcons.Application,
            Text = "SelaDock",
            ContextMenuStrip = menu,
            Visible = true,
        };
    }

    private static System.Drawing.Icon? LoadIcon()
    {
        try
        {
            var info = Application.GetResourceStream(new Uri("pack://application:,,,/Assets/SelaDock.ico"));
            if (info is null) return null;
            using var stream = info.Stream;
            return new System.Drawing.Icon(stream, Forms.SystemInformation.SmallIconSize);
        }
        catch (Exception ex) when (ex is IOException or ArgumentException or UriFormatException)
        {
            return null;
        }
    }

    public void Dispose()
    {
        _icon.Visible = false;
        _icon.ContextMenuStrip?.Dispose();
        _icon.Dispose();
        _ownedIcon?.Dispose();
    }
}
