using System.Collections.ObjectModel;
using System.IO;
using SelaDock.Interop;
using SelaDock.Services;

namespace SelaDock.ViewModel;

/// <summary>
/// Builds the dock's item list by merging the user's pinned apps with the currently running
/// windows. Running windows that share an executable with a pinned app light up that pin instead
/// of adding a duplicate button, the way a Mac dock or the Windows taskbar groups them.
/// </summary>
public sealed class DockViewModel
{
    private readonly WindowService _windows;
    private readonly IconService _icons;
    private readonly Settings _settings;

    public ObservableCollection<DockItem> Items { get; } = new();

    public DockViewModel(WindowService windows, IconService icons, Settings settings)
    {
        _windows = windows;
        _icons = icons;
        _settings = settings;
        Rebuild();
        _windows.Changed += Rebuild;
    }

    /// <summary>Recompute the item list from scratch, reusing existing DockItem instances by key so
    /// WPF keeps element state (and running animations) for buttons that stay put.</summary>
    public void Rebuild()
    {
        var running = _windows.Enumerate();
        var fg = Native.GetForegroundWindow();

        // Group running windows by executable so multiple windows of one app share a button.
        var byExe = running
            .GroupBy(w => Normalize(w.ProcessPath), StringComparer.OrdinalIgnoreCase);

        var desired = new List<DockItem>();
        var usedExes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // 1) Pinned apps first, in the user's saved order.
        foreach (var path in _settings.PinnedApps)
        {
            var key = "pin:" + Normalize(path);
            var item = Take(key) ?? new DockItem { Key = key, ProcessPath = path, IsPinned = true };
            item.IsPinned = true;
            item.Title = Path.GetFileNameWithoutExtension(path);
            item.Icon ??= _icons.ForPath(path);

            item.Windows.Clear();
            var group = running.Where(w => PathsMatch(w.ProcessPath, path)).ToList();
            item.IsRunning = group.Count > 0;
            item.IsActive = group.Any(w => w.Handle == fg);
            foreach (var w in group)
            {
                item.Windows.Add(w.Handle);
                usedExes.Add(Normalize(w.ProcessPath));
            }
            if (group.Count > 0)
            {
                item.Title = group[0].Title;
                item.Icon = _icons.ForWindow(group[0].Handle, group[0].ProcessPath) ?? item.Icon;
            }
            desired.Add(item);
        }

        // 2) Running apps that aren't pinned, after a separator (handled in the view).
        foreach (var grp in byExe)
        {
            if (string.IsNullOrEmpty(grp.Key) || usedExes.Contains(grp.Key)) continue;
            var first = grp.First();
            var key = "win:" + grp.Key;
            var item = Take(key) ?? new DockItem { Key = key, ProcessPath = first.ProcessPath };
            item.IsPinned = false;
            item.IsRunning = true;
            item.Title = first.Title;
            item.Icon = _icons.ForWindow(first.Handle, first.ProcessPath) ?? item.Icon;
            item.IsActive = grp.Any(w => w.Handle == fg);
            item.Windows.Clear();
            item.Windows.AddRange(grp.Select(w => w.Handle));
            desired.Add(item);
        }

        // Apply the diff in place so the ObservableCollection emits minimal changes.
        Sync(desired);
    }

    private readonly Dictionary<string, DockItem> _pool = new(StringComparer.Ordinal);

    private DockItem? Take(string key) => _pool.TryGetValue(key, out var i) ? i : null;

    private void Sync(List<DockItem> desired)
    {
        _pool.Clear();
        foreach (var d in desired) _pool[d.Key] = d;

        // Remove items no longer desired.
        for (int i = Items.Count - 1; i >= 0; i--)
            if (!_pool.ContainsKey(Items[i].Key))
                Items.RemoveAt(i);

        // Insert / reorder to match desired order.
        for (int i = 0; i < desired.Count; i++)
        {
            var d = desired[i];
            int existing = IndexOfKey(d.Key);
            if (existing < 0) Items.Insert(i, d);
            else if (existing != i) Items.Move(existing, i);
        }
    }

    private int IndexOfKey(string key)
    {
        for (int i = 0; i < Items.Count; i++)
            if (Items[i].Key == key) return i;
        return -1;
    }

    /// <summary>Click behaviour: launch if not running, otherwise activate/minimize the window.</summary>
    public void Invoke(DockItem item)
    {
        if (item.Windows.Count == 0)
        {
            Launch(item.ProcessPath);
            return;
        }
        // Single window: toggle. Multiple: cycle to the next non-foreground window.
        if (item.Windows.Count == 1)
        {
            WindowService.ActivateOrMinimize(item.Windows[0]);
            return;
        }
        var fg = Native.GetForegroundWindow();
        var idx = item.Windows.IndexOf(fg);
        var next = item.Windows[(idx + 1) % item.Windows.Count];
        WindowService.Activate(next);
    }

    public void Pin(DockItem item)
    {
        if (string.IsNullOrEmpty(item.ProcessPath)) return;
        var norm = Normalize(item.ProcessPath);
        if (_settings.PinnedApps.Any(p => Normalize(p) == norm)) return;
        _settings.PinnedApps.Add(item.ProcessPath);
        _settings.Save();
        Rebuild();
    }

    public void Unpin(DockItem item)
    {
        var norm = Normalize(item.ProcessPath ?? string.Empty);
        _settings.PinnedApps.RemoveAll(p => Normalize(p) == norm);
        _settings.Save();
        Rebuild();
    }

    /// <summary>Pin an executable (or a resolved shortcut) dropped onto the dock, at an optional index.</summary>
    public void PinPath(string path, int atIndex = -1)
    {
        path = ResolveToExe(path);
        if (string.IsNullOrEmpty(path)) return;
        var norm = Normalize(path);
        if (_settings.PinnedApps.Any(p => Normalize(p) == norm)) return;

        if (atIndex < 0 || atIndex > _settings.PinnedApps.Count)
            _settings.PinnedApps.Add(path);
        else
            _settings.PinnedApps.Insert(atIndex, path);
        _settings.Save();
        Rebuild();
    }

    /// <summary>Move a pinned app to a new slot among the pins (drag-to-reorder).</summary>
    public void MovePin(DockItem item, int toIndex)
    {
        if (!item.IsPinned || string.IsNullOrEmpty(item.ProcessPath)) return;
        var norm = Normalize(item.ProcessPath);
        var from = _settings.PinnedApps.FindIndex(p => Normalize(p) == norm);
        if (from < 0) return;

        toIndex = Math.Clamp(toIndex, 0, _settings.PinnedApps.Count - 1);
        if (toIndex == from) return;

        var path = _settings.PinnedApps[from];
        _settings.PinnedApps.RemoveAt(from);
        if (toIndex > from) toIndex--; // account for the removal shifting indices
        _settings.PinnedApps.Insert(toIndex, path);
        _settings.Save();
        Rebuild();
    }

    /// <summary>Index of a pinned item within the pinned list (for computing drop targets).</summary>
    public int PinnedIndexOf(DockItem item)
    {
        if (!item.IsPinned || string.IsNullOrEmpty(item.ProcessPath)) return -1;
        var norm = Normalize(item.ProcessPath);
        return _settings.PinnedApps.FindIndex(p => Normalize(p) == norm);
    }

    public int PinnedCount => _settings.PinnedApps.Count;

    /// <summary>Executable paths currently represented on the dock (pinned + running), so the tray
    /// flyout can exclude them and avoid showing the same app twice.</summary>
    public IReadOnlyCollection<string> DockExecutablePaths()
    {
        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var p in _settings.PinnedApps)
            if (!string.IsNullOrEmpty(p)) set.Add(p);
        foreach (var item in Items)
            if (!string.IsNullOrEmpty(item.ProcessPath)) set.Add(item.ProcessPath!);
        return set;
    }

    /// <summary>If the dropped file is a .lnk shortcut, resolve it to its target exe; else return as-is.</summary>
    private static string ResolveToExe(string path)
    {
        if (string.IsNullOrEmpty(path)) return string.Empty;
        if (path.EndsWith(".lnk", StringComparison.OrdinalIgnoreCase))
            return ShortcutResolver.ResolveTarget(path);
        return path;
    }

    private static void Launch(string? path)
    {
        if (string.IsNullOrEmpty(path)) return;
        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(path) { UseShellExecute = true });
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or FileNotFoundException or InvalidOperationException)
        {
            // Missing or blocked executable: nothing to launch.
        }
    }

    private static bool PathsMatch(string a, string b) =>
        !string.IsNullOrEmpty(a) && Normalize(a) == Normalize(b);

    private static string Normalize(string path) =>
        string.IsNullOrEmpty(path) ? string.Empty : path.Trim().ToLowerInvariant();
}
