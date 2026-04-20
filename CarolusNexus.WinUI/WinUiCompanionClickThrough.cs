using System;
using System.Runtime.InteropServices;

namespace CarolusNexus_WinUI;

/// <summary>Makes the companion overlay click-through (parity with Avalonia Win32ClickThrough).</summary>
internal static class WinUiCompanionClickThrough
{
    private const int GwlExStyle = -20;
    private const nint WsExLayered = 0x80000;
    private const nint WsExTransparent = 0x20;

    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW", SetLastError = true)]
    private static extern nint GetWindowLongPtr(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW", SetLastError = true)]
    private static extern nint SetWindowLongPtr(IntPtr hWnd, int nIndex, nint dwNewLong);

    public static void Enable(IntPtr hwnd)
    {
        if (hwnd == IntPtr.Zero || !OperatingSystem.IsWindows())
            return;
        var ex = GetWindowLongPtr(hwnd, GwlExStyle);
        SetWindowLongPtr(hwnd, GwlExStyle, ex | WsExLayered | WsExTransparent);
    }
}
