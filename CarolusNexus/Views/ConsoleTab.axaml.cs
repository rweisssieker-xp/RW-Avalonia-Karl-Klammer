using System;
using System.Threading.Tasks;
using Avalonia.Controls;
using CarolusNexus.Services;

namespace CarolusNexus.Views;

public partial class ConsoleTab : UserControl
{
    public ConsoleTab()
    {
        InitializeComponent();
        AgentBox.ItemsSource = new[] { "codex", "claude code", "openclaw" };
        AgentBox.SelectedIndex = 0;
        BtnRunAgent.Click += async (_, _) => await RunAsync();
    }

    private async Task RunAsync()
    {
        var agent = AgentBox.SelectedItem?.ToString() ?? "codex";
        var prompt = AgentPrompt.Text?.Trim();
        if (string.IsNullOrEmpty(prompt))
        {
            AgentOutput.Text = "Please enter a prompt.";
            return;
        }

        BtnRunAgent.IsEnabled = false;
        try
        {
            NexusShell.Log($"CLI: starting “{agent}” …");
            var (logPath, excerpt) = await CliAgentRunner.RunAsync(agent, prompt).ConfigureAwait(true);
            OutputPathHint.Text = logPath;
            AgentOutput.Text = excerpt;
            NexusShell.Log($"CLI: done → {logPath}");
        }
        catch (Exception ex)
        {
            AgentOutput.Text = "Error: " + ex;
            NexusShell.Log("CLI error: " + ex.Message);
        }
        finally
        {
            BtnRunAgent.IsEnabled = true;
        }
    }
}
