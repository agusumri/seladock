using System.Diagnostics;
using System.IO;
using System.Text;
using System.Windows.Threading;
using SelaDock.Interop;
using SelaDock.Model;


namespace SelaDock.Services;

/// <summary>
/// Enumerates the "alt-tab" style top-level windows and raises <see cref="Changed"/> when the set
/// of windows changes. Updates are driven by WinEvent hooks rather than a polling timer, so the
/// dock does no work while nothing on the desktop is opening, closing, or minimizing.
///
/// The event hooks are deliberately narrow. Hooking the whole OBJECT_CREATE..HIDE range would wake
/// the dock on virtually every UI redraw on the desktop; instead we hook only foreground changes,
/// minimize/restore, and SHOW/HIDE/DESTROY, and we reject non-window and child-object events in the
/// callback before doing anything. Refreshes are debounced so a burst collapses into one rebuild.
/// </summary>
public sealed class WindowService : IDisposable
{
    private readonly Native.WinEventProc _hookProc; // kept alive for the lifetime of the hook
    private readonly List<nint> _hooks = new();
    private readonly uint _ownProcessId;
    private readonly DispatcherTimer _debounce;

    /// <summary>Raised on the UI thread (debounced) when the visible window set may have changed.</summary>
    public event Action? Changed;

    public WindowService()
    {
        _ownProcessId = (uint)Environment.ProcessId;
        _hookProc = OnWinEvent;

        _debounce = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(120) };
        _debounce.Tick += (_, _) =>
        {
            _debounce.Stop();
            Changed?.Invoke();
        };

        // Foreground change and minimize/restore drive active/minimized state.
        Hook(Native.EVENT_SYSTEM_FOREGROUND, Native.EVENT_SYSTEM_FOREGROUND);
        Hook(Native.EVENT_SYSTEM_MINIMIZESTART, Native.EVENT_SYSTEM_MINIMIZEEND);
        // Only the three object events that change the dock's set: shown, hidden, destroyed.
        Hook(Native.EVENT_OBJECT_SHOW, Native.EVENT_OBJECT_SHOW);
        Hook(Native.EVENT_OBJECT_HIDE, Native.EVENT_OBJECT_HIDE);
        Hook(Native.EVENT_OBJECT_DESTROY, Native.EVENT_OBJECT_DESTROY);
    }

    private void Hook(uint min, uint max) =>
        _hooks.Add(Native.SetWinEventHook(min, max, nint.Zero, _hookProc, 0, 0,
            Native.WINEVENT_OUTOFCONTEXT | Native.WINEVENT_SKIPOWNPROCESS));

    private void OnWinEvent(nint hook, uint ev, nint hWnd, int idObject, int idChild,
        uint thread, uint time)
    {
        // Only top-level window objects matter. SHOW/HIDE/DESTROY fire for every child control and
        // menu too, so this check discards the overwhelming majority of events immediately.
        if (idObject != Native.OBJID_WINDOW || idChild != 0 || hWnd == nint.Zero) return;

        // Restart the debounce window; many events in a burst collapse to a single rebuild.
        _debounce.Stop();
        _debounce.Start();
    }

    /// <summary>Snapshot of the top-level windows that deserve a dock button, in a stable order.</summary>
    public IReadOnlyList<WindowInfo> Enumerate()
    {
        var result = new List<WindowInfo>();
        Native.EnumWindows((h, _) =>
        {
            if (TryDescribe(h, out var info)) result.Add(info);
            return true;
        }, nint.Zero);
        return result;
    }

    /// <summary>
    /// Apps running in the background with no visible window — the ones that normally live in the
    /// system tray (chat clients, sync tools, launchers). Windows 11 no longer exposes third-party
    /// tray icons as moveable windows, so the dock's "^" flyout lists these processes instead and
    /// lets the user bring each one to the foreground. This is a snapshot, computed only when the
    /// flyout opens, so it costs nothing while closed.
    /// </summary>
    public IReadOnlyList<WindowInfo> EnumerateBackgroundApps(IReadOnlyCollection<string>? excludePaths = null)
    {
        // Apps already represented on the dock (pinned or running) must not appear again in the
        // tray flyout. We exclude by executable path, since an app like Chrome can have both a
        // visible dock window and hidden background processes from the same exe.
        var excluded = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (excludePaths is not null)
            foreach (var p in excludePaths)
                if (!string.IsNullOrEmpty(p)) excluded.Add(p);

        // Build a PID -> window-state map from ALL top-level windows (including invisible ones).
        // A genuine tray app keeps a top-level window that is currently hidden; pure console/helper
        // processes (node, python, compilers) own no top-level window at all, so they fall out here.
        var hasVisible = new HashSet<uint>();
        var candidates = new HashSet<uint>();
        var target = new Dictionary<uint, nint>();

        Native.EnumWindows((h, _) =>
        {
            Native.GetWindowThreadProcessId(h, out var pid);
            if (pid == 0 || pid == _ownProcessId) return true;

            // Ignore tool windows (helper/overlay windows that aren't app windows).
            var ex = (long)Native.GetWindowLongPtr(h, Native.GWL_EXSTYLE);
            if ((ex & Native.WS_EX_TOOLWINDOW) != 0) return true;
            var owned = Native.GetWindow(h, Native.GW_OWNER) != nint.Zero;

            if (!owned)
            {
                if (!Native.IsWindowVisible(h)) candidates.Add(pid); // hidden top-level window -> tray candidate
                else if (Native.GetWindowTextLengthW(h) > 0) hasVisible.Add(pid); // already on the dock
            }

            // The window to show on click is the app's main window, which may be hidden or owned by
            // a hidden parent (WPF/WinForms tray apps like Aruba VIA do this). Helper windows
            // ("Hidden Window", 0x0 message sinks) are skipped: showing them does nothing visible.
            if (!target.ContainsKey(pid) && LooksLikeMainWindow(h)) target[pid] = h;
            return true;
        }, nint.Zero);

        var result = new List<WindowInfo>();
        foreach (var pid in candidates)
        {
            if (hasVisible.Contains(pid)) continue; // already represented on the dock
            var hwnd = target.GetValueOrDefault(pid);

            var path = ProcessPath(pid);
            if (string.IsNullOrEmpty(path) || !LooksLikeTrayApp(path)) continue;
            if (excluded.Contains(path)) continue; // already on the dock

            var name = Path.GetFileNameWithoutExtension(path);
            result.Add(new WindowInfo(hwnd, pid, name, path));
        }

        return result
            .GroupBy(w => w.ProcessPath, StringComparer.OrdinalIgnoreCase)
            .Select(g => g.First())
            .OrderBy(w => w.Title, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static readonly HashSet<string> HelperWindowTitles = new(StringComparer.Ordinal)
    {
        "Hidden Window", "MediaContextNotificationWindow", "SystemResourceNotifyWindow",
        "DDE Server Window", "OfficePowerManagerWindow", "JUCEWindow",
    };

    /// <summary>A hidden window that is a real app window: captioned, titled, sized, not a known helper.</summary>
    private static bool LooksLikeMainWindow(nint h)
    {
        const long WS_CAPTION = 0x00C00000;
        var style = (long)Native.GetWindowLongPtr(h, Native.GWL_STYLE);
        if ((style & WS_CAPTION) != WS_CAPTION) return false;
        if (!Native.GetWindowRect(h, out var r) || r.Right - r.Left < 200 || r.Bottom - r.Top < 120) return false;
        var len = Native.GetWindowTextLengthW(h);
        if (len == 0) return false;
        var sb = new StringBuilder(len + 1);
        Native.GetWindowTextW(h, sb, sb.Capacity);
        var title = sb.ToString();
        return !HelperWindowTitles.Contains(title) && !title.StartsWith(".NET-", StringComparison.Ordinal);
    }

    // Keep genuine user apps, drop OS background processes. Tray apps run from Program Files or the
    // user's AppData; System32 and the packaged SystemApps folder hold OS helpers, not tray apps.
    private static bool LooksLikeTrayApp(string path)
    {
        var sys = Environment.GetFolderPath(Environment.SpecialFolder.System);
        var winDir = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
        if (path.StartsWith(sys, StringComparison.OrdinalIgnoreCase)) return false;
        if (path.StartsWith(Path.Combine(winDir, "SystemApps"), StringComparison.OrdinalIgnoreCase)) return false;

        // The shell itself and common dev/runtime hosts are never tray apps, even when they keep a
        // hidden window; excluding them keeps the flyout to real background apps.
        var exe = Path.GetFileName(path);
        if (NotTrayApps.Contains(exe)) return false;
        return true;
    }

    private static readonly HashSet<string> NotTrayApps = new(StringComparer.OrdinalIgnoreCase)
    {
        "explorer.exe", "node.exe", "python.exe", "pythonw.exe", "dotnet.exe",
        "conhost.exe", "openconsole.exe", "cmd.exe", "powershell.exe", "pwsh.exe",
        "msedgewebview2.exe", "vbcscompiler.exe", "applicationframehost.exe",
    };

    private bool TryDescribe(nint h, out WindowInfo info)
    {
        info = null!;
        if (!Native.IsWindowVisible(h)) return false;

        var style = (long)Native.GetWindowLongPtr(h, Native.GWL_STYLE);
        if ((style & Native.WS_VISIBLE) == 0) return false;

        var ex = (long)Native.GetWindowLongPtr(h, Native.GWL_EXSTYLE);
        var appWindow = (ex & Native.WS_EX_APPWINDOW) != 0;
        // Tool windows and non-activating helpers are not real app windows (unless forced APPWINDOW).
        if (!appWindow && (ex & Native.WS_EX_TOOLWINDOW) != 0) return false;

        // Only true top-level windows (no owner) belong on the dock, matching alt-tab behaviour.
        if (!appWindow && Native.GetWindow(h, Native.GW_OWNER) != nint.Zero) return false;

        // Skip DWM-cloaked windows (suspended UWP apps, other virtual desktops).
        if (Native.DwmGetWindowAttribute(h, Native.DWMWA_CLOAKED, out var cloaked, sizeof(int)) == 0
            && cloaked != 0) return false;

        var len = Native.GetWindowTextLengthW(h);
        if (len == 0) return false;
        var sb = new StringBuilder(len + 1);
        Native.GetWindowTextW(h, sb, sb.Capacity);
        var title = sb.ToString();
        if (string.IsNullOrWhiteSpace(title)) return false;

        Native.GetWindowThreadProcessId(h, out var pid);
        if (pid == _ownProcessId) return false; // never show our own dock window

        info = new WindowInfo(h, pid, title, ProcessPath(pid)) { IsMinimized = Native.IsIconic(h) };
        return true;
    }

    // Process paths rarely change for a PID, and opening a process handle per window per refresh is
    // the most expensive part of enumeration. Cache by PID so a refresh is mostly cheap Win32 reads.
    private readonly Dictionary<uint, string> _pathCache = new();

    private string ProcessPath(uint pid)
    {
        if (_pathCache.TryGetValue(pid, out var cached)) return cached;
        string path;
        try
        {
            using var p = Process.GetProcessById((int)pid);
            path = p.MainModule?.FileName ?? string.Empty;
        }
        catch
        {
            // Access-denied for elevated/system processes is expected; path is best-effort.
            path = string.Empty;
        }
        _pathCache[pid] = path;
        return path;
    }

    /// <summary>Bring a window to the foreground, restoring it first if it was minimized.</summary>
    public static void Activate(nint hWnd)
    {
        if (!Native.IsWindow(hWnd)) return;
        if (Native.IsIconic(hWnd)) Native.ShowWindow(hWnd, Native.SW_RESTORE);

        // Windows blocks a background process from stealing focus; briefly attach to the
        // foreground window's thread so SetForegroundWindow is honoured, the standard workaround.
        var fg = Native.GetForegroundWindow();
        var fgThread = Native.GetWindowThreadProcessId(fg, out _);
        var self = Native.GetCurrentThreadId();
        var attached = fgThread != self && Native.AttachThreadInput(self, fgThread, true);
        try
        {
            Native.SetForegroundWindow(hWnd);
        }
        finally
        {
            if (attached) Native.AttachThreadInput(self, fgThread, false);
        }
    }

    /// <summary>Show a window the app hid to the tray (SW_HIDE), then bring it to the front.</summary>
    public static void ShowHidden(nint hWnd)
    {
        if (!Native.IsWindow(hWnd)) return;
        if (!Native.IsWindowVisible(hWnd)) Native.ShowWindow(hWnd, Native.SW_SHOW);
        Activate(hWnd);
    }

    /// <summary>The visible main window of any running instance of <paramref name="exePath"/>, if one exists.</summary>
    public nint FindVisibleMainWindow(string exePath)
    {
        nint found = nint.Zero;
        Native.EnumWindows((h, _) =>
        {
            if (!Native.IsWindowVisible(h) || !LooksLikeMainWindow(h)) return true;
            Native.GetWindowThreadProcessId(h, out var pid);
            if (pid == 0 || pid == _ownProcessId) return true;
            if (!string.Equals(ProcessPath(pid), exePath, StringComparison.OrdinalIgnoreCase)) return true;
            found = h;
            return false;
        }, nint.Zero);
        return found;
    }

    /// <summary>Toggle: minimize the window if it is already the foreground window, else activate it.</summary>
    public static void ActivateOrMinimize(nint hWnd)
    {
        if (!Native.IsWindow(hWnd)) return;
        if (Native.GetForegroundWindow() == hWnd && !Native.IsIconic(hWnd))
            Native.ShowWindow(hWnd, Native.SW_MINIMIZE);
        else
            Activate(hWnd);
    }

    public void Dispose()
    {
        _debounce.Stop();
        foreach (var h in _hooks)
            if (h != nint.Zero) Native.UnhookWinEvent(h);
        _hooks.Clear();
    }
}
