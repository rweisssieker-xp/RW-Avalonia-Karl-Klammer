using Avalonia.Controls;

namespace CarolusNexus.Views;

public partial class DashboardTab : UserControl
{
    public DashboardTab()
    {
        InitializeComponent();
    }

    public void RefreshSummaries(string env, string know, string live, string proactive, string gov, string rituals, string watch)
    {
        CardEnv.Text = env;
        CardKnow.Text = know;
        CardLive.Text = live;
        CardProactive.Text = proactive;
        CardGov.Text = gov;
        CardRituals.Text = rituals;
        CardWatch.Text = watch;
    }
}
