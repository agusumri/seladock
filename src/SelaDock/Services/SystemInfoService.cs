using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows.Threading;
using SelaDock.Interop;

namespace SelaDock.Services;

/// <summary>
/// Reads the system status the dock surfaces on the right (network, volume, battery). It samples
/// lightly on a slow timer (a few seconds) rather than hooking every subsystem; the cost is a
/// handful of cheap Win32/COM calls, so idle impact stays negligible.
/// </summary>
public sealed class SystemInfoService : INotifyPropertyChanged, IDisposable
{
    private readonly DispatcherTimer _timer;

    public SystemInfoService()
    {
        _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(6) };
        _timer.Tick += (_, _) => Sample();
        Sample();
        _timer.Start();
    }

    // ---- Battery -------------------------------------------------------------------

    public bool HasBattery { get; private set; }
    public int BatteryPercent { get; private set; }
    public bool Charging { get; private set; }
    public string BatteryGlyph { get; private set; } = string.Empty;
    public string BatteryTip => !HasBattery ? "On power"
        : Charging ? $"Battery {BatteryPercent}% · charging"
        : $"Battery {BatteryPercent}%";

    // ---- Network -------------------------------------------------------------------

    public bool Online { get; private set; }
    public bool IsWifi { get; private set; }
    public string NetworkGlyph { get; private set; } = "\uE774"; // globe
    public string NetworkTip => !Online ? "No network"
        : IsWifi ? "Wi-Fi connected" : "Connected";

    // ---- Volume --------------------------------------------------------------------

    public int Volume { get; private set; }
    public bool Muted { get; private set; }
    public string VolumeGlyph { get; private set; } = "\uE767"; // speaker
    public string VolumeTip => Muted ? "Muted" : $"Volume {Volume}%";

    public event PropertyChangedEventHandler? PropertyChanged;

    private void Sample()
    {
        SampleBattery();
        SampleNetwork();
        SampleVolume();
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(string.Empty)); // refresh all bindings
    }

    private void SampleBattery()
    {
        if (!Native.GetSystemPowerStatus(out var s)) { HasBattery = false; return; }

        HasBattery = (s.BatteryFlag & 128) == 0; // bit 7 set == no system battery
        Charging = s.ACLineStatus == 1;
        BatteryPercent = s.BatteryLifePercent == 255 ? 0 : s.BatteryLifePercent;

        // Segoe MDL2 battery glyphs: charging set at 0xE83E.., discharging at 0xE850..
        int step = Math.Clamp(BatteryPercent / 10, 0, 10);
        int baseCode = Charging ? 0xE83E : 0xE850;
        BatteryGlyph = char.ConvertFromUtf32(baseCode + step);
    }

    private void SampleNetwork()
    {
        try
        {
            Online = System.Net.NetworkInformation.NetworkInterface.GetIsNetworkAvailable();
            IsWifi = false;
            if (Online)
            {
                foreach (var ni in System.Net.NetworkInformation.NetworkInterface.GetAllNetworkInterfaces())
                {
                    if (ni.OperationalStatus != System.Net.NetworkInformation.OperationalStatus.Up) continue;
                    if (ni.NetworkInterfaceType == System.Net.NetworkInformation.NetworkInterfaceType.Wireless80211)
                    {
                        IsWifi = true;
                        break;
                    }
                }
            }
            NetworkGlyph = !Online ? "\uEB55"      // disconnected
                : IsWifi ? "\uE701"                 // wifi
                : "\uE839";                          // ethernet
        }
        catch (System.Net.NetworkInformation.NetworkInformationException)
        {
            Online = false;
            NetworkGlyph = "\uEB55";
        }
    }

    private void SampleVolume()
    {
        try
        {
            var (vol, muted) = CoreAudio.Read();
            Volume = vol;
            Muted = muted;
            VolumeGlyph = muted || vol == 0 ? "\uE74F"  // muted
                : vol < 34 ? "\uE993"                    // low
                : vol < 67 ? "\uE994"                    // medium
                : "\uE995";                               // high
        }
        catch (Exception ex) when (ex is COMException or InvalidCastException)
        {
            VolumeGlyph = "\uE767";
        }
    }

    public void Dispose() => _timer.Stop();
}
