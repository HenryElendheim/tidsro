using System.Runtime.InteropServices;

namespace Tidsro.Services;

internal static class NativeFocus
{
    [DllImport("user32.dll")] internal static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] private static extern bool SetForegroundWindow(IntPtr hWnd);

    internal static void Restore(IntPtr hWnd) => SetForeground(hWnd);

    internal static void SetForeground(IntPtr hWnd)
    {
        if (hWnd != IntPtr.Zero) SetForegroundWindow(hWnd);
    }
}
