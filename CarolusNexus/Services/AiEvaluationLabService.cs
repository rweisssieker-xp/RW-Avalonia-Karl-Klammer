using System;
using System.IO;
using System.Linq;
using System.Text;
using CarolusNexus.Models;

namespace CarolusNexus.Services;

public static class AiEvaluationLabService
{
    public static string BuildEvalLabReport(NexusSettings settings, string? currentPrompt = null)
    {
        var prompt = currentPrompt?.Trim() ?? "";
        var rag = KnowledgeSnippetService.BuildAugmentationResult(prompt, 1800);
        var insight = OperatorInsightService.BuildSnapshot(settings);
        var evidenceScore = Math.Min(30, rag.Bundle.Sources.Count * 10);
        var contextScore = string.IsNullOrWhiteSpace(insight.ProcessName) ? 0 : 20;
        var safetyScore = settings.Safety.PanicStopEnabled && settings.Safety.NeverAutoSend && settings.Safety.NeverAutoPostBook ? 25 : 10;
        var promptScore = ScorePrompt(prompt);
        var total = Math.Clamp(evidenceScore + contextScore + safetyScore + promptScore, 0, 100);

        var sb = new StringBuilder();
        sb.AppendLine("AI Evaluation Lab");
        sb.AppendLine($"Eval score: {total}/100");
        sb.AppendLine($"Evidence score: {evidenceScore}/30");
        sb.AppendLine($"Live context score: {contextScore}/20");
        sb.AppendLine($"Safety score: {safetyScore}/25");
        sb.AppendLine($"Prompt score: {promptScore}/25");
        sb.AppendLine();
        sb.AppendLine("Rubric:");
        sb.AppendLine("- Evidence: local sources, RAG tier, citations, assumptions.");
        sb.AppendLine("- Context: active app, adapter family, UIA/screenshot when needed.");
        sb.AppendLine("- Safety: no hidden mutation, explicit approval boundary, panic stop.");
        sb.AppendLine("- Output: clear answer, next step, risk labels, reusable plan.");
        sb.AppendLine();
        sb.AppendLine("Recommended eval prompt:");
        sb.AppendLine("Evaluate the answer for evidence, assumptions, risk, actionability, and approval boundary. Return score 0-100 plus top 3 fixes.");
        return sb.ToString().TrimEnd();
    }

    public static string BuildHallucinationGuard(NexusSettings settings, string? currentPrompt = null)
    {
        var prompt = currentPrompt?.Trim() ?? "";
        var rag = KnowledgeSnippetService.BuildAugmentationResult(prompt, 2400);
        var insight = OperatorInsightService.BuildSnapshot(settings);
        var claimsAllowed = rag.Bundle.Sources.Count > 0 || !settings.UseLocalKnowledge
            ? "Evidence-backed answer allowed, but assumptions must be marked."
            : "No local evidence found. Answer should be framed as hypothesis or ask for source documents.";
        var sb = new StringBuilder();
        sb.AppendLine("Hallucination Guard");
        sb.AppendLine($"RAG tier: {rag.Tier}");
        sb.AppendLine($"Sources: {rag.Bundle.Sources.Count}");
        sb.AppendLine($"Foreground: {insight.ProcessName} / {insight.AdapterFamily}");
        sb.AppendLine();
        sb.AppendLine("Decision:");
        sb.AppendLine(claimsAllowed);
        sb.AppendLine();
        sb.AppendLine("Guardrails for the next AI answer:");
        sb.AppendLine("- Cite local sources or say evidence is missing.");
        sb.AppendLine("- Separate facts, assumptions, and recommended actions.");
        sb.AppendLine("- Do not invent AX/customer/vendor/order details.");
        sb.AppendLine("- Use current UI context only as observed evidence, not as database truth.");
        sb.AppendLine("- Require confirmation before write-like actions.");
        if (rag.Hints.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("RAG hints:");
            foreach (var h in rag.Hints)
                sb.AppendLine("- " + h);
        }
        return sb.ToString().TrimEnd();
    }

    public static string BuildPersonaNarratives(NexusSettings settings, string? currentPrompt = null)
    {
        var pilot = PilotReadinessUspService.BuildPilotScorecard(settings, currentPrompt).Split('\n').FirstOrDefault() ?? "Pilot scorecard available.";
        var sb = new StringBuilder();
        sb.AppendLine("AI Persona Narratives");
        sb.AppendLine();
        sb.AppendLine("CFO:");
        sb.AppendLine("Carolus reduces repeated backoffice effort while keeping approval gates and proof exports visible for audit.");
        sb.AppendLine();
        sb.AppendLine("CIO/IT:");
        sb.AppendLine("Carolus is a governed Windows AI shell: local data paths, .env secrets, RAG transparency, and controllable provider routing.");
        sb.AppendLine();
        sb.AppendLine("Compliance:");
        sb.AppendLine("Evidence Mode, Safe Mutation Scan, and Governance Proof make AI actions reviewable before and after execution.");
        sb.AppendLine();
        sb.AppendLine("Operations:");
        sb.AppendLine("Watch/process mining finds repeated desktop work and turns it into reviewable flow candidates.");
        sb.AppendLine();
        sb.AppendLine("AX power user:");
        sb.AppendLine("AX context is read first; write/post/book actions remain behind policy and human checkpoints.");
        sb.AppendLine();
        sb.AppendLine("Current pilot signal:");
        sb.AppendLine(pilot);
        return sb.ToString().TrimEnd();
    }

    public static string BuildPromptVariantBench(NexusSettings settings, string? currentPrompt = null)
    {
        var task = string.IsNullOrWhiteSpace(currentPrompt) ? "current foreground business task" : currentPrompt.Trim();
        var sb = new StringBuilder();
        sb.AppendLine("Prompt Variant Bench");
        sb.AppendLine();
        sb.AppendLine("Variant A - Evidence answer:");
        sb.AppendLine($"Use local evidence and live context to answer: {task}. Return Evidence, Assumptions, Answer, and Missing sources.");
        sb.AppendLine();
        sb.AppendLine("Variant B - Guarded plan:");
        sb.AppendLine($"Create a guarded plan for: {task}. Mark read/write steps, risk, approval boundary, and safe next action.");
        sb.AppendLine();
        sb.AppendLine("Variant C - Executive summary:");
        sb.AppendLine($"Summarize business value, risk, and pilot proof for: {task}. Keep it stakeholder-ready.");
        sb.AppendLine();
        sb.AppendLine("Variant D - AX-safe operator:");
        sb.AppendLine($"If AX is active, inspect context first for: {task}. Do not post/book/write. Require human approval for mutations.");
        sb.AppendLine();
        sb.AppendLine("Bench criteria:");
        sb.AppendLine("- Evidence strength");
        sb.AppendLine("- Risk clarity");
        sb.AppendLine("- Actionability");
        sb.AppendLine("- Approval boundary");
        sb.AppendLine("- Reusable flow potential");
        return sb.ToString().TrimEnd();
    }

    public static string ExportEvaluationPack(NexusSettings settings, string? currentPrompt = null)
    {
        Directory.CreateDirectory(AppPaths.DataDir);
        var path = Path.Combine(AppPaths.DataDir, $"ai-evaluation-lab-{DateTime.Now:yyyyMMdd-HHmmss}.md");
        var sb = new StringBuilder();
        sb.AppendLine("# Carolus Nexus AI Evaluation Lab");
        sb.AppendLine();
        sb.AppendLine($"Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        sb.AppendLine();
        sb.AppendLine("## Evaluation Lab");
        sb.AppendLine(BuildEvalLabReport(settings, currentPrompt));
        sb.AppendLine();
        sb.AppendLine("## Hallucination Guard");
        sb.AppendLine(BuildHallucinationGuard(settings, currentPrompt));
        sb.AppendLine();
        sb.AppendLine("## Persona Narratives");
        sb.AppendLine(BuildPersonaNarratives(settings, currentPrompt));
        sb.AppendLine();
        sb.AppendLine("## Prompt Variant Bench");
        sb.AppendLine(BuildPromptVariantBench(settings, currentPrompt));
        File.WriteAllText(path, sb.ToString());
        return path;
    }

    private static int ScorePrompt(string prompt)
    {
        if (string.IsNullOrWhiteSpace(prompt))
            return 5;
        var score = 5;
        if (prompt.Length > 40) score += 5;
        if (prompt.Contains("evidence", StringComparison.OrdinalIgnoreCase) || prompt.Contains("quelle", StringComparison.OrdinalIgnoreCase)) score += 5;
        if (prompt.Contains("risk", StringComparison.OrdinalIgnoreCase) || prompt.Contains("risiko", StringComparison.OrdinalIgnoreCase)) score += 5;
        if (prompt.Contains("plan", StringComparison.OrdinalIgnoreCase) || prompt.Contains("step", StringComparison.OrdinalIgnoreCase)) score += 5;
        return Math.Clamp(score, 0, 25);
    }
}
