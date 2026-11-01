using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace SelaDock.Services;

/// <summary>Dock icon sizing mode.</summary>
public enum DockSize
{
    Auto,
    Small,
    Medium,
    Large,
}

/// <summary>User settings, persisted as a small JSON file under %APPDATA%\SelaDock.</summary>
public sealed class Settings
{
    /// <summary>Executable paths the user pinned to the dock, in display order.</summary>
    public List<string> PinnedApps { get; set; } = new();

    /// <summary>True once the dock has seeded its pins from the Windows taskbar (first-run only).</summary>
    public bool ImportedTaskbarPins { get; set; }

    /// <summary>Hide the dock off-screen until the pointer reaches the bottom edge.</summary>
    public bool AutoHide { get; set; }

    /// <summary>Reserve screen space so maximized windows don't cover the dock.</summary>
    public bool ReserveSpace { get; set; } = true;

    /// <summary>Hide the Windows taskbar (auto-hide) while the dock runs; restored on exit.</summary>
    public bool HideTaskbar { get; set; } = true;

    /// <summary>How the dock icon size is chosen.</summary>
    public DockSize Size { get; set; } = DockSize.Auto;

    /// <summary>Resolved icon size (logical px) for the current <see cref="Size"/> and screen width.</summary>
    public double ResolveIconSize(double screenWidth) => Size switch
    {
        DockSize.Small => 30,
        DockSize.Medium => 38,
        DockSize.Large => 48,
        // Auto: scale gently with screen width so the dock feels right from small laptops to 4K.
        // The smaller floor keeps the whole pill from getting too wide on low-res screens.
        _ => Math.Clamp(Math.Round(screenWidth / 40.0), 30, 52),
    };

    // ---- Persistence ---------------------------------------------------------------

    private static readonly string Dir =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "SelaDock");

    private static readonly string FilePath = Path.Combine(Dir, "settings.json");

    private static readonly JsonSerializerOptions Json = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter() },
    };

    public static Settings Load()
    {
        try
        {
            if (File.Exists(FilePath))
                return JsonSerializer.Deserialize<Settings>(File.ReadAllText(FilePath), Json) ?? new Settings();
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
        {
            // Corrupt or unreadable settings fall back to defaults rather than blocking startup.
        }
        return new Settings();
    }

    public void Save()
    {
        try
        {
            Directory.CreateDirectory(Dir);
            File.WriteAllText(FilePath, JsonSerializer.Serialize(this, Json));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Best-effort: a failed save shouldn't crash the dock.
        }
    }
}
