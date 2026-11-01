using System.Runtime.InteropServices;

namespace SelaDock.Services;

/// <summary>
/// Opens the genuine Windows UI from the dock: Start, the network/sound/battery flyouts, the
/// calendar, and the hidden-tray overflow. These use the same keyboard shortcuts and shell
/// commands Windows exposes, so nothing is faked and the real system UI always appears.
/// </summary>
public static class SystemActions
{
    // ---- Keyboard synthesis for the built-in flyout shortcuts -----------------------

    [DllImport("user32.dll")]
    private static extern void keybd_event(byte vk, byte scan, uint flags, nuint extra);

    private const byte VK_LWIN = 0x5B;
    private const byte VK_A = 0x41;  // Win+A  Quick Settings (wifi / volume / battery)
    private const byte VK_N = 0x4E;  // Win+N  Notifications + calendar
    private const uint KEYUP = 0x0002;

    private static void WinChord(byte key)
    {
        keybd_event(VK_LWIN, 0, 0, 0);
        keybd_event(key, 0, 0, 0);
        keybd_event(key, 0, KEYUP, 0);
        keybd_event(VK_LWIN, 0, KEYUP, 0);
    }

    /// <summary>Quick Settings panel: Wi-Fi, volume slider, battery, brightness (Win+A).</summary>
    public static void OpenQuickSettings() => WinChord(VK_A);

    /// <summary>Notification centre with the full calendar (Win+N).</summary>
    public static void OpenCalendar() => WinChord(VK_N);

    /// <summary>Open the Start menu by tapping the Windows key.</summary>
    public static void OpenStart()
    {
        keybd_event(VK_LWIN, 0, 0, 0);
        keybd_event(VK_LWIN, 0, KEYUP, 0);
    }

    // ---- Shell-command flyouts ------------------------------------------------------

    private static void Shell(string target)
    {
        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(target)
            {
                UseShellExecute = true,
            });
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            // Target unavailable on this edition of Windows: ignore.
        }
    }

    public static void OpenNetworkSettings() => Shell("ms-availablenetworks:");
    public static void OpenSoundSettings() => Shell("ms-settings:sound");
    public static void OpenBatterySettings() => Shell("ms-settings:batterysaver");
}
