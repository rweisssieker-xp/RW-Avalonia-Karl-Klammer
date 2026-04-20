using Avalonia.Controls;
using CarolusNexus.Experiments;

namespace CarolusNexus.Views;

public partial class ExperimentsTab : UserControl
{
    public ExperimentsTab()
    {
        InitializeComponent();
        TagLine.Text = TierCExperiments.Tag;
    }
}
