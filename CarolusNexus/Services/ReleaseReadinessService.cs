using System;
using System.IO;
using System.Text;
using CarolusNexus.Models;

namespace CarolusNexus.Services;

public static class ReleaseReadinessService
{
    public static string BuildReadinessReport(NexusSettings settings)
    {
        var recipes = SafeRecipeCount();
        var knowledge = File.Exists(AppPaths.KnowledgeIndex);
        var env = File.Exists(AppPaths.EnvFile);
        var key = DotEnvStore.HasProviderKey(settings.Provider);
        var watch = File.Exists(AppPaths.WatchSessions);
        var score = 0;
        if (env) score++;
        if (key) score++;
        if (knowledge || !settings.UseLocalKnowledge) score++;
        if (recipes > 0) score++;
        if (settings.AxIntegrationEnabled) score++;
        if (watch) score++;

        var status = score >= 5 ? "Pilot Ready" : score >= 3 ? "Internal Demo Ready" : "Setup Required";
        var sb = new StringBuilder();
        sb.AppendLine("Release Readiness Dashboard");
        sb.AppendLine($"Status: {status}");
        sb.AppendLine($"Score: {score}/6");
        sb.AppendLine();
        sb.AppendLine(Check(env, ".env exists"));
        sb.AppendLine(Check(key, "Provider key configured"));
        sb.AppendLine(Check(knowledge || !settings.UseLocalKnowledge, "Knowledge index ready or RAG disabled"));
        sb.AppendLine(Check(recipes > 0, $"Flow inventory present ({recipes})"));
        sb.AppendLine(Check(settings.AxIntegrationEnabled, "AX integration enabled"));
        sb.AppendLine(Check(watch, "Watch/session evidence present"));
        sb.AppendLine();
        sb.AppendLine("Recommendation");
        sb.AppendLine(status == "Pilot Ready"
            ? "- Use WinUI as the pilot shell and export a master proof pack after each demo."
            : "- Complete missing setup/evidence items before positioning this as pilot-ready.");
        return sb.ToString().TrimEnd();
    }

    public static string BuildPilotModeReport(NexusSettings settings)
    {
        var sb = new StringBuilder();
        sb.AppendLine("One-click Pilot Mode Plan");
        sb.AppendLine("- Default to governed demo path: context, evidence, ROI, eval, proof pack.");
        sb.AppendLine("- Keep high-risk execution behind safety gates.");
        sb.AppendLine("- Prioritize WinUI shell for buyer-facing runs.");
        sb.AppendLine("- Keep Avalonia only as fallback/reference until release parity is signed off.");
        sb.AppendLine("- Export Pilot Proof Master Pack at the end of every session.");
        sb.AppendLine();
        sb.AppendLine("Pilot mode actions");
        sb.AppendLine("- Start Demo Progress Tracker.");
        sb.AppendLine("- Run Privacy Firewall before external model calls.");
        sb.AppendLine("- Use Prompt-to-Flow only as dry-run until approved.");
        sb.AppendLine("- Export Pilot Proof Master Pack as buyer artifact.");
        sb.AppendLine();
        sb.AppendLine("Current effective defaults");
        sb.AppendLine($"- Provider: {settings.Provider}");
        sb.AppendLine($"- Safety: {settings.Safety.Profile}");
        sb.AppendLine($"- AX enabled: {settings.AxIntegrationEnabled}");
        sb.AppendLine($"- Local knowledge: {settings.UseLocalKnowledge}");
        return sb.ToString().TrimEnd();
    }

    private static string Check(bool ok, string label) => (ok ? "[x] " : "[ ] ") + label;

    private static int SafeRecipeCount()
    {
        try { return RitualRecipeStore.LoadAll().Count; }
        catch { return 0; }
    }
}
