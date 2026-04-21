using CarolusNexus;
using CarolusNexus.Models;
using CarolusNexus.Services;

namespace CarolusNexus_WinUI;

internal sealed class NextBestAction
{
    public string Title { get; init; } = "Next best action";
    public string Message { get; init; } = "";
    public string Context { get; init; } = "";
    public string PrimaryLabel { get; init; } = "";
    public string SecondaryLabel { get; init; } = "";
    public string Intent { get; init; } = "";
    public string Severity { get; init; } = "info";
    public bool RequiresApproval { get; init; }
}

internal static class NextBestActionService
{
    public static NextBestAction Build(NexusSettings settings, string? liveContextLine)
    {
        var insight = OperatorInsightService.BuildSnapshot(settings);

        if (!DotEnvStore.HasProviderKey(settings.Provider))
        {
            return new NextBestAction
            {
                Message = "Provider key missing. Set up the AI route before running an operator plan.",
                Context = $"{settings.Provider} / {settings.Mode}",
                PrimaryLabel = "Open setup",
                SecondaryLabel = "Smoke test",
                Intent = "setup.provider",
                Severity = "blocked"
            };
        }

        if (insight.AdapterFamily is "mail" or "outlook")
        {
            return new NextBestAction
            {
                Message = "Mail context detected. Safe next step: summarize the message and draft a reply without sending.",
                Context = $"{insight.ProcessName} · {insight.AdapterFamily}",
                PrimaryLabel = "Use as prompt",
                SecondaryLabel = "Build plan",
                Intent = "ask.mail_summary",
                Severity = "success"
            };
        }

        if (insight.AdapterFamily == "ax2012")
        {
            return new NextBestAction
            {
                Message = "AX context detected. Safe next step: inspect context and prepare a guarded plan.",
                Context = $"{insight.ProcessName} · AX · approval required",
                PrimaryLabel = "Inspect context",
                SecondaryLabel = "Build guarded plan",
                Intent = "live.ax_context",
                Severity = "warning",
                RequiresApproval = true
            };
        }

        var watch = WatchSessionService.LoadOrEmpty();
        if (watch.Entries.Count >= 3)
        {
            return new NextBestAction
            {
                Message = "Recent watch activity looks repeatable. Safe next step: turn it into a draft operator flow.",
                Context = $"{watch.Entries.Count} watch entries · no automatic execution",
                PrimaryLabel = "Promote from watch",
                SecondaryLabel = "Show suggestion",
                Intent = "rituals.promote_watch",
                Severity = "success"
            };
        }

        if (settings.UseLocalKnowledge)
        {
            return new NextBestAction
            {
                Message = "Local knowledge is active. Safe next step: answer with project knowledge before planning action.",
                Context = $"{settings.Provider} · local knowledge · {settings.Mode}",
                PrimaryLabel = "Ask with knowledge",
                SecondaryLabel = "Open knowledge",
                Intent = "ask.knowledge",
                Severity = "info"
            };
        }

        return new NextBestAction
        {
            Message = insight.SafeNextAction,
            Context = $"{insight.ProcessName} · {insight.AdapterFamily}",
            PrimaryLabel = "Use as prompt",
            SecondaryLabel = "Open live context",
            Intent = "ask.generic",
            Severity = "info"
        };
    }
}
