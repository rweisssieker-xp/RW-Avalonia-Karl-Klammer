using CarolusNexus.Models;
using CarolusNexus.Services;
using Xunit;

namespace CarolusNexus.Core.Tests;

public sealed class RecipeStepGuardAndQualityTests
{
    [Fact]
    public void RecipeStepGuard_empty_guards_pass()
    {
        var step = new RecipeStep { ActionArgument = "x" };
        Assert.True(RecipeStepGuardEvaluator.TryPassGuards(step, out _));
    }

    [Fact]
    public void RitualQualityGate_blocks_script_channel_without_flag()
    {
        var recipe = new AutomationRecipe
        {
            Name = "t",
            Steps =
            [
                new RecipeStep
                {
                    ActionType = "token",
                    ActionArgument = "powershell: Get-Date",
                    Channel = "script"
                }
            ]
        };
        var settings = new NexusSettings();
        var r = RitualQualityGate.Validate(recipe, settings);
        Assert.False(r.Ok);
    }
}
