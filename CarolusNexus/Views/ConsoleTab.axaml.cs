using Avalonia.Controls;
using Avalonia.Interactivity;
using CarolusNexus;

namespace CarolusNexus.Views;

public partial class ConsoleTab : UserControl
{
    public ConsoleTab()
    {
        InitializeComponent();
        AgentBox.ItemsSource = new[] { "codex", "claude code", "openclaw" };
        AgentBox.SelectedIndex = 0;
        BtnRunAgent.Click += (_, _) =>
        {
            NexusShell.LogStub($"run selected agent ({AgentBox.SelectedItem})");
            OutputPathHint.Text =
                $"Log-Ziel (Stub): {System.IO.Path.Combine(AppPaths.CodexOutputDir, "karl-klammer-stub.txt")}";
            AgentOutput.Text =
                $"Stub-Ausgabe für „{AgentPrompt.Text?.Trim()}“\r\n— CLI-Prozess nicht gestartet.";
        };
    }
}
