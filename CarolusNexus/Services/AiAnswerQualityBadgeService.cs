using System;
using System.IO;
using System.Text;
using CarolusNexus.Models;

namespace CarolusNexus.Services;

public static class AiAnswerQualityBadgeService
{
    public static string BuildQualityBadge(NexusSettings settings, string? prompt)
    {
        var hasPrompt = !string.IsNullOrWhiteSpace(prompt);
        var hasKnowledge = settings.UseLocalKnowledge && File.Exists(AppPaths.KnowledgeIndex);
        var hasKey = DotEnvStore.HasProviderKey(settings.Provider);
        var strict = settings.Safety.Profile.Equals("strict", StringComparison.OrdinalIgnoreCase);
        var risk = strict ? "low" : hasKnowledge ? "medium-low" : "medium";
        var badge = hasKey && hasKnowledge ? "GREEN" : hasKey ? "YELLOW" : "RED";

        var sb = new StringBuilder();
        sb.AppendLine("AI Answer Quality Badges");
        sb.AppendLine($"Overall badge: {badge}");
        sb.AppendLine();
        sb.AppendLine($"- Provider key: {(hasKey ? "present" : "missing")}");
        sb.AppendLine($"- Local knowledge: {(hasKnowledge ? "indexed" : settings.UseLocalKnowledge ? "enabled but index missing" : "disabled")}");
        sb.AppendLine($"- Safety profile: {settings.Safety.Profile}");
        sb.AppendLine($"- Hallucination risk: {risk}");
        sb.AppendLine($"- Prompt present: {(hasPrompt ? "yes" : "no")}");
        sb.AppendLine();
        sb.AppendLine("Recommended answer contract");
        sb.AppendLine("- State assumptions explicitly.");
        sb.AppendLine("- Cite local knowledge when available.");
        sb.AppendLine("- Separate facts, inference, and next action.");
        sb.AppendLine("- Refuse or ask for confirmation before high-risk execution.");
        return sb.ToString().TrimEnd();
    }
}
