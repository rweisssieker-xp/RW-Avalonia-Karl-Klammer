using CarolusNexus.Models;

namespace CarolusNexus.Services;

/// <summary>Wählt UI-, Script- oder API-Ausführung für einen Ritual-Schritt.</summary>
public static class AutomationToolRouter
{
    public static string Execute(RecipeStep step, NexusSettings settings, string? forceChannel = null)
    {
        var ch = (forceChannel ?? step.Channel ?? "ui").Trim().ToLowerInvariant();
        return ch switch
        {
            "script" => ScriptHookRunner.TryRun(step, settings),
            "api" => ApiHookRunner.TryRun(step, settings),
            _ => Win32AutomationExecutor.ExecuteWithCvFallback(step, settings)
        };
    }
}
