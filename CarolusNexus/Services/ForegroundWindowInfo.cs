using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace CarolusNexus.Services;

/// <summary>Win32-Vordergrundfenster — Titel, Prozess, Klasse, Bounds für Live Context.</summary>
public readonly record struct ForegroundWindowDetail(
    string Title,
    string ProcessName,
    int ProcessId,
    string WindowClass,
    int Left,
    int Top,
    int Width,
    int Height);

public static class ForegroundWindowInfo
{
    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetWindowText(IntPtr hWnd, StringBuilder text, int maxCount);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetClassName(IntPtr hWnd, StringBuilder className, int maxCount);

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

    [DllImport("user32.dll")]
    private static extern bool GetWindowRect(IntPtr hWnd, out Rect lpRect);

    [StructLayout(LayoutKind.Sequential)]
    private struct Rect
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    public static ForegroundWindowDetail? TryReadDetail()
    {
        var h = GetForegroundWindow();
        if (h == IntPtr.Zero)
            return null;

        var titleSb = new StringBuilder(512);
        _ = GetWindowText(h, titleSb, titleSb.Capacity);
        var classSb = new StringBuilder(256);
        _ = GetClassName(h, classSb, classSb.Capacity);
        _ = GetWindowThreadProcessId(h, out var pid);
        string proc;
        try
        {
            using var p = Process.GetProcessById((int)pid);
            proc = p.ProcessName;
        }
        catch
        {
            proc = "";
        }

        int x = 0, y = 0, w = 0, ht = 0;
        if (GetWindowRect(h, out var r))
        {
            x = r.Left;
            y = r.Top;
            w = Math.Max(0, r.Right - r.Left);
            ht = Math.Max(0, r.Bottom - r.Top);
        }

        return new ForegroundWindowDetail(
            titleSb.ToString(),
            proc,
            (int)pid,
            classSb.ToString(),
            x,
            y,
            w,
            ht);
    }

    public static (string Title, string ProcessName) TryRead()
    {
        var d = TryReadDetail();
        return d == null ? ("", "") : (d.Value.Title, d.Value.ProcessName);
    }
}
