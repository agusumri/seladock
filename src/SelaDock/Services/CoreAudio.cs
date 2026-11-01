using System.Runtime.InteropServices;

namespace SelaDock.Services;

/// <summary>
/// Minimal Core Audio (MMDevice + IAudioEndpointVolume) wrapper to read the default playback
/// device's master volume and mute state. Only the vtable slots we use are declared.
/// </summary>
internal static class CoreAudio
{
    // Cache the endpoint across samples: creating the Core Audio COM graph every few seconds is
    // comparatively expensive. The endpoint is only rebuilt if a read fails (e.g. device changed).
    private static IAudioEndpointVolume? _endpoint;

    public static (int Volume, bool Muted) Read()
    {
        try
        {
            var ep = _endpoint ??= Acquire();
            if (ep is null) return (0, false);
            ep.GetMasterVolumeLevelScalar(out float scalar);
            ep.GetMute(out bool muted);
            return ((int)Math.Round(scalar * 100), muted);
        }
        catch (COMException)
        {
            // Default device likely changed; drop the cached endpoint and let the next read rebuild.
            Reset();
            return (0, false);
        }
    }

    private static IAudioEndpointVolume? Acquire()
    {
        IMMDeviceEnumerator? enumerator = null;
        IMMDevice? device = null;
        try
        {
            enumerator = (IMMDeviceEnumerator)new MMDeviceEnumerator();
            if (enumerator.GetDefaultAudioEndpoint(0, 0, out device) != 0 || device is null)
                return null;

            var iid = typeof(IAudioEndpointVolume).GUID;
            if (device.Activate(ref iid, 23 /*CLSCTX_ALL*/, nint.Zero, out var epObj) != 0 || epObj is null)
                return null;
            return (IAudioEndpointVolume)epObj;
        }
        finally
        {
            if (device is not null) Marshal.ReleaseComObject(device);
            if (enumerator is not null) Marshal.ReleaseComObject(enumerator);
        }
    }

    private static void Reset()
    {
        if (_endpoint is not null) Marshal.ReleaseComObject(_endpoint);
        _endpoint = null;
    }

    [ComImport, Guid("BCDE0395-E52F-467C-8E3D-C4579291692E")]
    private class MMDeviceEnumerator { }

    [ComImport, Guid("A95664D2-9614-4F35-A746-DE8DB63617E6"),
     InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IMMDeviceEnumerator
    {
        [PreserveSig] int NotImpl1();
        [PreserveSig] int GetDefaultAudioEndpoint(int dataFlow, int role, out IMMDevice device);
    }

    [ComImport, Guid("D666063F-1587-4E43-81F1-B948E807363F"),
     InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IMMDevice
    {
        [PreserveSig] int Activate(ref Guid iid, int clsCtx, nint activationParams,
            [MarshalAs(UnmanagedType.IUnknown)] out object iface);
    }

    [ComImport, Guid("5CDF2C82-841E-4546-9722-0CF74078229A"),
     InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IAudioEndpointVolume
    {
        [PreserveSig] int RegisterControlChangeNotify(nint notify);
        [PreserveSig] int UnregisterControlChangeNotify(nint notify);
        [PreserveSig] int GetChannelCount(out int count);
        [PreserveSig] int SetMasterVolumeLevel(float level, ref Guid ctx);
        [PreserveSig] int SetMasterVolumeLevelScalar(float level, ref Guid ctx);
        [PreserveSig] int GetMasterVolumeLevel(out float level);
        [PreserveSig] int GetMasterVolumeLevelScalar(out float level);
        [PreserveSig] int SetChannelVolumeLevel(uint channel, float level, ref Guid ctx);
        [PreserveSig] int SetChannelVolumeLevelScalar(uint channel, float level, ref Guid ctx);
        [PreserveSig] int GetChannelVolumeLevel(uint channel, out float level);
        [PreserveSig] int GetChannelVolumeLevelScalar(uint channel, out float level);
        [PreserveSig] int SetMute([MarshalAs(UnmanagedType.Bool)] bool mute, ref Guid ctx);
        [PreserveSig] int GetMute([MarshalAs(UnmanagedType.Bool)] out bool mute);
    }
}
