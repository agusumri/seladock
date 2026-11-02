using System.Windows;

namespace SelaDock.Services;

/// <summary>
/// Reserves screen space at the bottom (like the Windows taskbar) via a hidden app-bar window, so
/// maximized windows stop above the dock. Reservation is best-effort: if the shell rejects it (for
/// example while the taskbar is hidden, which makes Windows treat the whole screen as the work
/// area), the dock simply floats over the bottom edge instead. Failures never crash the dock.
/// </summary>
public sealed class AppBarService : IDisposable
{
    private readonly Window _owner;
    private AppBarWindow? _bar;

    public AppBarService(Window owner) => _owner = owner;

    /// <summary>
    /// Reserve the strip and snap the owner window onto the exact device-pixel rectangle the shell
    /// granted. Positioning in logical units can round half a pixel off at 125%/150% scaling, which
    /// leaves a 1px row of the maximized window showing above the dock.
    /// </summary>
    public void Reserve(int heightLogical)
    {
        try
        {
            _bar ??= new AppBarWindow();
            _bar.Show();
            var rc = _bar.Reserve(heightLogical);
            var hWnd = new System.Windows.Interop.WindowInteropHelper(_owner).Handle;
            if (hWnd != nint.Zero && rc.Bottom > rc.Top)
                Interop.Native.SetWindowPos(hWnd, nint.Zero, rc.Left, rc.Top, rc.Right - rc.Left,
                    rc.Bottom - rc.Top, Interop.Native.SWP_NOZORDER | Interop.Native.SWP_NOACTIVATE);
        }
        catch (Exception ex) when (ex is EntryPointNotFoundException or InvalidOperationException)
        {
            // Shell rejected the reservation or the helper window couldn't register; fall back to
            // a floating dock rather than crashing.
            Remove();
        }
    }

    public void Remove()
    {
        if (_bar is null) return;
        try
        {
            _bar.Unregister();
            _bar.Close();
        }
        catch (InvalidOperationException) { /* already closing */ }
        _bar = null;
    }

    public void Dispose() => Remove();
}
