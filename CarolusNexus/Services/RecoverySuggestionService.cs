using System;
using System.Text;
using CarolusNexus.Models;

namespace CarolusNexus.Services;

public static class RecoverySuggestionService
{
    public static string BuildSuggestion(RecipeStep step, string result, NexusSettings? settings = null)
    {
        var token = step.ActionArgument ?? "";
        var lower = result.ToLowerInvariant();
        var sb = new StringBuilder();
        sb.AppendLine("Recovery");
        if (lower.StartsWith("[blocked]"))
        {
            sb.AppendLine("Class: policy/guard block");
            sb.AppendLine("Next safe move: inspect readiness, allowed app families and safety profile.");
            if (!string.Equals(settings?.Safety.Profile, "power-user", StringComparison.OrdinalIgnoreCase))
                sb.AppendLine("Suggestion: switch to power-user only if this run really needs desktop execution.");
        }
        else if (lower.StartsWith("[skip]"))
        {
            sb.AppendLine("Class: unsupported or unavailable capability");
            sb.AppendLine("Next safe move: replace the token with a known executable adapter.");
            sb.AppendLine("Suggestion: prefer uia.*, app|..., browser.open:, api.get:, api.post:, ax.odata.get:.");
        }
        else if (lower.StartsWith("[err]"))
        {
            sb.AppendLine("Class: runtime failure");
            sb.AppendLine("Next safe move: retry a read-only context step before repeating the failing write.");
            if (token.StartsWith("uia.", StringComparison.OrdinalIgnoreCase) || token.StartsWith("ax.", StringComparison.OrdinalIgnoreCase))
                sb.AppendLine("Suggestion: run ax.read_context or uia.invoke/name-based variant and compare the active control tree.");
            else if (token.Contains("[ACTION:", StringComparison.OrdinalIgnoreCase))
                sb.AppendLine("Suggestion: convert generic ACTION to UIA/app-specific token or add a CV fallback template.");
            else
                sb.AppendLine("Suggestion: inspect token syntax and target app focus before retry.");
        }
        else
        {
            sb.AppendLine("Class: no recovery needed");
            sb.AppendLine("Result was not a failure class.");
        }

        return sb.ToString().TrimEnd();
    }
}
