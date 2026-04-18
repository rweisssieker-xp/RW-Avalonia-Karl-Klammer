using System.Runtime.InteropServices;

namespace CarolusNexus;

internal static class CursorTracking
{
    [StructLayout(LayoutKind.Sequential)]
    public struct Point
    {
        public int X;
        public int Y;
    }

    [DllImport("user32.dll")]
    public static extern bool GetCursorPos(out Point lpPoint);
}
