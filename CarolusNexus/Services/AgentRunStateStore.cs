using System;
using System.Threading;

namespace CarolusNexus.Services;

/// <summary>Einfacher Agent-/Plan-Laufzustand für UI und LocalToolHost.</summary>
public static class AgentRunStateStore
{
    private static readonly object Gate = new();
    private static string? _runId;
    private static string? _recipeName;
    private static int _totalSteps;
    private static int _currentStepIndex;
    private static string? _lastError;

    public static void BeginRun(string runId, string recipeName, int totalSteps)
    {
        lock (Gate)
        {
            _runId = runId;
            _recipeName = recipeName;
            _totalSteps = totalSteps;
            _currentStepIndex = 0;
            _lastError = null;
        }
    }

    public static void SetProgress(int zeroBasedIndex, string? lastError = null)
    {
        lock (Gate)
        {
            _currentStepIndex = zeroBasedIndex;
            _lastError = lastError;
        }
    }

    public static void EndRun()
    {
        lock (Gate)
        {
            _runId = null;
            _recipeName = null;
            _totalSteps = 0;
            _currentStepIndex = 0;
            _lastError = null;
        }
    }

    public static AgentRunSnapshot Snapshot()
    {
        lock (Gate)
        {
            return new AgentRunSnapshot(_runId, _recipeName, _totalSteps, _currentStepIndex, _lastError);
        }
    }

    public sealed record AgentRunSnapshot(
        string? RunId,
        string? RecipeName,
        int TotalSteps,
        int CurrentStepIndex,
        string? LastError);
}
