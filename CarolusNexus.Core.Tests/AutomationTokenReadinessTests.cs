using CarolusNexus.Models;
using CarolusNexus.Services;
using Xunit;

namespace CarolusNexus.Core.Tests;

public sealed class AutomationTokenReadinessTests
{
    [Fact]
    public void Classify_known_action_hotkey_as_executable_when_power_user()
    {
        var settings = new NexusSettings();
        settings.Safety.Profile = "power-user";

        var result = AutomationTokenReadiness.Classify("[ACTION:hotkey|Ctrl+L]", settings);

        Assert.True(result.IsExecutable);
        Assert.Equal("ui", result.Channel);
        Assert.Equal("real", result.Mode);
    }

    [Fact]
    public void Classify_unknown_action_as_unsupported_not_simulated()
    {
        var settings = new NexusSettings();
        settings.Safety.Profile = "power-user";

        var result = AutomationTokenReadiness.Classify("[ACTION:book-flight|SEA]", settings);

        Assert.False(result.IsExecutable);
        Assert.Equal("unsupported", result.Mode);
    }

    [Fact]
    public void Classify_api_get_as_guarded_without_power_user()
    {
        var settings = new NexusSettings();
        settings.Safety.Profile = "balanced";

        var result = AutomationTokenReadiness.Classify("api.get:https://example.test", settings, channel: "api");

        Assert.False(result.IsExecutable);
        Assert.Equal("guarded", result.Mode);
    }
}
