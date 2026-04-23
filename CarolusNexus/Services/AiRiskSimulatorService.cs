using System;
using System.Text;
using CarolusNexus.Models;

namespace CarolusNexus.Services;

public static class AiRiskSimulatorService
{
    public static string BuildRiskSimulation(NexusSettings settings, string? planText)
    {
        var text = planText ?? "";
        var writes = ContainsAny(text, "post", "book", "send", "delete", "update", "write", "create");
        var sensitive = ContainsAny(text, "iban", "password", "token", "secret", "customer", "vendor", "invoice");
        var strict = settings.Safety.Profile.Equals("strict", StringComparison.OrdinalIgnoreCase);
        var score = 20 + (writes ? 35 : 0) + (sensitive ? 25 : 0) + (strict ? -10 : 0);
        score = Math.Clamp(score, 5, 95);
        var level = score >= 70 ? "high" : score >= 40 ? "medium" : "low";

        var sb = new StringBuilder();
        sb.AppendLine("AI Risk Simulator");
        sb.AppendLine($"Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        sb.AppendLine($"Risk score: {score}/100 ({level})");
        sb.AppendLine();
        sb.AppendLine($"- Write/post/send intent: {(writes ? "yes" : "no")}");
        sb.AppendLine($"- Sensitive data signals: {(sensitive ? "yes" : "no")}");
        sb.AppendLine($"- Safety profile: {settings.Safety.Profile}");
        sb.AppendLine();
        sb.AppendLine("Required controls");
        sb.AppendLine("- Always dry-run first.");
        if (writes || level == "high")
            sb.AppendLine("- Require second human confirmation before execution.");
        if (sensitive)
            sb.AppendLine("- Run privacy firewall and redact before model call.");
        sb.AppendLine("- Persist audit line and resume state.");
        sb.AppendLine();
        sb.AppendLine("Safer alternative");
        sb.AppendLine("- Prepare a draft/checklist instead of executing the write action.");
        return sb.ToString().TrimEnd();
    }

    private static bool ContainsAny(string text, params string[] needles)
    {
        foreach (var needle in needles)
        {
            if (text.Contains(needle, StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }
}
