using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
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
    /// <summary>Erster Monitor in Links-nach-Rechts-Reihenfolge (wie für den Watch-Hash verwendet).</summary>
    private static Screen? FirstOrderedScreen() =>
        Screen.AllScreens.OrderBy(s => s.Bounds.Left).FirstOrDefault();

    /// <summary>PNG base64 strings, ein Eintrag pro Monitor.</summary>
    public static IReadOnlyList<(string Label, string Base64Png)> CaptureAllMonitorsPngBase64()
    {
        var list = new List<(string, string)>();
        foreach (var screen in Screen.AllScreens.OrderBy(s => s.Bounds.Left))
        {
            var b = screen.Bounds;
            using var bmp = new Bitmap(b.Width, b.Height);
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

    /// <summary>Skaliertes JPEG desselben Monitorausschnitts wie der Watch-Hash (linker Monitor in Sortierung).</summary>
    public static void SaveWatchThumbnailJpeg(string destinationPath, int maxEdge = 480, long jpegQuality = 78L)
    {
        var screen = FirstOrderedScreen();
        if (screen == null)
            return;

        var b = screen.Bounds;
        using var bmp = new Bitmap(b.Width, b.Height);
        using (var g = Graphics.FromImage(bmp))
            g.CopyFromScreen(b.Left, b.Top, 0, 0, b.Size);

        var w = bmp.Width;
        var h = bmp.Height;
        if (w <= 0 || h <= 0)
            return;

        var scale = maxEdge / (double)Math.Max(w, h);
        Bitmap toSave = bmp;
        Bitmap? scaled = null;
        try
        {
            if (scale < 1.0)
            {
                var nw = Math.Max(1, (int)Math.Round(w * scale));
                var nh = Math.Max(1, (int)Math.Round(h * scale));
                scaled = new Bitmap(nw, nh);
                using (var g = Graphics.FromImage(scaled))
                {
                    g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                    g.DrawImage(bmp, 0, 0, nw, nh);
                }

                toSave = scaled;
            }

            var dir = Path.GetDirectoryName(destinationPath);
            if (!string.IsNullOrEmpty(dir))
                Directory.CreateDirectory(dir);

            var jpegCodec = ImageCodecInfo.GetImageEncoders()
                .FirstOrDefault(ici => ici.FormatID == ImageFormat.Jpeg.Guid);
            if (jpegCodec == null)
            {
                toSave.Save(destinationPath, ImageFormat.Jpeg);
                return;
            }

            using var encParams = new EncoderParameters(1);
            encParams.Param[0] = new EncoderParameter(Encoder.Quality, jpegQuality);
            toSave.Save(destinationPath, jpegCodec, encParams);
        }
        finally
        {
            scaled?.Dispose();
        }
    }
}
