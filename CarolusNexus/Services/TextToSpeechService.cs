using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Speech.Synthesis;
using System.Text.Json;
using System.Threading.Tasks;
using NAudio.Wave;

namespace CarolusNexus.Services;

public static class TextToSpeechService
{
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromMinutes(2) };

    public static async Task<string> SpeakAsync(string text, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(text))
            return "Kein Text für TTS.";

        var env = DotEnvStore.Load();
        var mode = env.TryGetValue("TTS_PROVIDER", out var tp) ? tp.Trim().ToLowerInvariant() : "auto";

        if (mode is "windows" or "sapi")
            return await SpeakWindowsAsync(text, ct).ConfigureAwait(false);

        if (mode == "elevenlabs")
            return await SpeakElevenLabsAsync(env, text, ct).ConfigureAwait(false);

        // auto
        if (env.TryGetValue("ELEVENLABS_API_KEY", out var k) && !string.IsNullOrWhiteSpace(k)
            && env.TryGetValue("ELEVENLABS_VOICE_ID", out var v) && !string.IsNullOrWhiteSpace(v))
        {
            var err = await SpeakElevenLabsAsync(env, text, ct).ConfigureAwait(false);
            if (string.IsNullOrEmpty(err))
                return "";
        }

        return await SpeakWindowsAsync(text, ct).ConfigureAwait(false);
    }

    private static Task<string> SpeakWindowsAsync(string text, CancellationToken ct)
    {
        if (!OperatingSystem.IsWindows())
            return Task.FromResult("Windows-TTS nur unter Windows.");

        return Task.Run(() =>
        {
            try
            {
                using var synth = new SpeechSynthesizer();
                synth.SetOutputToDefaultAudioDevice();
                synth.Speak(text);
                return "";
            }
            catch (Exception ex)
            {
                return "Windows-TTS: " + ex.Message;
            }
        }, ct);
    }

    private static async Task<string> SpeakElevenLabsAsync(
        IReadOnlyDictionary<string, string> env,
        string text,
        CancellationToken ct)
    {
        if (!env.TryGetValue("ELEVENLABS_API_KEY", out var key) || string.IsNullOrWhiteSpace(key))
            return "Fehlt ELEVENLABS_API_KEY in windows\\.env für TTS.";
        if (!env.TryGetValue("ELEVENLABS_VOICE_ID", out var voiceId) || string.IsNullOrWhiteSpace(voiceId))
            return "Fehlt ELEVENLABS_VOICE_ID in windows\\.env für TTS.";

        voiceId = voiceId.Trim();
        var url = $"https://api.elevenlabs.io/v1/text-to-speech/{Uri.EscapeDataString(voiceId)}";
        using var req = new HttpRequestMessage(HttpMethod.Post, url);
        req.Headers.TryAddWithoutValidation("xi-api-key", key.Trim());
        req.Headers.TryAddWithoutValidation("Accept", "audio/mpeg");
        var body = JsonSerializer.Serialize(new { text, model_id = "eleven_multilingual_v2" });
        req.Content = new StringContent(body, System.Text.Encoding.UTF8, "application/json");

        using var resp = await Http.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false);
        if (!resp.IsSuccessStatusCode)
        {
            var err = await resp.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
            return $"ElevenLabs TTS HTTP {(int)resp.StatusCode}: {err}";
        }

        await using var stream = await resp.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
        await using var ms = new MemoryStream();
        await stream.CopyToAsync(ms, ct).ConfigureAwait(false);

        await Task.Run(() => PlayMp3(ms.ToArray(), ct), ct).ConfigureAwait(false);
        return "";
    }

    private static void PlayMp3(byte[] mp3, CancellationToken ct)
    {
        using var ms = new MemoryStream(mp3, writable: false);
        using var reader = new Mp3FileReader(ms);
        using var wo = new WaveOutEvent();
        using var done = new ManualResetEventSlim(false);
        wo.PlaybackStopped += (_, _) => done.Set();
        wo.Init(reader);
        using (ct.Register(() =>
               {
                   try
                   {
                       wo.Stop();
                   }
                   catch
                   {
                       // ignore
                   }
               }))
        {
            wo.Play();
            while (!done.IsSet)
                done.Wait(80, ct);
        }
    }
}
