using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Runtime.Versioning;
using System.Security.Cryptography;
using System.Windows.Forms;

namespace CarolusNexus.Services;

[SupportedOSPlatform("windows")]
public static class ScreenCaptureWin
{
    /// <summary>PNG base64 strings, ein Eintrag pro Monitor.</summary>
    public static IReadOnlyList<(string Label, string Base64Png)> CaptureAllMonitorsPngBase64()
    {
        var list = new List<(string, string)>();
        foreach (var screen in Screen.AllScreens.OrderBy(s => s.Bounds.Left))
        {
            var b = screen.Bounds;
            using var bmp = new Bitmap(b.Width, b.Height, PixelFormat.Format24bppRgb);
            using (var g = Graphics.FromImage(bmp))
            {
                g.CopyFromScreen(b.Left, b.Top, 0, 0, b.Size);
            }

            using var ms = new MemoryStream();
            bmp.Save(ms, ImageFormat.Png);
            var b64 = Convert.ToBase64String(ms.ToArray());
            var label = screen.Primary ? "primary" : $"screen@{b.Left},{b.Top}";
            list.Add((label, b64));
        }

        return list;
    }

    /// <summary>Kurzer SHA256-Präfix des primären Monitors (für Watch-Modus / Änderungserkennung).</summary>
    public static string? PrimaryMonitorSha256Prefix16()
    {
        var all = CaptureAllMonitorsPngBase64();
        if (all.Count == 0)
            return null;
        var bytes = Convert.FromBase64String(all[0].Base64Png);
        var hash = SHA256.HashData(bytes);
        return Convert.ToHexString(hash)[..16];
    }
}
