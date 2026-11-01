using System.IO;

namespace SelaDock.Services;

/// <summary>
/// Reads the apps the user has pinned to the Windows taskbar. Windows stores these as .lnk
/// shortcuts in a well-known Quick Launch folder; each is resolved to its target executable so the
/// dock can show and launch the same apps. Used once, to seed the dock on first run.
/// </summary>
public static class TaskbarPins
{
    private static string PinnedDir => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        @"Microsoft\Internet Explorer\Quick Launch\User Pinned\TaskBar");

    /// <summary>Target executables of the user's current taskbar pins, in folder order.</summary>
    public static List<string> Read()
    {
        var result = new List<string>();
        try
        {
            if (!Directory.Exists(PinnedDir)) return result;
            var explorerExe = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.Windows), "explorer.exe");

            foreach (var lnk in Directory.EnumerateFiles(PinnedDir, "*.lnk"))
            {
                var target = ShortcutResolver.ResolveTarget(lnk);

                // The File Explorer pin is a special shell shortcut with no .exe target; map it to
                // explorer.exe so it still appears on the dock.
                if (string.IsNullOrEmpty(target)
                    && Path.GetFileNameWithoutExtension(lnk).Contains("explorer", StringComparison.OrdinalIgnoreCase))
                {
                    target = explorerExe;
                }

                if (!string.IsNullOrEmpty(target)
                    && target.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)
                    && File.Exists(target)
                    && !result.Any(p => string.Equals(p, target, StringComparison.OrdinalIgnoreCase)))
                {
                    result.Add(target);
                }
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Best-effort: if the folder can't be read, start with an empty dock.
        }
        return result;
    }
}
