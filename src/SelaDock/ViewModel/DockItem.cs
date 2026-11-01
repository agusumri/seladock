using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Media;

namespace SelaDock.ViewModel;

/// <summary>One button on the dock: either a pinned app, a running window, or a pinned app that is running.</summary>
public sealed class DockItem : INotifyPropertyChanged
{
    public required string Key { get; init; }          // identity for diffing (path or window handle)
    public string? ProcessPath { get; init; }           // for launching when not running
    public string Title { get; set; } = string.Empty;   // tooltip

    private ImageSource? _icon;
    public ImageSource? Icon
    {
        get => _icon;
        set => Set(ref _icon, value);
    }

    private bool _isPinned;
    public bool IsPinned
    {
        get => _isPinned;
        set => Set(ref _isPinned, value);
    }

    private bool _isRunning;
    public bool IsRunning
    {
        get => _isRunning;
        set => Set(ref _isRunning, value);
    }

    private bool _isActive;
    public bool IsActive
    {
        get => _isActive;
        set => Set(ref _isActive, value);
    }

    /// <summary>Window handles represented by this item (one for a window, several if windows are grouped).</summary>
    public List<nint> Windows { get; } = new();

    public event PropertyChangedEventHandler? PropertyChanged;

    private void Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return;
        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
