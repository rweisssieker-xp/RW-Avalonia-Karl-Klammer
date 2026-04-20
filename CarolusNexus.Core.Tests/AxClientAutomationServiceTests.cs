using CarolusNexus.Models;
using CarolusNexus.Services;
using Xunit;

namespace CarolusNexus.Core.Tests;

public sealed class AxClientAutomationServiceTests
{
    [Fact]
    public void TryExecute_returns_false_for_non_ax_token()
    {
        var s = new NexusSettings { Safety = { Profile = "power-user" } };
        var handled = AxClientAutomationService.TryExecute("browser.open:https://example.com", s, out var msg);
        Assert.False(handled);
        Assert.Equal("", msg);
    }

    [Fact]
    public void TryExecute_skip_when_ax_disabled()
    {
        var s = new NexusSettings
        {
            AxIntegrationEnabled = false,
            Safety = { Profile = "power-user" }
        };
        var handled = AxClientAutomationService.TryExecute("ax.read_context", s, out var msg);
        Assert.True(handled);
        Assert.Contains("disabled", msg, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void TryExecute_integration_status_lists_backends()
    {
        var s = new NexusSettings
        {
            AxIntegrationEnabled = true,
            AxIntegrationBackend = "odata",
            AxODataBaseUrl = "https://example.invalid/AX/Data/",
            AxDataAreaId = "USMF",
            Safety = { Profile = "power-user" }
        };
        var handled = AxClientAutomationService.TryExecute("ax.integration.status", s, out var msg);
        Assert.True(handled);
        Assert.Contains("odata", msg, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("USMF", msg, StringComparison.OrdinalIgnoreCase);
    }
}
