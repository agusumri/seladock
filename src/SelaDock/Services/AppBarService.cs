using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using SelaDock.Interop;

namespace SelaDock.Services;

/// <summary>
/// Registers the dock window as a bottom app bar so that maximized windows leave room for it,
/// the same mechanism the Windows taskbar uses. When disabled the dock simply floats on top and
/// windows can sit beneath it.
/// </summary>
public sealed class AppBarService : IDisposable
{
    private readonly Window _window;
    private nint _hWnd;
    private bool _registered;

    public AppBarService(Window window) => _window = window;

    /// <summary>Reserve a strip of <paramref name="heightPx"/> device pixels at the bottom of the primary screen.</summary>
    public void Reserve(int heightPx)
    {
        _hWnd = new WindowInteropHelper(_window).Handle;
        if (_hWnd == nint.Zero) return;

        var data = new Native.APPBARDATA
        {
            cbSize = Marshal.SizeOf<Native.APPBARDATA>(),
            hWnd = _hWnd,
            uEdge = Native.ABE_BOTTOM,
        };

        if (!_registered)
        {
            Native.SHAppBarMessage(Native.ABM_NEW, ref data);
            _registered = true;
        }

        var screen = SystemParameters.WorkArea; // start from the current work area
        int screenRight = (int)SystemParameters.PrimaryScreenWidth;
        int screenBottom = (int)SystemParameters.PrimaryScreenHeight;

        data.rc = new Native.RECT
        {
            Left = 0,
            Right = screenRight,
            Top = screenBottom - heightPx,
            Bottom = screenBottom,
        };

        Native.SHAppBarMessage(Native.ABM_QUERYPOS, ref data);
        data.rc.Top = data.rc.Bottom - heightPx;
        Native.SHAppBarMessage(Native.ABM_SETPOS, ref data);
    }

    private void Remove()
    {
        if (!_registered || _hWnd == nint.Zero) return;
        var data = new Native.APPBARDATA
        {
            cbSize = Marshal.SizeOf<Native.APPBARDATA>(),
            hWnd = _hWnd,
        };
        Native.SHAppBarMessage(Native.ABM_REMOVE, ref data);
        _registered = false;
    }

    public void Dispose() => Remove();
}
