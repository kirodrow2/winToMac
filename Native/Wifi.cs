using System.Runtime.InteropServices;
using System.Text;

namespace WinToMac.Native;

public sealed class WifiNetwork
{
    public string Ssid { get; init; } = "";
    public string? ProfileName { get; init; }
    public int Signal { get; init; }
    public bool IsSecured { get; init; }
    public bool IsConnected { get; init; }
    public Guid InterfaceId { get; init; }
}

/// <summary>Список Wi‑Fi сетей и подключение к сохранённым через Native Wifi API (wlanapi.dll).</summary>
public static class Wifi
{
    private const int InterfaceInfoSize = 532;
    private const int AvailableNetworkSize = 628;
    private const uint FlagConnected = 1;

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct WLAN_CONNECTION_PARAMETERS
    {
        public int wlanConnectionMode;
        public string strProfile;
        public IntPtr pDot11Ssid;
        public IntPtr pDesiredBssidList;
        public int dot11BssType;
        public uint dwFlags;
    }

    [DllImport("wlanapi.dll")]
    private static extern int WlanOpenHandle(uint clientVersion, IntPtr reserved, out uint negotiatedVersion, out IntPtr handle);

    [DllImport("wlanapi.dll")]
    private static extern int WlanCloseHandle(IntPtr handle, IntPtr reserved);

    [DllImport("wlanapi.dll")]
    private static extern int WlanEnumInterfaces(IntPtr handle, IntPtr reserved, out IntPtr interfaceList);

    [DllImport("wlanapi.dll")]
    private static extern int WlanGetAvailableNetworkList(IntPtr handle, ref Guid interfaceGuid, uint flags, IntPtr reserved, out IntPtr networkList);

    [DllImport("wlanapi.dll")]
    private static extern int WlanScan(IntPtr handle, ref Guid interfaceGuid, IntPtr ssid, IntPtr ieData, IntPtr reserved);

    [DllImport("wlanapi.dll")]
    private static extern int WlanConnect(IntPtr handle, ref Guid interfaceGuid, ref WLAN_CONNECTION_PARAMETERS parameters, IntPtr reserved);

    [DllImport("wlanapi.dll")]
    private static extern int WlanDisconnect(IntPtr handle, ref Guid interfaceGuid, IntPtr reserved);

    [DllImport("wlanapi.dll")]
    private static extern void WlanFreeMemory(IntPtr memory);

    /// <summary>Есть ли в системе Wi‑Fi адаптер.</summary>
    public static bool IsAvailable => Use(handle => GetInterfaces(handle).Count > 0, false);

    /// <summary>Сети, видимые адаптером: подключённая первой, дальше по уровню сигнала.</summary>
    public static List<WifiNetwork> GetNetworks() => Use(handle =>
    {
        var result = new List<WifiNetwork>();
        foreach (var interfaceId in GetInterfaces(handle))
        {
            var id = interfaceId;
            if (WlanGetAvailableNetworkList(handle, ref id, 0, IntPtr.Zero, out IntPtr list) != 0)
            {
                continue;
            }

            try
            {
                int count = Marshal.ReadInt32(list);
                for (int index = 0; index < count; index++)
                {
                    IntPtr item = list + 8 + index * AvailableNetworkSize;
                    int ssidLength = Math.Min(32, Marshal.ReadInt32(item + 512));
                    var ssidBytes = new byte[ssidLength];
                    Marshal.Copy(item + 516, ssidBytes, 0, ssidLength);
                    string ssid = Encoding.UTF8.GetString(ssidBytes);
                    if (string.IsNullOrWhiteSpace(ssid))
                    {
                        continue;
                    }

                    string profile = Marshal.PtrToStringUni(item, 256).TrimEnd('\0');
                    uint flags = (uint)Marshal.ReadInt32(item + 620);
                    result.Add(new WifiNetwork
                    {
                        Ssid = ssid,
                        ProfileName = profile.Length > 0 ? profile : null,
                        Signal = Marshal.ReadInt32(item + 604),
                        IsSecured = Marshal.ReadInt32(item + 608) != 0,
                        IsConnected = (flags & FlagConnected) != 0,
                        InterfaceId = interfaceId,
                    });
                }
            }
            finally
            {
                WlanFreeMemory(list);
            }
        }

        // Одна сеть приходит дважды: с сохранённым профилем и без — оставляем лучшую запись
        return result
            .GroupBy(network => network.Ssid)
            .Select(group => group
                .OrderByDescending(network => network.IsConnected)
                .ThenByDescending(network => network.ProfileName != null)
                .First())
            .OrderByDescending(network => network.IsConnected)
            .ThenByDescending(network => network.Signal)
            .ToList();
    }, new List<WifiNetwork>());

    /// <summary>Попросить адаптер обновить список сетей (результат появится через пару секунд).</summary>
    public static void Scan() => Use(handle =>
    {
        foreach (var interfaceId in GetInterfaces(handle))
        {
            var id = interfaceId;
            WlanScan(handle, ref id, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero);
        }

        return true;
    }, false);

    /// <summary>Подключиться к сети с сохранённым профилем.</summary>
    public static bool Connect(WifiNetwork network) => network.ProfileName != null && Use(handle =>
    {
        var id = network.InterfaceId;
        var parameters = new WLAN_CONNECTION_PARAMETERS
        {
            wlanConnectionMode = 0,
            strProfile = network.ProfileName,
            dot11BssType = 1,
        };
        return WlanConnect(handle, ref id, ref parameters, IntPtr.Zero) == 0;
    }, false);

    public static void Disconnect(WifiNetwork network) => Use(handle =>
    {
        var id = network.InterfaceId;
        return WlanDisconnect(handle, ref id, IntPtr.Zero) == 0;
    }, false);

    private static List<Guid> GetInterfaces(IntPtr handle)
    {
        var result = new List<Guid>();
        if (WlanEnumInterfaces(handle, IntPtr.Zero, out IntPtr list) != 0)
        {
            return result;
        }

        try
        {
            int count = Marshal.ReadInt32(list);
            for (int index = 0; index < count; index++)
            {
                result.Add(Marshal.PtrToStructure<Guid>(list + 8 + index * InterfaceInfoSize));
            }
        }
        finally
        {
            WlanFreeMemory(list);
        }

        return result;
    }

    private static T Use<T>(Func<IntPtr, T> action, T fallback)
    {
        try
        {
            if (WlanOpenHandle(2, IntPtr.Zero, out _, out IntPtr handle) != 0)
            {
                return fallback;
            }

            try
            {
                return action(handle);
            }
            finally
            {
                WlanCloseHandle(handle, IntPtr.Zero);
            }
        }
        catch (DllNotFoundException)
        {
            // Службы WLAN нет (например, на ПК без Wi‑Fi)
            return fallback;
        }
    }
}
