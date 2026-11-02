using SelaDock.Interop;

namespace SelaDock.Services;

/// <summary>
/// Hides the Windows taskbar (and the Start orb and any secondary-monitor taskbars) while SelaDock
/// runs, so the dock can act as a clean replacement, then shows them again on exit.
///
/// Auto-hide alone is leaky (Start / Quick Settings slide the taskbar back over the dock), and hiding
/// alone leaves the taskbar's work-area strip in place. So both are applied, and a WinEvent hook
/// re-hides the taskbar the moment Explorer shows it again. Nothing is removed permanently — every window is shown again in <see cref="Dispose"/>, and if the
/// process is killed, Explorer restores the taskbar on its next refresh (e.g. sign-out/in).
/// </summary>
public sealed class TaskbarService : IDisposable
{
    private readonly List<nint> _hidden = new();
    private bool _active;
    private int _prevState;
    private bool _autoHideApplied;

    /// <summary>
    /// Set the Windows taskbar to auto-hide (not ShowWindow-hide). This keeps the taskbar as a
    /// registered app bar so it does NOT reset the desktop work area, which lets the dock's own app
    /// bar reservation stick. Restored on <see cref="Dispose"/>.
    /// </summary>
    public void AutoHide()
    {
        var hWnd = Native.FindWindowW("Shell_TrayWnd", null);
        if (hWnd == nint.Zero) return;
        var data = new Native.APPBARDATA
        {
            cbSize = System.Runtime.InteropServices.Marshal.SizeOf<Native.APPBARDATA>(),
            hWnd = hWnd,
        };
        _prevState = (int)Native.SHAppBarMessage(Native.ABM_GETSTATE, ref data);
        if ((_prevState & Native.ABS_AUTOHIDE) != 0) return; // already auto-hiding
        data.lParam = Native.ABS_AUTOHIDE | (_prevState & Native.ABS_ALWAYSONTOP);
        Native.SHAppBarMessage(Native.ABM_SETSTATE, ref data);
        _autoHideApplied = true;
    }

    private void RestoreAutoHide()
    {
        if (!_autoHideApplied) return;
        var hWnd = Native.FindWindowW("Shell_TrayWnd", null);
        if (hWnd == nint.Zero) return;
        var data = new Native.APPBARDATA
        {
            cbSize = System.Runtime.InteropServices.Marshal.SizeOf<Native.APPBARDATA>(),
            hWnd = hWnd,
            lParam = _prevState,
        };
        Native.SHAppBarMessage(Native.ABM_SETSTATE, ref data);
        _autoHideApplied = false;
    }

    /// <summary>
    /// Fully replace the taskbar: auto-hide it (so it gives up its work-area strip), hide its windows,
    /// and re-hide them the instant Explorer shows them again (Start / Win+A / Win+N do that).
    /// </summary>
    public void Hide()
    {
        AutoHide();
        HideWindows();
        WatchForReshow();
    }

    // Kept in a field so the GC never collects the callback while the hook is live.
    private Native.WinEventProc? _onShow;
    private nint _showHook;

    private void WatchForReshow()
    {
        if (_showHook != nint.Zero || _hidden.Count == 0) return;
        Native.GetWindowThreadProcessId(_hidden[0], out var explorerPid);
        _onShow = (_, _, hWnd, idObject, _, _, _) =>
        {
            if (idObject == Native.OBJID_WINDOW && _hidden.Contains(hWnd))
                Native.ShowWindow(hWnd, Native.SW_HIDE);
        };
        _showHook = Native.SetWinEventHook(Native.EVENT_OBJECT_SHOW, Native.EVENT_OBJECT_SHOW, nint.Zero,
            _onShow, explorerPid, 0, Native.WINEVENT_OUTOFCONTEXT);
    }

    private void StopWatching()
    {
        if (_showHook == nint.Zero) return;
        Native.UnhookWinEvent(_showHook);
        _showHook = nint.Zero;
        _onShow = null;
    }

    private void HideWindows()
    {
        _hidden.Clear();

        // Primary taskbar + the Start button hosted on it.
        var primary = Native.FindWindowW("Shell_TrayWnd", null);
        if (primary != nint.Zero)
        {
            HideWindow(primary);
            var start = Native.FindWindowExW(primary, nint.Zero, "Start", null);
            if (start != nint.Zero) HideWindow(start);
        }

        // Secondary taskbars on additional monitors.
        nint secondary = nint.Zero;
        while ((secondary = Native.FindWindowExW(nint.Zero, secondary, "Shell_SecondaryTrayWnd", null)) != nint.Zero)
            HideWindow(secondary);

        _active = _hidden.Count > 0;
    }

    private void HideWindow(nint hWnd)
    {
        Native.ShowWindow(hWnd, Native.SW_HIDE);
        _hidden.Add(hWnd);
    }

    private void Show()
    {
        if (!_active) return;
        foreach (var hWnd in _hidden)
            if (Native.IsWindow(hWnd)) Native.ShowWindow(hWnd, Native.SW_SHOW);
        _hidden.Clear();
        _active = false;
    }

    public void Dispose()
    {
        StopWatching();
        Show();
        RestoreAutoHide();
    }
}
