using System.Linq;
using Avalonia.Controls;
using CarolusNexus.Models;
using CarolusNexus.Services;

namespace CarolusNexus.Views;

public partial class SetupTab : UserControl
{
    public SetupTab()
    {
        InitializeComponent();
        ProviderBox.ItemsSource = new[] { "anthropic", "openai", "openai-compatible" };
        ModeBox.ItemsSource = new[] { "companion", "agent", "automation", "watch" };
        SafetyProfile.ItemsSource = new[] { "strict", "balanced", "power-user" };
    }

    public void Apply(NexusSettings s)
    {
        ProviderBox.SelectedItem = s.Provider;
        ModeBox.SelectedItem = s.Mode;
        ModelBox.Text = s.Model;
        SpeakResponses.IsChecked = s.SpeakResponses;
        UseLocalKnowledge.IsChecked = s.UseLocalKnowledge;
        SuggestAutomations.IsChecked = s.SuggestAutomations;
        SafetyProfile.SelectedItem = s.Safety.Profile;
        NeverAutoSend.IsChecked = s.Safety.NeverAutoSend;
        NeverAutoPost.IsChecked = s.Safety.NeverAutoPostBook;
        PanicStop.IsChecked = s.Safety.PanicStopEnabled;
        DenylistBox.Text = s.Safety.Denylist;
    }

    public NexusSettings Gather()
    {
        return new NexusSettings
        {
            Provider = ProviderBox.SelectedItem?.ToString() ?? "anthropic",
            Mode = ModeBox.SelectedItem?.ToString() ?? "companion",
            Model = ModelBox.Text?.Trim() ?? "",
            SpeakResponses = SpeakResponses.IsChecked == true,
            UseLocalKnowledge = UseLocalKnowledge.IsChecked == true,
            SuggestAutomations = SuggestAutomations.IsChecked == true,
            Safety = new SafetySettings
            {
                Profile = SafetyProfile.SelectedItem?.ToString() ?? "balanced",
                NeverAutoSend = NeverAutoSend.IsChecked == true,
                NeverAutoPostBook = NeverAutoPost.IsChecked == true,
                PanicStopEnabled = PanicStop.IsChecked == true,
                Denylist = DenylistBox.Text ?? "",
            },
        };
    }

    public void RefreshEnvSummary()
    {
        EnvPathHint.Text = AppPaths.EnvFile;
        var keys = DotEnvSummary.ListKeys(AppPaths.EnvFile);
        EnvSummary.Text = keys.Count == 0
            ? "(keine .env oder leer — Vorlage: windows\\.env.example)"
            : string.Join("\r\n", keys.Select(k => k + "=***"));
    }
}
