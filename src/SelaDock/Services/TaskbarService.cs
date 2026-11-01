using SelaDock.Interop;

namespace SelaDock.Services;

/// <summary>
/// Hides the Windows taskbar (and the Start orb and any secondary-monitor taskbars) while SelaDock
/// runs, so the dock can act as a clean replacement, then shows them again on exit.
///
/// Auto-hide was tried first but is leaky: opening Start or Quick Settings forces the taskbar back
/// on screen, where it overlaps the dock. Hiding the taskbar windows outright avoids that entirely.
/// Nothing is removed permanently — every window is shown again in <see cref="Dispose"/>, and if the
/// process is killed, Explorer restores the taskbar on its next refresh (e.g. sign-out/in).
/// </summary>
public sealed class TaskbarService : IDisposable
{
    private readonly List<nint> _hidden = new();
    private bool _active;

    public void Hide()
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

    public void Dispose() => Show();
}
