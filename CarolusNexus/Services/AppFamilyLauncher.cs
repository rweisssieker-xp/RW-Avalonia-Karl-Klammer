using System;
using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;
using CarolusNexus.Models;

namespace CarolusNexus.Services;

/// <summary>
/// Startet bekannte Desktop-Apps über das Token-Format <c>app|family</c> bzw. <c>app|open|path</c>.
/// </summary>
public static class AppFamilyLauncher
{
    private static readonly Regex AppPipe = new(
        @"^app\|(\w+)(?:\|(.+))?$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public static bool TryParse(string argument, out string family, out string? rest)
    {
        family = "";
        rest = null;
        var m = AppPipe.Match((argument ?? "").Trim());
        if (!m.Success)
            return false;
        family = m.Groups[1].Value.ToLowerInvariant();
        rest = m.Groups[2].Success ? m.Groups[2].Value.Trim() : null;
        if (family == "open" && !string.IsNullOrEmpty(rest))
        {
            // app|open|C:\foo.exe — family becomes "open", rest is path
        }

        return true;
    }

    /// <summary>
    /// Wenn das Argument <c>app|…</c> ist: starten und Meldung liefern, sonst <c>false</c>.
    /// </summary>
    public static bool TryLaunch(string argument, NexusSettings settings, out string message)
    {
        message = "";
        if (!TryParse(argument, out var fam, out var rest))
            return false;

        if (!OperatingSystem.IsWindows())
        {
            message = "[SKIP] app| not Windows";
            return true;
        }

        if (!string.Equals(settings.Safety.Profile, "power-user", StringComparison.OrdinalIgnoreCase))
        {
            message = "[SKIP] app| requires safety profile power-user";
            return true;
        }

        if (!PlanGuard.IsAllowed(settings, argument))
        {
            message = "[BLOCKED] Safety-Policy";
            return true;
        }

        try
        {
            if (fam == "open" && !string.IsNullOrWhiteSpace(rest))
            {
                var path = rest.Trim().Trim('"');
                if (!File.Exists(path) && !Directory.Exists(path))
                {
                    message = "[SKIP] app|open path not found";
                    return true;
                }

                Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
                message = "[OK] app|open";
                return true;
            }

            var psi = ResolveStartInfo(fam, rest);
            if (psi == null)
            {
                message = "[SKIP] app| family not mapped — use app|open|\"C:\\Path\\app.exe\" or set AX_CLIENT_EXE for ax2012";
                return true;
            }

            Process.Start(psi);
            message = "[OK] app|" + fam;
            return true;
        }
        catch (Exception ex)
        {
            message = "[ERR] app| " + ex.Message;
            return true;
        }
    }

    private static ProcessStartInfo? ResolveStartInfo(string fam, string? rest)
    {
        switch (fam)
        {
            case "explorer":
                return new ProcessStartInfo("explorer.exe") { UseShellExecute = true };
            case "notepad":
                return new ProcessStartInfo("notepad.exe") { UseShellExecute = true };
            case "calc":
            case "calculator":
                return new ProcessStartInfo("calc.exe") { UseShellExecute = true };
            case "cmd":
                return new ProcessStartInfo("cmd.exe") { UseShellExecute = true };
            case "powershell":
                return new ProcessStartInfo("powershell.exe") { UseShellExecute = true };
            case "browser":
            {
                var url = string.IsNullOrWhiteSpace(rest) ? "https://www.example.com" : rest!.Trim();
                if (!url.Contains("://", StringComparison.Ordinal))
                    url = "https://" + url;
                return new ProcessStartInfo(url) { UseShellExecute = true };
            }
            case "word":
                return new ProcessStartInfo("winword.exe") { UseShellExecute = true };
            case "excel":
                return new ProcessStartInfo("excel.exe") { UseShellExecute = true };
            case "outlook":
                return new ProcessStartInfo("outlook.exe") { UseShellExecute = true };
            case "teams":
                return new ProcessStartInfo("ms-teams.exe") { UseShellExecute = true };
            case "ax2012":
            {
                var exe = (DotEnvStore.Get("AX_CLIENT_EXE") ?? "").Trim();
                if (string.IsNullOrEmpty(exe) || !File.Exists(exe))
                    return null;
                return new ProcessStartInfo(exe) { UseShellExecute = true };
            }
            default:
                return null;
        }
    }
}
