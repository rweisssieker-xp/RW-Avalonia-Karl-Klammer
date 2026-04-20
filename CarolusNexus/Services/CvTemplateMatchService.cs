using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Windows.Forms;

namespace CarolusNexus.Services;

/// <summary>Minimaler Template-Match auf dem primären Monitor (Grayscale-SSD, Downscale) als CV-Fallback.</summary>
[SupportedOSPlatform("windows")]
public static class CvTemplateMatchService
{
    /// <summary>
    /// Token: <c>cv.click:file=C:\path\to.png</c> oder <c>cv.click:template=C:\path</c>
    /// </summary>
    public static bool TryParseAndExecute(string argument, out string message)
    {
        message = "";
        var arg = (argument ?? "").Trim();
        if (!arg.StartsWith("cv.", StringComparison.OrdinalIgnoreCase))
            return false;

        if (!OperatingSystem.IsWindows())
        {
            message = "[SKIP] cv.* requires Windows";
            return true;
        }

        var path = ExtractPath(arg);
        if (string.IsNullOrEmpty(path) || !File.Exists(path))
        {
            message = "[ERR] cv: missing or invalid file path";
            return true;
        }

        try
        {
            message = TryClickTemplate(path);
        }
        catch (Exception ex)
        {
            message = "[ERR] cv: " + ex.Message;
        }

        return true;
    }

    private static string ExtractPath(string arg)
    {
        var lower = arg.ToLowerInvariant();
        if (lower.StartsWith("cv.click:", StringComparison.Ordinal))
        {
            var rest = arg["cv.click:".Length..].Trim();
            if (rest.StartsWith("file=", StringComparison.OrdinalIgnoreCase))
                return rest["file=".Length..].Trim().Trim('"');
            if (rest.StartsWith("template=", StringComparison.OrdinalIgnoreCase))
                return rest["template=".Length..].Trim().Trim('"');
            return rest.Trim().Trim('"');
        }

        return "";
    }

    /// <summary>Öffentlich für Self-Heal / Fallback nach fehlgeschlagenem UIA-Schritt.</summary>
    public static string TryClickTemplate(string templatePath)
    {
        using var needleRaw = TryLoadBitmap(templatePath);
        if (needleRaw == null)
            return "[ERR] cv: could not load template";
        using var needle = ToRgb24(needleRaw);

        var screen = Screen.AllScreens.OrderBy(s => s.Bounds.Left).FirstOrDefault();
        if (screen == null)
            return "[ERR] cv: no screen";

        var b = screen.Bounds;
        using var hay = new Bitmap(b.Width, b.Height, PixelFormat.Format24bppRgb);
        using (var g = Graphics.FromImage(hay))
            g.CopyFromScreen(b.Left, b.Top, 0, 0, b.Size);

        var scale = Math.Clamp(b.Width / 320, 4, 12);
        using var hayS = DownscaleGray(hay, scale);
        using var needS = DownscaleGray(needle, scale);
        if (needS.Width >= hayS.Width || needS.Height >= hayS.Height)
            return "[ERR] cv: template larger than screen (after downscale)";

        var (bx, by, score) = BestSsd(hayS, needS);
        if (score > 1e15) // no match
            return "[ERR] cv: no match above threshold";

        var cx = b.Left + (bx + needS.Width / 2) * scale;
        var cy = b.Top + (by + needS.Height / 2) * scale;

        if (!SetCursorPos(cx, cy))
            return "[ERR] cv: SetCursorPos";
        mouse_event(MOUSEEVENTF_LEFTDOWN, 0, 0, 0, UIntPtr.Zero);
        mouse_event(MOUSEEVENTF_LEFTUP, 0, 0, 0, UIntPtr.Zero);
        return $"[OK] cv.click score≈{score:0} at ({cx},{cy})";
    }

    private static Bitmap? TryLoadBitmap(string path)
    {
        try
        {
            return new Bitmap(path);
        }
        catch
        {
            return null;
        }
    }

    private static Bitmap ToRgb24(Bitmap src)
    {
        var b = new Bitmap(src.Width, src.Height, PixelFormat.Format24bppRgb);
        using (var g = Graphics.FromImage(b))
            g.DrawImage(src, 0, 0);
        return b;
    }

    private static unsafe Bitmap DownscaleGray(Bitmap src, int factor)
    {
        var w = Math.Max(1, src.Width / factor);
        var h = Math.Max(1, src.Height / factor);
        var dst = new Bitmap(w, h, PixelFormat.Format24bppRgb);
        var srcData = src.LockBits(
            new Rectangle(0, 0, src.Width, src.Height),
            ImageLockMode.ReadOnly,
            PixelFormat.Format24bppRgb);
        var dstData = dst.LockBits(
            new Rectangle(0, 0, w, h),
            ImageLockMode.WriteOnly,
            PixelFormat.Format24bppRgb);
        try
        {
            var sp = (byte*)srcData.Scan0.ToPointer();
            var dp = (byte*)dstData.Scan0.ToPointer();
            var sStride = srcData.Stride;
            var dStride = dstData.Stride;
            for (var y = 0; y < h; y++)
            {
                var sy = y * factor;
                for (var x = 0; x < w; x++)
                {
                    var sx = x * factor;
                    var si = sy * sStride + sx * 3;
                    var gray = (sp[si] + sp[si + 1] + sp[si + 2]) / 3;
                    var di = y * dStride + x * 3;
                    dp[di] = dp[di + 1] = dp[di + 2] = (byte)gray;
                }
            }
        }
        finally
        {
            src.UnlockBits(srcData);
            dst.UnlockBits(dstData);
        }

        return dst;
    }

    private static unsafe (int bx, int by, double score) BestSsd(Bitmap hay, Bitmap needle)
    {
        var hd = hay.LockBits(new Rectangle(0, 0, hay.Width, hay.Height), ImageLockMode.ReadOnly, PixelFormat.Format24bppRgb);
        var nd = needle.LockBits(new Rectangle(0, 0, needle.Width, needle.Height), ImageLockMode.ReadOnly, PixelFormat.Format24bppRgb);
        try
        {
            var hp = (byte*)hd.Scan0.ToPointer();
            var np = (byte*)nd.Scan0.ToPointer();
            var hs = hd.Stride;
            var ns = nd.Stride;
            var nw = needle.Width;
            var nh = needle.Height;
            var hw = hay.Width;
            var hh = hay.Height;
            var best = double.MaxValue;
            var bx = 0;
            var by = 0;
            for (var y = 0; y <= hh - nh; y++)
            {
                for (var x = 0; x <= hw - nw; x++)
                {
                    double ssd = 0;
                    for (var yy = 0; yy < nh; yy++)
                    {
                        for (var xx = 0; xx < nw; xx++)
                        {
                            var hi = (y + yy) * hs + (x + xx) * 3;
                            var ni = yy * ns + xx * 3;
                            var d0 = hp[hi] - np[ni];
                            var d1 = hp[hi + 1] - np[ni + 1];
                            var d2 = hp[hi + 2] - np[ni + 2];
                            ssd += d0 * d0 + d1 * d1 + d2 * d2;
                        }
                    }

                    if (ssd < best)
                    {
                        best = ssd;
                        bx = x;
                        by = y;
                    }
                }
            }

            var thresh = nw * nh * 50.0 * 255.0; // grobe Schwelle
            if (best > thresh)
                return (0, 0, double.MaxValue);
            return (bx, by, best);
        }
        finally
        {
            hay.UnlockBits(hd);
            needle.UnlockBits(nd);
        }
    }

    private const uint MOUSEEVENTF_LEFTDOWN = 0x0002;
    private const uint MOUSEEVENTF_LEFTUP = 0x0004;

    [DllImport("user32.dll")]
    private static extern void mouse_event(uint dwFlags, uint dx, uint dy, uint dwData, UIntPtr dwExtraInfo);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool SetCursorPos(int X, int Y);
}
