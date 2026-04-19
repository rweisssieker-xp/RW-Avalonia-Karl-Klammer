using System.Runtime.InteropServices;

namespace CarolusNexus.Services;

internal static class Win32AsyncKey
{
    [DllImport("user32.dll")]
    public static extern short GetAsyncKeyState(int vKey);
}
