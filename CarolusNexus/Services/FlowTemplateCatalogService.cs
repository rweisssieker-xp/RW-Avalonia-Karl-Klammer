using System;
using System.Collections.Generic;
using CarolusNexus.Models;

namespace CarolusNexus.Services;

public static class FlowTemplateCatalogService
{
    public static List<AutomationRecipe> BuildDefaultTemplates()
    {
        return
        [
            new AutomationRecipe
            {
                Name = "AX Read Context",
                Description = "Read-only AX foreground context and form summary.",
                Category = "AX",
                RiskLevel = "low",
                ApprovalMode = "manual",
                AdapterAffinity = "ax2012",
                ConfidenceSource = "Built-in template",
                Steps =
                [
                    new RecipeStep { ActionType = "token", ActionArgument = "ax.read_context", WaitMs = 250 },
                    new RecipeStep { ActionType = "token", ActionArgument = "ax.form_summary", WaitMs = 250, Checkpoint = true }
                ]
            },
            new AutomationRecipe
            {
                Name = "Browser Open URL",
                Description = "Open a guarded browser URL.",
                Category = "Browser",
                RiskLevel = "low",
                ApprovalMode = "manual",
                AdapterAffinity = "browser",
                ConfidenceSource = "Built-in template",
                Steps =
                [
                    new RecipeStep { ActionType = "token", ActionArgument = "browser.open:https://example.com", WaitMs = 500, Checkpoint = true }
                ]
            },
            new AutomationRecipe
            {
                Name = "Explorer Open Path",
                Description = "Open a local path in Explorer.",
                Category = "Explorer",
                RiskLevel = "low",
                ApprovalMode = "manual",
                AdapterAffinity = "explorer",
                ConfidenceSource = "Built-in template",
                Steps =
                [
                    new RecipeStep { ActionType = "token", ActionArgument = "explorer.open_path:C:\\tmp", WaitMs = 500, Checkpoint = true }
                ]
            }
        ];
    }

    public static int EnsureDefaultTemplates()
    {
        var all = RitualRecipeStore.LoadAll();
        var added = 0;
        foreach (var t in BuildDefaultTemplates())
        {
            if (all.Exists(r => string.Equals(r.Name, t.Name, StringComparison.OrdinalIgnoreCase)))
                continue;
            RitualRecipeStore.AppendRecipe(t);
            added++;
        }
        return added;
    }
}
