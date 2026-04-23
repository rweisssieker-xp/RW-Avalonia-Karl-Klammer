using System;
using System.IO;
using System.Text;
using CarolusNexus.Models;

namespace CarolusNexus.Services;

public static class AiDemoOrchestratorService
{
    public static string BuildDemoRunbook(NexusSettings settings, string? currentPrompt = null)
    {
        var sb = new StringBuilder();
        sb.AppendLine("AI Demo Orchestrator");
        sb.AppendLine($"Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        sb.AppendLine();
        sb.AppendLine("15-minute executive demo path");
        sb.AppendLine("00:00 - 01:30 | Open with operator reality: foreground context, AX affinity, and safety profile.");
        sb.AppendLine("01:30 - 03:30 | Show Ask with evidence mode: answer with local knowledge, source confidence, and assumptions.");
        sb.AppendLine("03:30 - 05:30 | Convert context to flow: prove this is not just chat, but an operator automation surface.");
        sb.AppendLine("05:30 - 07:30 | Run ROI scoring: quantify repeated work, flow inventory, and risk-adjusted annual value.");
        sb.AppendLine("07:30 - 09:30 | Open AI Evaluation Lab: show hallucination guard, prompt benchmark, and acceptance gates.");
        sb.AppendLine("09:30 - 11:30 | Export governance proof: dry-run, audit trail, resume state, and autonomy boundaries.");
        sb.AppendLine("11:30 - 13:30 | Show competitive battlecard: explain why WinUI + AX + governed AI is differentiated.");
        sb.AppendLine("13:30 - 15:00 | Export USP Studio pack: leave behind buyer-ready artifacts.");
        sb.AppendLine();
        sb.AppendLine("Talk track");
        sb.AppendLine("- Carolus Nexus is not another generic assistant; it turns real operator context into governed, auditable automation.");
        sb.AppendLine("- The proof is not a model claim. The proof is the chain: context -> flow -> guard -> eval -> ROI -> export.");
        sb.AppendLine("- The pilot does not need a perfect ERP integration on day one. It needs evidence that repeated work can be observed, scored, and safely converted.");
        sb.AppendLine();
        sb.AppendLine("Demo artifacts to export");
        sb.AppendLine("- AI ROI Opportunity Pack");
        sb.AppendLine("- AI Evaluation Pack");
        sb.AppendLine("- Governance Proof Pack");
        sb.AppendLine("- USP Studio Pack");
        sb.AppendLine("- Competitive Pack");
        sb.AppendLine();
        sb.AppendLine("Operator proof checklist");
        sb.AppendLine("- One real foreground screen captured.");
        sb.AppendLine("- One flow generated or selected.");
        sb.AppendLine("- One safety gate visible.");
        sb.AppendLine("- One eval/guardrail report generated.");
        sb.AppendLine("- One ROI estimate generated.");
        sb.AppendLine("- One export folder created for the buyer.");
        return sb.ToString().TrimEnd();
    }

    public static string BuildClickPath(NexusSettings settings)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Recommended next-click path");
        sb.AppendLine("1. Live Context -> Refresh deep UIA/context.");
        sb.AppendLine("2. Live Context -> Context -> Flow.");
        sb.AppendLine("3. Ask -> Evidence mode.");
        sb.AppendLine("4. Diagnostics -> AI ROI.");
        sb.AppendLine("5. Diagnostics -> AI Eval Lab.");
        sb.AppendLine("6. Diagnostics -> USP Studio.");
        sb.AppendLine("7. Diagnostics -> USP Studio pack.");
        sb.AppendLine();
        sb.AppendLine("Fallback if no real AX window is open");
        sb.AppendLine("1. Ask -> AI demo flow.");
        sb.AppendLine("2. Ask -> Pilot scorecard.");
        sb.AppendLine("3. Diagnostics -> AI ROI pack.");
        sb.AppendLine("4. Diagnostics -> USP Studio pack.");
        return sb.ToString().TrimEnd();
    }

    public static string BuildBuyerFollowUp(NexusSettings settings, string? currentPrompt = null)
    {
        var provider = string.IsNullOrWhiteSpace(settings.Provider) ? "configured LLM provider" : settings.Provider;
        var sb = new StringBuilder();
        sb.AppendLine("Buyer follow-up email");
        sb.AppendLine();
        sb.AppendLine("Subject: Carolus Nexus pilot proof pack and next validation step");
        sb.AppendLine();
        sb.AppendLine("Hi,");
        sb.AppendLine();
        sb.AppendLine("attached is the pilot proof pack from today's Carolus Nexus walkthrough. The important part is the evidence chain: real operator context, governed AI response, flow candidate, evaluation guardrails, ROI estimate, and exportable audit artifacts.");
        sb.AppendLine();
        sb.AppendLine("Recommended next step: select one repeated AX/operator process and let us run a 60-90 minute pilot capture. The goal is not broad automation yet; the goal is one measurable before/after proof with safety gates and buyer-visible reporting.");
        sb.AppendLine();
        sb.AppendLine($"Current AI route: {provider}. Safety profile: {settings.Safety.Profile}.");
        sb.AppendLine();
        sb.AppendLine("Best regards");
        return sb.ToString().TrimEnd();
    }

    public static string ExportDemoPack(NexusSettings settings, string? currentPrompt = null)
    {
        Directory.CreateDirectory(AppPaths.DataDir);
        var stamp = DateTime.Now.ToString("yyyyMMdd-HHmmss");
        var dir = Path.Combine(AppPaths.DataDir, $"ai-demo-orchestrator-{stamp}");
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, "00-demo-runbook.md"), BuildDemoRunbook(settings, currentPrompt) + Environment.NewLine);
        File.WriteAllText(Path.Combine(dir, "01-click-path.md"), BuildClickPath(settings) + Environment.NewLine);
        File.WriteAllText(Path.Combine(dir, "02-buyer-follow-up.md"), BuildBuyerFollowUp(settings, currentPrompt) + Environment.NewLine);
        File.WriteAllText(Path.Combine(dir, "03-ai-roi.md"), AiRoiOpportunityService.BuildRoiReport(settings, currentPrompt) + Environment.NewLine);
        File.WriteAllText(Path.Combine(dir, "index.md"),
            "# AI Demo Orchestrator Pack\n\n"
            + $"- Generated: {stamp}\n"
            + "- [Demo Runbook](00-demo-runbook.md)\n"
            + "- [Click Path](01-click-path.md)\n"
            + "- [Buyer Follow-up](02-buyer-follow-up.md)\n"
            + "- [AI ROI](03-ai-roi.md)\n");
        return dir;
    }
}
