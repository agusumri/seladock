using System.Threading;

namespace SelaDock.Services;

/// <summary>A named mutex so only one dock runs at a time.</summary>
public static class SingleInstance
{
    private static Mutex? _mutex;

    public static bool Acquire()
    {
        _mutex = new Mutex(initiallyOwned: true, @"Local\SelaDock.SingleInstance", out var created);
        return created;
    }

    public static void Release()
    {
        try
        {
            _mutex?.ReleaseMutex();
        }
        catch (ApplicationException)
        {
            // Not the owning thread / already released: nothing to do.
        }
        _mutex?.Dispose();
        _mutex = null;
    }
}
