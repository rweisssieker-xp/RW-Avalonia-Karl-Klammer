using System;
using System.IO;
using System.Linq;
using System.Text;
using CarolusNexus.Models;

namespace CarolusNexus.Services;

public static class RuntimeDiagnosticsService
{
    public static string BuildReport(NexusSettings settings)
    {
        var recipes = SafeLoadRecipes();
        var sb = new StringBuilder();
        sb.AppendLine("Carolus Nexus diagnostics");
        sb.AppendLine("Generated: " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
        sb.AppendLine();
        sb.AppendLine("[Environment]");
        sb.AppendLine(AppBuildInfo.Summary);
        sb.AppendLine("RepoRoot: " + AppPaths.RepoRoot);
        sb.AppendLine("DataDir: " + AppPaths.DataDir);
        sb.AppendLine(".env: " + (File.Exists(AppPaths.EnvFile) ? "present" : "missing"));
        sb.AppendLine("Provider: " + settings.Provider);
        sb.AppendLine("Provider key: " + (DotEnvStore.HasProviderKey(settings.Provider) ? "present" : "missing"));
        sb.AppendLine("Mode: " + settings.Mode);
        sb.AppendLine("Safety profile: " + settings.Safety.Profile);
        sb.AppendLine();
        sb.AppendLine("[Knowledge]");
        sb.AppendLine("Knowledge files: " + CountFiles(AppPaths.KnowledgeDir));
        sb.AppendLine("Index: " + Exists(AppPaths.KnowledgeIndex));
        sb.AppendLine("Chunks: " + Exists(AppPaths.KnowledgeChunks));
        sb.AppendLine("FTS DB: " + Exists(AppPaths.KnowledgeFtsDb));
        sb.AppendLine("Embeddings: " + Exists(AppPaths.KnowledgeEmbeddings));
        sb.AppendLine();
        sb.AppendLine("[Operator Flows]");
        sb.AppendLine("Recipes: " + recipes.Count);
        sb.AppendLine("Published: " + recipes.Count(r => string.Equals(r.PublicationState, "published", StringComparison.OrdinalIgnoreCase)));
        sb.AppendLine("Archived: " + recipes.Count(r => r.Archived));
        sb.AppendLine("AX affinity: " + recipes.Count(r => string.Equals(r.AdapterAffinity, "ax2012", StringComparison.OrdinalIgnoreCase)));
        sb.AppendLine("Pending jobs: " + RitualJobQueueStore.GetPendingCount());
        sb.AppendLine("Resume state: " + Exists(FlowResumeStore.StatePath));
        sb.AppendLine();
        sb.AppendLine("[USP Radar]");
        sb.AppendLine(OperatorUspPackService.BuildUspRadar(settings));
        sb.AppendLine();
        sb.AppendLine("[AI USP Prompt Pack]");
        sb.AppendLine(AiUspCommandService.BuildPromptPack(settings, ""));
        sb.AppendLine();
        sb.AppendLine("[AI/RAG Gap Report]");
        sb.AppendLine(AiUspCommandService.BuildRagGapReport(settings, ""));
        sb.AppendLine();
        sb.AppendLine("[AX]");
        sb.AppendLine("Enabled: " + settings.AxIntegrationEnabled);
        sb.AppendLine("Backend: " + Show(settings.AxIntegrationBackend));
        sb.AppendLine("OData URL: " + Show(settings.AxODataBaseUrl));
        sb.AppendLine("AIF URL: " + Show(settings.AxAifServiceBaseUrl));
        sb.AppendLine("DataAreaId: " + Show(settings.AxDataAreaId));
        sb.AppendLine("Tenant label: " + Show(settings.AxTestTenantLabel));
        sb.AppendLine();
        sb.AppendLine("[Audit]");
        sb.AppendLine("Action history: " + Exists(AppPaths.ActionHistory));
        sb.AppendLine("Step audit: " + Exists(AppPaths.RitualStepAudit));
        sb.AppendLine("Watch sessions: " + Exists(AppPaths.WatchSessions));
        sb.AppendLine();
        sb.AppendLine("[Recent log]");
        sb.AppendLine(NexusShell.FormatRecentLogForDashboard());
        return sb.ToString().TrimEnd();
    }

    public static string SaveReport(NexusSettings settings)
    {
        Directory.CreateDirectory(AppPaths.DataDir);
        var path = Path.Combine(AppPaths.DataDir, $"diagnostics-full-{DateTime.Now:yyyyMMdd-HHmmss}.txt");
        File.WriteAllText(path, BuildReport(settings));
        return path;
    }

    private static string Exists(string path) => File.Exists(path) ? "present" : "missing";

    private static string Show(string? value) => string.IsNullOrWhiteSpace(value) ? "(empty)" : value.Trim();

    private static int CountFiles(string dir)
    {
        try { return Directory.Exists(dir) ? Directory.GetFiles(dir).Length : 0; }
        catch { return 0; }
    }

    private static System.Collections.Generic.List<AutomationRecipe> SafeLoadRecipes()
    {
        try { return RitualRecipeStore.LoadAll(); }
        catch { return new System.Collections.Generic.List<AutomationRecipe>(); }
    }
}
