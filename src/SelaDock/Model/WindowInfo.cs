namespace SelaDock.Model;

/// <summary>A top-level application window that belongs on the dock.</summary>
public sealed record WindowInfo(nint Handle, uint ProcessId, string Title, string ProcessPath)
{
    public bool IsMinimized { get; init; }
}
