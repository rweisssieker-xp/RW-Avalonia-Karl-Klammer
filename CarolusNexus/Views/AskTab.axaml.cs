using Avalonia.Controls;
using Avalonia.Interactivity;
using CarolusNexus;

namespace CarolusNexus.Views;

public partial class AskTab : UserControl
{
    public AskTab()
    {
        InitializeComponent();
        Wire();
    }

    private void Wire()
    {
        BtnAskNow.Click += (_, _) => Stub("ask now");
        BtnSmoke.Click += (_, _) => Stub("smoke test");
        BtnImportAudio.Click += (_, _) => Stub("import audio + transcribe");
        BtnPttStart.Click += (_, _) => Stub("start push-to-talk");
        BtnPttStop.Click += (_, _) => Stub("stop + ask");
        BtnCancelRec.Click += (_, _) => Stub("cancel recording");
        BtnClearConv.Click += (_, _) => { AssistantOut.Text = ""; NexusShell.LogStub("clear conversation"); };
        BtnRunPlan.Click += (_, _) => Stub("run plan");
        BtnApproveRun.Click += (_, _) => Stub("approve + run");
        BtnRunNext.Click += (_, _) => Stub("run next step");
        BtnSaveRitual.Click += (_, _) => Stub("save plan as ritual");
        BtnClearPlan.Click += (_, _) => { PlanPreview.Text = ""; PlanExec.Text = ""; NexusShell.LogStub("clear plan"); };
        BtnPanic.Click += (_, _) => Stub("panic stop");
        BtnSpeak.Click += (_, _) => Stub("speak response");
    }

    private void Stub(string action)
    {
        NexusShell.LogStub(action);
        AssistantOut.Text =
            $"(Stub) Anfrage: „{PromptBox.Text?.Trim()}“\r\nAktion: {action}\r\n— Provider/Vision/STT/TTS/Plan-Runtime noch nicht angebunden.";
    }
}
