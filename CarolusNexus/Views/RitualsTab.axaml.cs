using Avalonia.Controls;
using Avalonia.Interactivity;
using CarolusNexus;

namespace CarolusNexus.Views;

public partial class RitualsTab : UserControl
{
    public RitualsTab()
    {
        InitializeComponent();
        BtnSaveRitual.Click += (_, _) => NexusShell.LogStub("save ritual");
        BtnDeleteRitual.Click += (_, _) => NexusShell.LogStub("delete ritual");
        BtnClone.Click += (_, _) => NexusShell.LogStub("clone ritual");
        BtnArchive.Click += (_, _) => NexusShell.LogStub("archive ritual");
        BtnPublish.Click += (_, _) => NexusShell.LogStub("publish flow");
        BtnQueue.Click += (_, _) => NexusShell.LogStub("queue for run");
        BtnApproveJob.Click += (_, _) => NexusShell.LogStub("approve next job");
        BtnDryRun.Click += (_, _) => NexusShell.LogStub("dry run");
        BtnRunRitual.Click += (_, _) => NexusShell.LogStub("run ritual");
        BtnRunNextStep.Click += (_, _) => NexusShell.LogStub("run next step");
        BtnResume.Click += (_, _) => NexusShell.LogStub("resume ritual");
        BtnPromoteHist.Click += (_, _) => NexusShell.LogStub("promote from history");
        BtnPromoteWatch.Click += (_, _) => NexusShell.LogStub("promote from watch");
        BtnTeachStart.Click += (_, _) => NexusShell.LogStub("start teach");
        BtnTeachStop.Click += (_, _) => NexusShell.LogStub("stop teach");
    }
}
