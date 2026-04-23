using System;
using System.IO;
using System.Text;
using CarolusNexus.Models;

namespace CarolusNexus.Services;

public static class PromptToFlowCompilerService
{
    public static string BuildCompiledFlow(NexusSettings settings, string? prompt)
    {
        var request = string.IsNullOrWhiteSpace(prompt)
            ? "When an operator screen is visible, analyze context, propose a safe next step, and prepare a note without posting."
            : prompt!.Trim();
        var risk = request.Contains("post", StringComparison.OrdinalIgnoreCase)
            || request.Contains("book", StringComparison.OrdinalIgnoreCase)
            || request.Contains("send", StringComparison.OrdinalIgnoreCase)
            ? "high"
            : "medium";

        var sb = new StringBuilder();
        sb.AppendLine("Prompt-to-Flow Compiler");
        sb.AppendLine($"Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        sb.AppendLine();
        sb.AppendLine("Intent");
        sb.AppendLine(request);
        sb.AppendLine();
        sb.AppendLine("Compiled operator flow draft");
        sb.AppendLine("1. Observe foreground window and capture UIA/context snapshot.");
        sb.AppendLine("2. Classify application/form and determine whether AX affinity applies.");
        sb.AppendLine("3. Extract visible facts only; mark missing data as assumptions.");
        sb.AppendLine("4. Run privacy firewall before any model call.");
        sb.AppendLine("5. Ask model for safe plan with facts/assumptions/risk/next-action sections.");
        sb.AppendLine("6. Run evidence contract and hallucination guard.");
        sb.AppendLine("7. Present dry-run plan to operator.");
        sb.AppendLine("8. Require approval before any write/post/send action.");
        sb.AppendLine("9. Persist audit line, ROI estimate, and resume state.");
        sb.AppendLine();
        sb.AppendLine("Safety gates");
        sb.AppendLine($"- Risk level: {risk}");
        sb.AppendLine($"- Safety profile: {settings.Safety.Profile}");
        sb.AppendLine("- Never send secrets or unredacted sensitive data to the model.");
        sb.AppendLine("- Execute only after human approval; default output is prepare/draft/check.");
        sb.AppendLine();
        sb.AppendLine("Test cases");
        sb.AppendLine("- Prompt contains sensitive value -> privacy firewall redacts or blocks.");
        sb.AppendLine("- Prompt requests posting/sending -> second confirmation required.");
        sb.AppendLine("- Missing evidence -> answer must state assumptions.");
        return sb.ToString().TrimEnd();
    }

    public static string ExportCompiledFlow(NexusSettings settings, string? prompt)
    {
        Directory.CreateDirectory(AppPaths.DataDir);
        var path = Path.Combine(AppPaths.DataDir, $"prompt-to-flow-{DateTime.Now:yyyyMMdd-HHmmss}.md");
        File.WriteAllText(path, BuildCompiledFlow(settings, prompt) + Environment.NewLine);
        return path;
    }
}
