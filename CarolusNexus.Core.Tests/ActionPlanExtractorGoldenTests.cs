using CarolusNexus.Services;
using Xunit;

namespace CarolusNexus.Core.Tests;

public sealed class ActionPlanExtractorGoldenTests
{
    [Fact]
    public void Extract_finds_bracket_action_hotkey()
    {
        var text = "Save with [ACTION:hotkey|Ctrl+S] then done.";
        var tokens = ActionPlanExtractor.Extract(text);
        Assert.Contains(tokens, t => t.Contains("ACTION:hotkey", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Extract_orders_by_position()
    {
        var text = "First ax.open:foo then [ACTION:type|hello]";
        var tokens = ActionPlanExtractor.Extract(text);
        Assert.True(tokens.Count >= 2);
        Assert.Contains(tokens, x => x.StartsWith("ax.", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void ToRecipeSteps_maps_tokens()
    {
        var raw = new[] { "[ACTION:open|notepad]" };
        var steps = ActionPlanExtractor.ToRecipeSteps(raw);
        Assert.Single(steps);
        Assert.Equal("token", steps[0].ActionType);
        Assert.Contains("ACTION:open", steps[0].ActionArgument, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void RitualQualityGate_blocks_empty_argument()
    {
        var recipe = new CarolusNexus.Models.AutomationRecipe
        {
            Name = "t",
            Steps =
            [
                new CarolusNexus.Models.RecipeStep { ActionType = "x", ActionArgument = "" }
            ]
        };
        var settings = new CarolusNexus.Models.NexusSettings();
        var r = RitualQualityGate.Validate(recipe, settings);
        Assert.False(r.Ok);
    }
}
