using System.Collections.Generic;
using System.Linq;
using CarolusNexus.Models;

namespace CarolusNexus.Services;

/// <summary>Teach-Modus: sammelt Schritte bis „stop teach“ (Rituals-Tab, optional Live Context).</summary>
public static class RitualsTeachSession
{
    private static readonly List<RecipeStep> Buffer = new();
    public static bool IsActive { get; private set; }

    public static void Start()
    {
        Buffer.Clear();
        IsActive = true;
    }

    public static void Append(RecipeStep step)
    {
        if (!IsActive)
            return;
        Buffer.Add(new RecipeStep
        {
            ActionType = step.ActionType,
            ActionArgument = step.ActionArgument,
            WaitMs = step.WaitMs
        });
    }

    /// <summary>Beendet die Session und liefert eine Kopie der gesammelten Schritte.</summary>
    public static IReadOnlyList<RecipeStep> Stop()
    {
        IsActive = false;
        return Buffer.Select(s => new RecipeStep
        {
            ActionType = s.ActionType,
            ActionArgument = s.ActionArgument,
            WaitMs = s.WaitMs
        }).ToList();
    }

    public static int BufferedCount => Buffer.Count;
}
