using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace CarolusNexus.Services;

public static class SpeechTranscriptionService
{
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromMinutes(15) };

    public static async Task<string> TranscribeFileAsync(string audioPath, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(audioPath) || !File.Exists(audioPath))
            return "Keine Audiodatei.";

        var env = DotEnvStore.Load();
        var provider = env.TryGetValue("STT_PROVIDER", out var p) ? p.Trim().ToLowerInvariant() : "whisper";

        return provider switch
        {
            "elevenlabs" => await TranscribeElevenLabsAsync(env, audioPath, ct).ConfigureAwait(false),
            _ => await TranscribeWhisperAsync(env, audioPath, ct).ConfigureAwait(false)
        };
    }

    private static async Task<string> TranscribeWhisperAsync(
        IReadOnlyDictionary<string, string> env,
        string audioPath,
        CancellationToken ct)
    {
        var python = env.TryGetValue("WHISPER_PYTHON", out var py) && !string.IsNullOrWhiteSpace(py)
            ? py.Trim()
            : "python";
        var model = env.TryGetValue("WHISPER_MODEL", out var m) && !string.IsNullOrWhiteSpace(m)
            ? m.Trim()
            : "base";
        var lang = env.TryGetValue("WHISPER_LANGUAGE", out var l) ? l.Trim() : "de";

        var outDir = Path.Combine(Path.GetTempPath(), "whisper-out-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(outDir);

        var psi = new ProcessStartInfo
        {
            FileName = python,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };
        psi.ArgumentList.Add("-m");
        psi.ArgumentList.Add("whisper");
        psi.ArgumentList.Add(audioPath);
        psi.ArgumentList.Add("--model");
        psi.ArgumentList.Add(model);
        psi.ArgumentList.Add("--output_format");
        psi.ArgumentList.Add("txt");
        psi.ArgumentList.Add("--output_dir");
        psi.ArgumentList.Add(outDir);
        if (!string.IsNullOrWhiteSpace(lang) && !lang.Equals("auto", StringComparison.OrdinalIgnoreCase))
        {
            psi.ArgumentList.Add("--language");
            psi.ArgumentList.Add(lang);
        }

        using var proc = Process.Start(psi);
        if (proc == null)
            return "Whisper: Prozessstart fehlgeschlagen.";

        await proc.WaitForExitAsync(ct).ConfigureAwait(false);
        var err = await proc.StandardError.ReadToEndAsync(ct).ConfigureAwait(false);
        if (proc.ExitCode != 0)
            return $"Whisper beendet mit {proc.ExitCode}: {err.Trim()}";

        var baseName = Path.GetFileNameWithoutExtension(audioPath);
        var txtPath = Path.Combine(outDir, baseName + ".txt");
        if (!File.Exists(txtPath))
            return $"Whisper: erwartete Ausgabe fehlt ({txtPath}). {err.Trim()}";

        var text = await File.ReadAllTextAsync(txtPath, ct).ConfigureAwait(false);
        try
        {
            Directory.Delete(outDir, recursive: true);
        }
        catch
        {
            // ignore
        }

        return text.Trim();
    }

    private static async Task<string> TranscribeElevenLabsAsync(
        IReadOnlyDictionary<string, string> env,
        string audioPath,
        CancellationToken ct)
    {
        if (!env.TryGetValue("ELEVENLABS_API_KEY", out var key) || string.IsNullOrWhiteSpace(key))
            return "Fehlt ELEVENLABS_API_KEY in windows\\.env für STT.";

        var modelId = env.TryGetValue("ELEVENLABS_STT_MODEL", out var mid) && !string.IsNullOrWhiteSpace(mid)
            ? mid.Trim()
            : "scribe_v1";
        var lang = env.TryGetValue("WHISPER_LANGUAGE", out var l) ? l.Trim() : "";

        await using var fs = File.OpenRead(audioPath);
        using var content = new MultipartFormDataContent();
        var fileContent = new StreamContent(fs);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
        content.Add(fileContent, "file", Path.GetFileName(audioPath));
        content.Add(new StringContent(modelId), "model_id");
        if (!string.IsNullOrWhiteSpace(lang) && !lang.Equals("auto", StringComparison.OrdinalIgnoreCase))
            content.Add(new StringContent(lang), "language_code");

        using var req = new HttpRequestMessage(HttpMethod.Post, "https://api.elevenlabs.io/v1/speech-to-text");
        req.Headers.TryAddWithoutValidation("xi-api-key", key.Trim());
        req.Content = content;

        using var resp = await Http.SendAsync(req, ct).ConfigureAwait(false);
        var json = await resp.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
        if (!resp.IsSuccessStatusCode)
            return $"ElevenLabs STT HTTP {(int)resp.StatusCode}: {json}";

        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty("text", out var te))
                return te.GetString()?.Trim() ?? json;
            if (doc.RootElement.TryGetProperty("transcripts", out var arr)
                && arr.ValueKind == JsonValueKind.Array
                && arr.GetArrayLength() > 0
                && arr[0].TryGetProperty("text", out var t0))
                return t0.GetString()?.Trim() ?? json;
        }
        catch
        {
            return json;
        }

        return json;
    }
}
