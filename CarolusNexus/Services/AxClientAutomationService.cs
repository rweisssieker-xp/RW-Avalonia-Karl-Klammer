using System;
using CarolusNexus.Models;

namespace CarolusNexus.Services;

/// <summary>
/// Explizite Schicht für AX-/Dynamics-Fat-Client-Token (<c>ax.*</c>) — lesend Kontext, schreibend delegiert an UIA (§14 Handbuch).
/// Hinter <see cref="NexusSettings.AxIntegrationEnabled"/> und nur mit power-user.
/// </summary>
public static class AxClientAutomationService
{
    public static bool TryExecute(string argument, NexusSettings settings, out string message)
    {
        message = "";
        var arg = (argument ?? "").Trim();
        if (!arg.StartsWith("ax.", StringComparison.OrdinalIgnoreCase))
            return false;

        if (!OperatingSystem.IsWindows())
        {
            message = "[SKIP] ax.* not Windows";
            return true;
        }

        if (!settings.AxIntegrationEnabled)
        {
            message = "[SKIP] AX integration disabled (Setup)";
            return true;
        }

        if (!string.Equals(settings.Safety.Profile, "power-user", StringComparison.OrdinalIgnoreCase))
        {
            message = "[SKIP] ax.* requires safety profile power-user";
            return true;
        }

        if (!PlanGuard.IsAllowed(settings, arg))
        {
            message = "[BLOCKED] Safety-Policy";
            return true;
        }

        try
        {
            var lower = arg.ToLowerInvariant();

            if (lower == "ax.integration.status")
            {
                message = BuildIntegrationStatus(settings);
                return true;
            }

            if (IsAxReadToken(lower))
            {
                return TryExecuteAxReadToken(arg, settings, lower, out message);
            }

            if (lower is "ax.read_context" or "ax.snapshot" or "ax.form_summary")
            {
                return TryExecuteAxReadToken(arg, settings, lower, out message);
            }

            if (IsAxWriteToken(lower))
            {
                return TryExecuteAxWriteToken(arg, settings, out message);
            }

            if (lower == "ax.com.logon")
            {
                message = Ax2012ComBusinessConnectorRuntime.TryLogonProbe(settings);
                return true;
            }

            message = "[SKIP] ax token not recognized: " + arg;
            return true;
        }
        catch (Exception ex)
        {
            message = "[ERR] ax.* " + ex.Message;
            return true;
        }
    }

    private static string TruncateForLog(string s)
    {
        const int max = 14_000;
        if (s.Length <= max)
            return s;
        return s[..max] + "\n… (truncated)";
    }

    private static string BuildIntegrationStatus(NexusSettings s)
    {
        static string Show(string? v) => string.IsNullOrWhiteSpace(v) ? "(empty)" : v.Trim();

        return string.Join("\n", new[]
        {
            "axIntegrationEnabled: " + s.AxIntegrationEnabled,
            "axTestTenantLabel: " + Show(s.AxTestTenantLabel),
            "axIntegrationBackend: " + Show(s.AxIntegrationBackend),
            "axODataBaseUrl: " + Show(s.AxODataBaseUrl),
            "axODataUseDefaultCredentials: " + s.AxODataUseDefaultCredentials,
            "axAifServiceBaseUrl: " + Show(s.AxAifServiceBaseUrl),
            "axDataAreaId: " + Show(s.AxDataAreaId),
            "axBusinessConnectorNetAssemblyPath: " + Show(s.AxBusinessConnectorNetAssemblyPath),
            "axBcObjectServer: " + Show(s.AxBcObjectServer),
            "axBcDatabase: " + Show(s.AxBcDatabase),
            "axBcLanguage: " + Show(s.AxBcLanguage),
            "Secrets: use windows/.env → AX_HTTP_USER / AX_HTTP_PASSWORD (Basic) or Windows-integrated."
        });
    }

    private static bool IsAxReadToken(string lower) =>
        lower == "ax.aif.ping" || lower.StartsWith("ax.odata.get:", StringComparison.Ordinal);

    private static bool IsAxWriteToken(string lower) =>
        lower.StartsWith("ax.invoke:", StringComparison.Ordinal)
        || lower.StartsWith("ax.setvalue:", StringComparison.Ordinal)
        || lower.StartsWith("ax.expand:", StringComparison.Ordinal);

    private static bool TryExecuteAxReadToken(string arg, NexusSettings settings, string lower, out string message)
    {
        if (lower == "ax.aif.ping")
        {
            var u = (settings.AxAifServiceBaseUrl ?? "").Trim();
            message = string.IsNullOrEmpty(u)
                ? "[SKIP] AxAifServiceBaseUrl empty — set AIF/SOAP base in Setup"
                : Ax2012ODataClient.GetAbsoluteSync(settings, u);
            return true;
        }

        if (lower.StartsWith("ax.odata.get:", StringComparison.Ordinal))
        {
            var path = arg["ax.odata.get:".Length..].Trim();
            message = Ax2012ODataClient.GetStringSync(settings, path);
            return true;
        }

        var d = ForegroundWindowInfo.TryReadDetail();
        var fam = d == null ? "?" : OperatorAdapterRegistry.ResolveFamily(d.Value.ProcessName, d.Value.Title);
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("[AX][read] foreground · family=" + fam);
        sb.AppendLine("Integration backend (settings): " + (settings.AxIntegrationBackend ?? "foreground_uia"));
        if (!string.IsNullOrWhiteSpace(settings.AxDataAreaId))
            sb.AppendLine("DataAreaId (test): " + settings.AxDataAreaId);
        if (d != null)
        {
            sb.AppendLine($"Process={d.Value.ProcessName} · “{d.Value.Title}”");
            if (!string.IsNullOrWhiteSpace(settings.AxTestTenantLabel))
                sb.AppendLine("Tenant hint (settings): " + settings.AxTestTenantLabel);
        }

        var detail = lower.Contains("form_summary", StringComparison.Ordinal)
            ? ForegroundUiAutomationContext.BuildFormSummary(settings, 32, 10)
            : ForegroundUiAutomationContext.BuildFormSummary(settings, 56, 14);
        sb.AppendLine("--- UIA ---");
        sb.AppendLine(detail);
        var sel = ForegroundUiAutomationContext.TryReadSelectionHint(settings);
        if (!string.IsNullOrEmpty(sel))
        {
            sb.AppendLine("---");
            sb.AppendLine(sel);
        }

        var deep = ForegroundUiAutomationContext.BuildDeepSelectionSummary(settings);
        if (!string.IsNullOrWhiteSpace(deep))
        {
            sb.AppendLine("---");
            sb.AppendLine(deep);
        }

        message = TruncateForLog(sb.ToString());
        return true;
    }

    private static bool TryExecuteAxWriteToken(string arg, NexusSettings settings, out string message)
    {
        var uia = "uia" + arg[2..];
        if (UiAutomationActions.TryParseAndExecute(uia, settings, out var uiaMsg))
        {
            message = uiaMsg.StartsWith("[", StringComparison.Ordinal) ? uiaMsg : "[OK] ax→" + uiaMsg;
            return true;
        }

        message = "[ERR] ax delegation to UIA failed";
        return true;
    }
}
