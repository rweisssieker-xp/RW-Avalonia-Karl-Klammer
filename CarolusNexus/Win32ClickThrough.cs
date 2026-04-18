using System;
using System.Runtime.InteropServices;
using Avalonia.Controls;
using Avalonia.Platform;

namespace CarolusNexus;

/// <summary>
/// Macht ein Fenster unter Windows „durchklickbar“, damit ein Overlay den Mauszeiger nicht blockiert.
/// </summary>
internal static class Win32ClickThrough
{
    private const int GwlExStyle = -20;
    private const nint WsExLayered = 0x80000;
    private const nint WsExTransparent = 0x20;

    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW", SetLastError = true)]
    private static extern nint GetWindowLongPtr(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW", SetLastError = true)]
    private static extern nint SetWindowLongPtr(IntPtr hWnd, int nIndex, nint dwNewLong);

    public static void EnableForWindow(Window window)
    {
        if (!OperatingSystem.IsWindows())
            return;

        var handle = window.TryGetPlatformHandle()?.Handle ?? IntPtr.Zero;
        if (handle == IntPtr.Zero)
            return;

        var ex = GetWindowLongPtr(handle, GwlExStyle);
        SetWindowLongPtr(handle, GwlExStyle, ex | WsExLayered | WsExTransparent);
    }
}
