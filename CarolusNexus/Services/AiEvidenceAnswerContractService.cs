using System;
using System.Text;
using CarolusNexus.Models;

namespace CarolusNexus.Services;

public static class AiEvidenceAnswerContractService
{
    public static string BuildContract(NexusSettings settings, string? prompt)
    {
        var sb = new StringBuilder();
        sb.AppendLine("AI Evidence Answer Contract");
        sb.AppendLine($"Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        sb.AppendLine();
        sb.AppendLine("Required answer sections");
        sb.AppendLine("1. Facts visible in context");
        sb.AppendLine("2. Assumptions / missing evidence");
        sb.AppendLine("3. Source confidence");
        sb.AppendLine("4. Risk level");
        sb.AppendLine("5. Safe next action");
        sb.AppendLine("6. Execution allowed: yes/no and why");
        sb.AppendLine();
        sb.AppendLine("Current badge");
        sb.AppendLine(AiAnswerQualityBadgeService.BuildQualityBadge(settings, prompt));
        sb.AppendLine();
        sb.AppendLine("Reusable answer template");
        sb.AppendLine("Facts:");
        sb.AppendLine("- ...");
        sb.AppendLine("Assumptions:");
        sb.AppendLine("- ...");
        sb.AppendLine("Source confidence:");
        sb.AppendLine("- high/medium/low, with reason");
        sb.AppendLine("Risk:");
        sb.AppendLine("- low/medium/high");
        sb.AppendLine("Safe next action:");
        sb.AppendLine("- ...");
        sb.AppendLine("Execution:");
        sb.AppendLine("- Not allowed without explicit approval for write/post/send actions.");
        return sb.ToString().TrimEnd();
    }
}
