using System.Runtime.InteropServices;

namespace WinToMac.Native;

/// <summary>
/// Размытие того, что под окном (SetWindowCompositionAttribute).
/// Цвет и прозрачность подложки рисует само WPF-окно, здесь включается только размытие.
/// </summary>
public static class Blur
{
    private enum AccentState
    {
        Disabled = 0,
        BlurBehind = 3,
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct AccentPolicy
    {
        public AccentState AccentState;
        public int AccentFlags;
        public uint GradientColor;
        public int AnimationId;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct WindowCompositionAttributeData
    {
        public int Attribute;
        public IntPtr Data;
        public int SizeOfData;
    }

    private const int WCA_ACCENT_POLICY = 19;

    [DllImport("user32.dll")]
    private static extern int SetWindowCompositionAttribute(IntPtr hwnd, ref WindowCompositionAttributeData data);

    public static void Apply(IntPtr hwnd, bool enabled)
    {
        if (hwnd == IntPtr.Zero)
        {
            return;
        }

        var policy = new AccentPolicy { AccentState = enabled ? AccentState.BlurBehind : AccentState.Disabled };
        int size = Marshal.SizeOf(policy);
        IntPtr pointer = Marshal.AllocHGlobal(size);
        try
        {
            Marshal.StructureToPtr(policy, pointer, false);
            var data = new WindowCompositionAttributeData
            {
                Attribute = WCA_ACCENT_POLICY,
                Data = pointer,
                SizeOfData = size,
            };
            SetWindowCompositionAttribute(hwnd, ref data);
        }
        finally
        {
            Marshal.FreeHGlobal(pointer);
        }
    }
}
