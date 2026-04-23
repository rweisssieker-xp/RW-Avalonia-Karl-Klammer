using System;
using System.IO;
using System.Linq;
using System.Text;
using CarolusNexus.Models;

namespace CarolusNexus.Services;

public static class AiAgentOpsUspService
{
    public static string BuildModelRouterReport(NexusSettings settings, string? currentPrompt = null)
    {
        var prompt = currentPrompt?.Trim() ?? "";
        var rag = KnowledgeSnippetService.BuildAugmentationResult(prompt, 1200);
        var insight = OperatorInsightService.BuildSnapshot(settings);
        var needsVision = prompt.Contains("screen", StringComparison.OrdinalIgnoreCase)
                          || prompt.Contains("screenshot", StringComparison.OrdinalIgnoreCase)
                          || prompt.Contains("bild", StringComparison.OrdinalIgnoreCase);
        var needsPlan = prompt.Contains("plan", StringComparison.OrdinalIgnoreCase)
                        || prompt.Contains("flow", StringComparison.OrdinalIgnoreCase)
                        || prompt.Contains("autom", StringComparison.OrdinalIgnoreCase);
        var needsEvidence = rag.Bundle.Sources.Count > 0 || prompt.Contains("beleg", StringComparison.OrdinalIgnoreCase);
        var risk = insight.AdapterFamily == "ax2012" || prompt.Contains("send", StringComparison.OrdinalIgnoreCase)
                                         || prompt.Contains("post", StringComparison.OrdinalIgnoreCase)
                                         || prompt.Contains("book", StringComparison.OrdinalIgnoreCase);
        var route = risk
            ? "High-control route: compact context, cite evidence, require checkpoints, no mutation without approval."
            : needsVision
                ? "Vision route: include screenshots/UIA summary, then convert findings into guarded text plan."
                : needsPlan
                    ? "Planner route: structured JSON steps + mutation scan + approval boundary."
                    : needsEvidence
                        ? "Evidence route: local RAG first, answer with assumptions and source refs."
                        : "Fast companion route: short answer, optional local context, no execution.";

        var sb = new StringBuilder();
        sb.AppendLine("AI Model/Route Control");
        sb.AppendLine($"Configured provider/model: {settings.Provider} / {settings.Model}");
        sb.AppendLine($"Provider key: {(DotEnvStore.HasProviderKey(settings.Provider) ? "present" : "missing")}");
        sb.AppendLine($"Prompt chars: {prompt.Length}");
        sb.AppendLine($"RAG tier: {rag.Tier}");
        sb.AppendLine($"Foreground risk: {insight.AdapterFamily} / {insight.OperatorPosture}");
        sb.AppendLine();
        sb.AppendLine("Recommended route:");
        sb.AppendLine(route);
        sb.AppendLine();
        sb.AppendLine("Routing policy:");
        sb.AppendLine("- Use cheapest/fastest route for low-risk summaries.");
        sb.AppendLine("- Use evidence route when local knowledge is present.");
        sb.AppendLine("- Use planner route when output can become a flow.");
        sb.AppendLine("- Use high-control route for AX, mail, posting, deletion, approval, or write-like steps.");
        return sb.ToString().TrimEnd();
    }

    public static string BuildPromptQualityReport(NexusSettings settings, string? currentPrompt = null)
    {
        var prompt = currentPrompt?.Trim() ?? "";
        var score = 40;
        if (prompt.Length >= 30) score += 10;
        if (prompt.Contains("context", StringComparison.OrdinalIgnoreCase) || prompt.Contains("kontext", StringComparison.OrdinalIgnoreCase)) score += 10;
        if (prompt.Contains("risk", StringComparison.OrdinalIgnoreCase) || prompt.Contains("risiko", StringComparison.OrdinalIgnoreCase)) score += 10;
        if (prompt.Contains("evidence", StringComparison.OrdinalIgnoreCase) || prompt.Contains("beleg", StringComparison.OrdinalIgnoreCase) || prompt.Contains("quelle", StringComparison.OrdinalIgnoreCase)) score += 10;
        if (prompt.Contains("plan", StringComparison.OrdinalIgnoreCase) || prompt.Contains("steps", StringComparison.OrdinalIgnoreCase) || prompt.Contains("schritte", StringComparison.OrdinalIgnoreCase)) score += 10;
        if (prompt.Contains("do not", StringComparison.OrdinalIgnoreCase) || prompt.Contains("nicht", StringComparison.OrdinalIgnoreCase)) score += 10;
        score = Math.Clamp(score, 0, 100);

        var sb = new StringBuilder();
        sb.AppendLine("Prompt Quality Coach");
        sb.AppendLine($"Score: {score}/100");
        sb.AppendLine();
        sb.AppendLine("Recommended enterprise prompt shape:");
        sb.AppendLine("Task: what should be decided or produced.");
        sb.AppendLine("Context: active app, local RAG, relevant process/customer/vendor/order.");
        sb.AppendLine("Evidence: cite local sources and live UI facts.");
        sb.AppendLine("Risk: mark write/send/post/delete/approval risks.");
        sb.AppendLine("Boundary: do not execute irreversible actions without human approval.");
        sb.AppendLine("Output: answer, assumptions, plan, next safest step.");
        sb.AppendLine();
        sb.AppendLine("Upgraded prompt:");
        sb.AppendLine(UpgradePrompt(settings, prompt));
        return sb.ToString().TrimEnd();
    }

    public static string BuildPrivacyAndRedTeamReport(NexusSettings settings, string? currentPrompt = null)
    {
        var prompt = currentPrompt?.Trim() ?? "";
        var hasSecretLike = prompt.Contains("password", StringComparison.OrdinalIgnoreCase)
                            || prompt.Contains("secret", StringComparison.OrdinalIgnoreCase)
                            || prompt.Contains("token", StringComparison.OrdinalIgnoreCase)
                            || prompt.Contains("api_key", StringComparison.OrdinalIgnoreCase);
        var sb = new StringBuilder();
        sb.AppendLine("AI Privacy + Red-Team Gate");
        sb.AppendLine($"Secret-like prompt content: {(hasSecretLike ? "detected" : "not detected")}");
        sb.AppendLine($"Local knowledge enabled: {settings.UseLocalKnowledge}");
        sb.AppendLine($"Conversation memory: {settings.ConversationMemoryEnabled}");
        sb.AppendLine($"Never auto-send: {settings.Safety.NeverAutoSend}");
        sb.AppendLine($"Never auto-post/book: {settings.Safety.NeverAutoPostBook}");
        sb.AppendLine($"Panic stop: {settings.Safety.PanicStopEnabled}");
        sb.AppendLine();
        sb.AppendLine("Red-team checks before action:");
        sb.AppendLine("- Is the request asking to bypass approval, policy, credentials, or audit?");
        sb.AppendLine("- Does the prompt contain secrets or personal data that should not leave the machine?");
        sb.AppendLine("- Is the model asked to send, post, book, delete, approve, or overwrite?");
        sb.AppendLine("- Is there enough local evidence to justify the answer?");
        sb.AppendLine("- Is a human checkpoint required before execution?");
        sb.AppendLine();
        sb.AppendLine("USP:");
        sb.AppendLine("Carolus Nexus can present AI safety as an explicit operating mode, not an invisible backend promise.");
        return sb.ToString().TrimEnd();
    }

    public static string BuildAgentOpsRunbook(NexusSettings settings, string? currentPrompt = null)
    {
        var sb = new StringBuilder();
        sb.AppendLine("AI AgentOps Runbook");
        sb.AppendLine();
        sb.AppendLine("Daily operator flow:");
        sb.AppendLine("1. Start in Watch or Companion mode.");
        sb.AppendLine("2. Use Evidence Mode before important answers.");
        sb.AppendLine("3. Generate AI Opportunity Flow for repeatable work.");
        sb.AppendLine("4. Run Safe Mutation Scan before execution.");
        sb.AppendLine("5. Export Governance Proof for pilot/audit records.");
        sb.AppendLine();
        sb.AppendLine("Admin flow:");
        sb.AppendLine("1. Add/update knowledge documents.");
        sb.AppendLine("2. Rebuild index/FTS/embeddings.");
        sb.AppendLine("3. Publish one low-risk demo flow.");
        sb.AppendLine("4. Review Pilot Scorecard and Competitive Pack.");
        sb.AppendLine("5. Keep provider keys in .env, never in recipe JSON.");
        sb.AppendLine();
        sb.AppendLine("Incident flow:");
        sb.AppendLine("1. Panic stop.");
        sb.AppendLine("2. Export runtime diagnostics.");
        sb.AppendLine("3. Export governance proof.");
        sb.AppendLine("4. Inspect step audit, action history, and resume state.");
        sb.AppendLine("5. Lower autonomy level or disable write-like routes.");
        return sb.ToString().TrimEnd();
    }

    public static string ExportAgentOpsPack(NexusSettings settings, string? currentPrompt = null)
    {
        Directory.CreateDirectory(AppPaths.DataDir);
        var path = Path.Combine(AppPaths.DataDir, $"ai-agentops-pack-{DateTime.Now:yyyyMMdd-HHmmss}.md");
        var sb = new StringBuilder();
        sb.AppendLine("# Carolus Nexus AI AgentOps Pack");
        sb.AppendLine();
        sb.AppendLine($"Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        sb.AppendLine();
        sb.AppendLine("## Model/Route Control");
        sb.AppendLine(BuildModelRouterReport(settings, currentPrompt));
        sb.AppendLine();
        sb.AppendLine("## Prompt Quality");
        sb.AppendLine(BuildPromptQualityReport(settings, currentPrompt));
        sb.AppendLine();
        sb.AppendLine("## Privacy + Red-Team");
        sb.AppendLine(BuildPrivacyAndRedTeamReport(settings, currentPrompt));
        sb.AppendLine();
        sb.AppendLine("## Runbook");
        sb.AppendLine(BuildAgentOpsRunbook(settings, currentPrompt));
        File.WriteAllText(path, sb.ToString());
        return path;
    }

    private static string UpgradePrompt(NexusSettings settings, string prompt)
    {
        var baseTask = string.IsNullOrWhiteSpace(prompt) ? "Handle the current business task" : prompt;
        return "Task: " + baseTask + "\n"
               + "Use the active Windows context and local knowledge when available. "
               + "Return Evidence, Assumptions, Risk, Approval Boundary, and Safest Next Step. "
               + "Do not send, post, book, approve, delete, overwrite, or execute irreversible actions without explicit human approval.";
    }
}
