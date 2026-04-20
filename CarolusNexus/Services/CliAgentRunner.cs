using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace CarolusNexus.Services;

public static class CliAgentRunner
{
    public static async Task<(string LogPath, string Excerpt)> RunAsync(
        string agent,
        string prompt,
        CancellationToken ct = default)
    {
        var display = string.IsNullOrWhiteSpace(agent) ? "agent" : agent.Trim();
        if (display.Length > 24)
            display = display[..23] + "…";
        ActivityStatusHub.SetCliAgentRun(display);

        try
        {
            Directory.CreateDirectory(AppPaths.CodexOutputDir);
            var ts = DateTime.Now.ToString("yyyyMMdd-HHmmss");
            var tag = agent.Replace(' ', '-').ToLowerInvariant();
            var logPath = Path.Combine(AppPaths.CodexOutputDir, $"karl-klammer-{tag}-{ts}.txt");
            var env = DotEnvStore.Load();

            return agent switch
            {
                "codex" => await RunCodexAsync(env, prompt, logPath, ct),
                "claude code" => await RunClaudeAsync(env, prompt, logPath, ct),
                "openclaw" => await RunOpenClawAsync(env, prompt, logPath, ct),
                _ => (logPath, "Unknown agent: " + agent)
            };
        }
        finally
        {
            ActivityStatusHub.SetCliAgentRun(null);
        }
    }

    private static void ApplyOpenClawGatewayEnv(ProcessStartInfo psi, IReadOnlyDictionary<string, string> env)
    {
        if (env.TryGetValue("OPENCLAW_GATEWAY_URL", out var u) && !string.IsNullOrWhiteSpace(u))
            psi.Environment["OPENCLAW_GATEWAY_URL"] = u.Trim();
        if (env.TryGetValue("GATEWAY_TOKEN", out var t) && !string.IsNullOrWhiteSpace(t))
            psi.Environment["GATEWAY_TOKEN"] = t.Trim();
    }

    private static string ResolveWorkDir(IReadOnlyDictionary<string, string> env)
    {
        var work = env.TryGetValue("CODEX_WORKDIR", out var w) && !string.IsNullOrWhiteSpace(w)
            ? w.Trim().TrimEnd('/', '\\')
            : "playground";
        var wd = Path.IsPathRooted(work)
            ? work
            : Path.Combine(AppPaths.RepoRoot, work);
        Directory.CreateDirectory(wd);
        return wd;
    }

    private static async Task<(string, string)> RunCodexAsync(
        IReadOnlyDictionary<string, string> env,
        string prompt,
        string logPath,
        CancellationToken ct)
    {
        var cmd = env.TryGetValue("CODEX_COMMAND", out var c) && !string.IsNullOrWhiteSpace(c)
            ? c
            : "codex.cmd";
        var wd = ResolveWorkDir(env);

        var psi = new ProcessStartInfo
        {
            FileName = cmd,
            UseShellExecute = false,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
            WorkingDirectory = wd,
        };
        psi.ArgumentList.Add("exec");
        psi.ArgumentList.Add("--full-auto");
        psi.ArgumentList.Add("--skip-git-repo-check");
        psi.ArgumentList.Add("-C");
        psi.ArgumentList.Add(wd);
        psi.ArgumentList.Add("-");

        var timeoutSec = env.TryGetValue("CODEX_TIMEOUT_SECONDS", out var tstr) && int.TryParse(tstr, out var to) && to > 0
            ? to
            : 900;
        using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(timeoutSec));
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct, timeoutCts.Token);
        return await StartCaptureAsync(psi, prompt ?? "", logPath, linked.Token);
    }

    private static async Task<(string, string)> RunClaudeAsync(
        IReadOnlyDictionary<string, string> env,
        string prompt,
        string logPath,
        CancellationToken ct)
    {
        var cmd = env.TryGetValue("CLAUDE_CODE_COMMAND", out var c) && !string.IsNullOrWhiteSpace(c)
            ? c
            : "claude";
        var wd = ResolveWorkDir(env);

        var psi = new ProcessStartInfo
        {
            FileName = cmd,
            UseShellExecute = false,
            RedirectStandardInput = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
            WorkingDirectory = wd,
        };
        psi.ArgumentList.Add("-p");
        psi.ArgumentList.Add(prompt ?? "");
        psi.ArgumentList.Add("--permission-mode");
        psi.ArgumentList.Add("bypassPermissions");

        return await StartCaptureAsync(psi, null, logPath, ct);
    }

    private static async Task<(string, string)> RunOpenClawAsync(
        IReadOnlyDictionary<string, string> env,
        string prompt,
        string logPath,
        CancellationToken ct)
    {
        var cmd = env.TryGetValue("OPENCLAW_COMMAND", out var c) && !string.IsNullOrWhiteSpace(c)
            ? c
            : "openclaw";
        var wd = ResolveWorkDir(env);
        var session = env.TryGetValue("OPENCLAW_SESSION_KEY", out var s) && !string.IsNullOrWhiteSpace(s)
            ? s
            : "main";
        var timeout = env.TryGetValue("OPENCLAW_TIMEOUT_SECONDS", out var t) && int.TryParse(t, out var sec)
            ? sec
            : 120;

        var psi = new ProcessStartInfo
        {
            FileName = cmd,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
            WorkingDirectory = wd,
        };
        ApplyOpenClawGatewayEnv(psi, env);
        psi.ArgumentList.Add("agent");
        psi.ArgumentList.Add("--agent");
        psi.ArgumentList.Add(session);
        psi.ArgumentList.Add("--message");
        psi.ArgumentList.Add(prompt ?? "");
        psi.ArgumentList.Add("--timeout");
        psi.ArgumentList.Add(timeout.ToString());

        return await StartCaptureAsync(psi, null, logPath, ct);
    }

    private static async Task<(string LogPath, string Excerpt)> StartCaptureAsync(
        ProcessStartInfo psi,
        string? stdinText,
        string logPath,
        CancellationToken ct)
    {
        var header = new StringBuilder();
        header.AppendLine($"file: {psi.FileName}");
        if (!string.IsNullOrEmpty(psi.Arguments))
            header.AppendLine($"args(raw): {psi.Arguments}");
        header.AppendLine($"cwd: {psi.WorkingDirectory}");
        header.AppendLine("---");

        using var proc = new Process { StartInfo = psi };
        proc.Start();
        var stdoutTask = proc.StandardOutput.ReadToEndAsync(ct);
        var stderrTask = proc.StandardError.ReadToEndAsync(ct);
        using (ct.Register(() =>
               {
                   try
                   {
                       if (!proc.HasExited)
                           proc.Kill(entireProcessTree: true);
                   }
                   catch
                   {
                       // ignore
                   }
               }))
        {
            if (stdinText != null)
            {
                await proc.StandardInput.WriteAsync(stdinText.AsMemory(), ct).ConfigureAwait(false);
                await proc.StandardInput.FlushAsync(ct).ConfigureAwait(false);
                proc.StandardInput.Close();
            }

            await Task.WhenAll(stdoutTask, stderrTask).ConfigureAwait(false);
            var stdout = await stdoutTask.ConfigureAwait(false);
            var stderr = await stderrTask.ConfigureAwait(false);
            await proc.WaitForExitAsync(ct).ConfigureAwait(false);

            var sb = new StringBuilder();
            sb.Append(header);
            sb.AppendLine(stdout);
            if (!string.IsNullOrWhiteSpace(stderr))
            {
                sb.AppendLine("--- stderr ---");
                sb.AppendLine(stderr);
            }

            sb.AppendLine($"--- exit {proc.ExitCode} ---");
            var full = sb.ToString();
            await File.WriteAllTextAsync(logPath, full, ct).ConfigureAwait(false);
            var excerpt = full.Length > 8000 ? full[..8000] + "\n…" : full;
            return (logPath, excerpt);
        }
    }
}
