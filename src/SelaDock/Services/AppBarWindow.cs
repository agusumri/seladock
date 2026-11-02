using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Media;
using SelaDock.Interop;

namespace SelaDock.Services;

/// <summary>
/// A tiny, opaque, 1px-tall window pinned to the bottom edge that registers itself as a Windows
/// app bar. Windows won't honor an app-bar reservation from the transparent dock pill, so this
/// separate non-layered window holds the reservation while the visible dock floats above it.
///
/// It is a normal WPF <see cref="Window"/> (so it has a valid composition target and never crashes
/// on paint, unlike a bare HwndSource), but it is kept 1px tall, click-through, and off the taskbar
/// so the user never sees or interacts with it.
/// </summary>
public sealed class AppBarWindow : Window
{
    private nint _hWnd;
    private bool _registered;
    private int _heightLogical;

    public AppBarWindow()
    {
        WindowStyle = WindowStyle.None;
        ShowInTaskbar = false;
        ResizeMode = ResizeMode.NoResize;
        Background = Brushes.Black; // opaque; it is 1px tall and sits off-screen edge, never visible
        Width = 1;
        Height = 1;
        Left = 0;
        Top = SystemParameters.PrimaryScreenHeight - 1;
        Opacity = 0; // fully transparent to the eye WITHOUT being a layered/AllowsTransparency window
        ShowActivated = false;
    }

    /// <summary>Reserve the strip and return the rectangle (device px) the shell actually granted.</summary>
    internal Native.RECT Reserve(int heightLogical)
    {
        _heightLogical = heightLogical;
        _hWnd = new System.Windows.Interop.WindowInteropHelper(this).EnsureHandle();

        if (!_registered)
        {
            var reg = new Native.APPBARDATA
            {
                cbSize = Marshal.SizeOf<Native.APPBARDATA>(),
                hWnd = _hWnd,
                uCallbackMessage = Native.RegisterWindowMessageW("SelaDockAppBarMsg"),
            };
            Native.SHAppBarMessage(Native.ABM_NEW, ref reg);
            _registered = true;
        }

        var dpi = VisualTreeHelper.GetDpi(this);
        int heightPx = (int)Math.Ceiling(heightLogical * dpi.DpiScaleY);
        int screenRight = (int)Math.Round(SystemParameters.PrimaryScreenWidth * dpi.DpiScaleX);
        int screenBottom = (int)Math.Round(SystemParameters.PrimaryScreenHeight * dpi.DpiScaleY);

        var data = new Native.APPBARDATA
        {
            cbSize = Marshal.SizeOf<Native.APPBARDATA>(),
            hWnd = _hWnd,
            uEdge = Native.ABE_BOTTOM,
            rc = new Native.RECT { Left = 0, Right = screenRight, Top = screenBottom - heightPx, Bottom = screenBottom },
        };
        Native.SHAppBarMessage(Native.ABM_QUERYPOS, ref data);
        data.rc.Top = data.rc.Bottom - heightPx;
        Native.SHAppBarMessage(Native.ABM_SETPOS, ref data);
        return data.rc;
    }

    public void Unregister()
    {
        if (!_registered || _hWnd == nint.Zero) return;
        var data = new Native.APPBARDATA { cbSize = Marshal.SizeOf<Native.APPBARDATA>(), hWnd = _hWnd };
        Native.SHAppBarMessage(Native.ABM_REMOVE, ref data);
        _registered = false;
    }
}
