using System;
using System.Diagnostics;
using System.IO;
using CarolusNexus.Models;

namespace CarolusNexus.Services;

/// <summary>Kanal <c>script</c>: eingeschränkte lokale Hooks (PowerShell/cmd) mit Sandbox-Flag.</summary>
public static class ScriptHookRunner
{
    public static string TryRun(RecipeStep step, NexusSettings settings)
    {
        if (!OperatingSystem.IsWindows())
            return "[SKIP] script channel requires Windows";

        if (!string.Equals(settings.Safety.Profile, "power-user", StringComparison.OrdinalIgnoreCase))
            return "[SKIP] script channel requires safety profile power-user";

        if (!settings.Safety.AllowScriptHooks)
            return "[BLOCKED] script hooks disabled in settings";

        var raw = (step.ActionArgument ?? "").Trim();
        if (raw.Length == 0)
            return "[SKIP] script: empty argument";

        if (!PlanGuard.IsAllowed(settings, raw))
            return "[BLOCKED] Safety-Policy";

        if (settings.Safety.SandboxScriptHooks)
            return "[SANDBOX] would run script (sandboxScriptHooks=true): " + Truncate(raw, 200);

        if (raw.StartsWith("powershell:", StringComparison.OrdinalIgnoreCase))
        {
            var ps = raw["powershell:".Length..].Trim();
            return RunProcess("powershell.exe", "-NoProfile -NonInteractive -ExecutionPolicy Bypass -Command " + Quote(ps));
        }

        if (raw.StartsWith("cmd:", StringComparison.OrdinalIgnoreCase))
        {
            var cmd = raw["cmd:".Length..].Trim();
            return RunProcess("cmd.exe", "/c " + cmd);
        }

        return "[SKIP] script: use powershell:... or cmd:... prefix";
    }

    private static string Quote(string s)
    {
        if (s.Contains('"'))
            return "\"" + s.Replace("\"", "\\\"") + "\"";
        return "\"" + s + "\"";
    }

    private static string RunProcess(string file, string args)
    {
        try
        {
            using var p = Process.Start(new ProcessStartInfo(file, args)
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                WorkingDirectory = AppPaths.RepoRoot
            });
            if (p == null)
                return "[ERR] script: could not start";
            p.WaitForExit(120_000);
            var ok = p.ExitCode == 0;
            return ok ? "[OK] script exit 0" : "[ERR] script exit " + p.ExitCode;
        }
        catch (Exception ex)
        {
            return "[ERR] script: " + ex.Message;
        }
    }

    private static string Truncate(string s, int max)
    {
        if (s.Length <= max)
            return s;
        return s[..max] + "…";
    }
}
