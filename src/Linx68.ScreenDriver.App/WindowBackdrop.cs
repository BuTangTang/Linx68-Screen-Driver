using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;

namespace Linx68.ScreenDriver.App;

internal static class WindowBackdrop
{
    private const int DwmwaUseImmersiveDarkMode = 20;
    private const int DwmwaWindowCornerPreference = 33;
    private const int DwmwaBorderColor = 34;
    private const int DwmwaSystemBackdropType = 38;
    private const int DwmwcpRound = 2;
    private const int DwmsbtMainWindow = 2;
    private const uint DwmColorNone = 0xFFFFFFFE;

    public static bool TryApplyRoundedCorners(Window window)
    {
        if (!OperatingSystem.IsWindowsVersionAtLeast(10, 0, 22000))
        {
            return false;
        }

        var handle = new WindowInteropHelper(window).Handle;
        if (handle == IntPtr.Zero)
        {
            return false;
        }

        var cornerPreference = DwmwcpRound;
        return DwmSetWindowAttribute(
            handle,
            DwmwaWindowCornerPreference,
            ref cornerPreference,
            sizeof(int)) >= 0;
    }
    public static bool TryApplyMica(Window window, bool useDarkMode)
    {
        if (!OperatingSystem.IsWindowsVersionAtLeast(10, 0, 22621))
        {
            return false;
        }

        var handle = new WindowInteropHelper(window).Handle;
        if (handle == IntPtr.Zero)
        {
            return false;
        }

        if (HwndSource.FromHwnd(handle) is { CompositionTarget: not null } source)
        {
            source.CompositionTarget.BackgroundColor = Colors.Transparent;
        }

        var margins = new Margins { Left = -1, Right = -1, Top = -1, Bottom = -1 };
        _ = DwmExtendFrameIntoClientArea(handle, ref margins);

        var darkMode = useDarkMode ? 1 : 0;
        _ = DwmSetWindowAttribute(handle, DwmwaUseImmersiveDarkMode, ref darkMode, sizeof(int));
        var cornerPreference = DwmwcpRound;
        _ = DwmSetWindowAttribute(handle, DwmwaWindowCornerPreference, ref cornerPreference, sizeof(int));
        var borderColor = DwmColorNone;
        _ = DwmSetWindowAttribute(handle, DwmwaBorderColor, ref borderColor, sizeof(uint));
        var backdropType = DwmsbtMainWindow;
        return DwmSetWindowAttribute(handle, DwmwaSystemBackdropType, ref backdropType, sizeof(int)) >= 0;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Margins
    {
        public int Left;
        public int Right;
        public int Top;
        public int Bottom;
    }

    [DllImport("dwmapi.dll")]
    private static extern int DwmExtendFrameIntoClientArea(IntPtr window, ref Margins margins);

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr window, int attribute, ref int value, int valueSize);

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr window, int attribute, ref uint value, int valueSize);
}
