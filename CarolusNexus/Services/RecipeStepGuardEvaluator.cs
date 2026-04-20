using System;
using CarolusNexus.Models;

namespace CarolusNexus.Services;

/// <summary>Prüft optionale Guard-Felder eines <see cref="RecipeStep"/> gegen das Vordergrundfenster.</summary>
public static class RecipeStepGuardEvaluator
{
    public static bool TryPassGuards(RecipeStep step, out string detail)
    {
        detail = "";
        var procNeed = (step.GuardProcessContains ?? "").Trim();
        var titleNeed = (step.GuardWindowTitleContains ?? "").Trim();
        if (procNeed.Length == 0 && titleNeed.Length == 0)
            return true;

        if (!OperatingSystem.IsWindows())
        {
            detail = "not Windows";
            return false;
        }

        var d = ForegroundWindowInfo.TryReadDetail();
        if (d == null)
        {
            detail = "no foreground window";
            return false;
        }

        var proc = (d.Value.ProcessName ?? "").ToLowerInvariant();
        var title = (d.Value.Title ?? "").ToLowerInvariant();
        if (procNeed.Length > 0 && !proc.Contains(procNeed.ToLowerInvariant()))
        {
            detail = $"process '{proc}' does not contain '{procNeed}'";
            return false;
        }

        if (titleNeed.Length > 0 && !title.Contains(titleNeed.ToLowerInvariant()))
        {
            detail = $"title does not contain '{titleNeed}'";
            return false;
        }

        return true;
    }
}
