using System.IO;
using Avalonia.Controls;
using Avalonia.Interactivity;
using CarolusNexus;

namespace CarolusNexus.Views;

public partial class HistoryTab : UserControl
{
    public HistoryTab()
    {
        InitializeComponent();
        BtnCreateRitual.Click += (_, _) => NexusShell.LogStub("create ritual from selection");
        Loaded += (_, _) => Refresh();
        HistList.SelectionChanged += (_, _) =>
        {
            HistDetail.Text = HistList.SelectedItem?.ToString() ?? "";
        };
    }

    public void Refresh()
    {
        if (!File.Exists(AppPaths.ActionHistory))
        {
            HistList.ItemsSource = new[] { "(noch keine action-history.json)" };
            return;
        }

        var text = File.ReadAllText(AppPaths.ActionHistory);
        HistList.ItemsSource = new[] { "action-history.json (gesamt)" };
        HistDetail.Text = text;
    }
}
