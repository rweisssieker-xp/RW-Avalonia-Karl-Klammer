using System.IO;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Interactivity;
using CarolusNexus;

namespace CarolusNexus.Views;

public partial class KnowledgeTab : UserControl
{
    public KnowledgeTab()
    {
        InitializeComponent();
        BtnSearch.Click += (_, _) => NexusShell.LogStub("knowledge search");
        BtnImport.Click += (_, _) => NexusShell.LogStub("import files");
        BtnRemove.Click += (_, _) => NexusShell.LogStub("remove doc");
        BtnReindex.Click += (_, _) => { RefreshList(); NexusShell.LogStub("reindex knowledge"); };
        BtnSuggestRitual.Click += (_, _) => NexusShell.LogStub("suggest ritual from doc");
        DocList.SelectionChanged += OnSel;
        RefreshList();
    }

    public void RefreshList()
    {
        Directory.CreateDirectory(AppPaths.KnowledgeDir);
        var files = Directory.GetFiles(AppPaths.KnowledgeDir, "*.*", SearchOption.TopDirectoryOnly)
            .Select(Path.GetFileName).Where(f => f != null).ToArray();
        DocList.ItemsSource = files;
    }

    private void OnSel(object? s, SelectionChangedEventArgs e)
    {
        if (DocList.SelectedItem is not string name)
            return;
        var path = Path.Combine(AppPaths.KnowledgeDir, name);
        try
        {
            Preview.Text = File.ReadAllText(path);
        }
        catch
        {
            Preview.Text = "(Binär oder nicht lesbar — PDF/DOCX-Parser Stub.)";
        }
    }
}
