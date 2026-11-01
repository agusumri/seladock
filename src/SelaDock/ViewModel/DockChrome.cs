using System.ComponentModel;
using SelaDock.Services;

namespace SelaDock.ViewModel;

/// <summary>
/// Window-level view model bound as the DockWindow DataContext. Exposes the resolved sizing
/// (icon / plate / button / glyph) and the live system-info service so the XAML can bind directly.
/// </summary>
public sealed class DockChrome : INotifyPropertyChanged
{
    public SystemInfoService Sys { get; }

    public DockChrome(SystemInfoService sys) => Sys = sys;

    private double _iconSize = 40;

    /// <summary>Rest icon size (logical px). Everything else is derived from it.</summary>
    public double IconSize
    {
        get => _iconSize;
        set
        {
            if (Math.Abs(_iconSize - value) < 0.1) return;
            _iconSize = value;
            foreach (var p in new[] { nameof(IconSize), nameof(PlateSize), nameof(ButtonSize), nameof(GlyphSize) })
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(p));
        }
    }

    /// <summary>Hover/active backplate size behind each app icon.</summary>
    public double PlateSize => Math.Round(IconSize * 1.22);

    /// <summary>System/Start/tray button size, matched to the plate for a consistent row height.</summary>
    public double ButtonSize => PlateSize;

    /// <summary>Segoe Fluent glyph size for the system buttons.</summary>
    public double GlyphSize => Math.Round(IconSize * 0.5);

    public event PropertyChangedEventHandler? PropertyChanged;
}
