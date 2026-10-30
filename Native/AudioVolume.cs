using System.Runtime.InteropServices;

namespace WinToMac.Native;

/// <summary>Громкость устройства вывода по умолчанию (Core Audio).</summary>
public static class AudioVolume
{
    [ComImport]
    [Guid("BCDE0395-E52F-467C-8E3D-C4579291692E")]
    private class MMDeviceEnumeratorComObject
    {
    }

    [ComImport]
    [Guid("A95664D2-9614-4F35-A746-DE8DB63617E6")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IMMDeviceEnumerator
    {
        [PreserveSig]
        int EnumAudioEndpoints(int dataFlow, int stateMask, out IntPtr devices);

        [PreserveSig]
        int GetDefaultAudioEndpoint(int dataFlow, int role, out IMMDevice device);
    }

    [ComImport]
    [Guid("D666063F-1587-4E43-81F1-B948E807363F")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IMMDevice
    {
        [PreserveSig]
        int Activate(
            [MarshalAs(UnmanagedType.LPStruct)] Guid iid,
            int clsCtx,
            IntPtr activationParams,
            [MarshalAs(UnmanagedType.IUnknown)] out object endpoint);
    }

    [ComImport]
    [Guid("5CDF2C82-841E-4546-9722-0CF74078229A")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IAudioEndpointVolume
    {
        [PreserveSig] int RegisterControlChangeNotify(IntPtr notify);
        [PreserveSig] int UnregisterControlChangeNotify(IntPtr notify);
        [PreserveSig] int GetChannelCount(out uint count);
        [PreserveSig] int SetMasterVolumeLevel(float level, Guid eventContext);
        [PreserveSig] int SetMasterVolumeLevelScalar(float level, [MarshalAs(UnmanagedType.LPStruct)] Guid eventContext);
        [PreserveSig] int GetMasterVolumeLevel(out float level);
        [PreserveSig] int GetMasterVolumeLevelScalar(out float level);
        [PreserveSig] int SetChannelVolumeLevel(uint channel, float level, Guid eventContext);
        [PreserveSig] int SetChannelVolumeLevelScalar(uint channel, float level, Guid eventContext);
        [PreserveSig] int GetChannelVolumeLevel(uint channel, out float level);
        [PreserveSig] int GetChannelVolumeLevelScalar(uint channel, out float level);
        [PreserveSig] int SetMute([MarshalAs(UnmanagedType.Bool)] bool mute, [MarshalAs(UnmanagedType.LPStruct)] Guid eventContext);
        [PreserveSig] int GetMute([MarshalAs(UnmanagedType.Bool)] out bool mute);
    }

    private const int CLSCTX_ALL = 23;

    /// <summary>Устройство каждый раз берём заново — пользователь мог переключить наушники/колонки.</summary>
    private static IAudioEndpointVolume? GetEndpoint()
    {
        try
        {
            var enumerator = (IMMDeviceEnumerator)new MMDeviceEnumeratorComObject();
            if (enumerator.GetDefaultAudioEndpoint(0, 1, out var device) != 0)
            {
                return null;
            }

            device.Activate(typeof(IAudioEndpointVolume).GUID, CLSCTX_ALL, IntPtr.Zero, out object endpoint);
            return endpoint as IAudioEndpointVolume;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>Громкость 0..1 и признак выключенного звука; null — устройства нет.</summary>
    public static (float Level, bool Muted)? Get()
    {
        var endpoint = GetEndpoint();
        if (endpoint == null)
        {
            return null;
        }

        endpoint.GetMasterVolumeLevelScalar(out float level);
        endpoint.GetMute(out bool muted);
        return (level, muted);
    }

    public static void SetLevel(float level)
    {
        GetEndpoint()?.SetMasterVolumeLevelScalar(Math.Clamp(level, 0f, 1f), Guid.Empty);
    }

    public static void SetMute(bool mute)
    {
        GetEndpoint()?.SetMute(mute, Guid.Empty);
    }
}
