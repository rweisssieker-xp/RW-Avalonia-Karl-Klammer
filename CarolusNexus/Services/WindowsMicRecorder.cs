using System;
using System.IO;
using System.Threading.Tasks;
using NAudio.Wave;

namespace CarolusNexus.Services;

/// <summary>WAV-Aufnahme vom Standard-Mikrofon (16 kHz mono) für Whisper/STT.</summary>
public sealed class WindowsMicRecorder : IDisposable
{
    private WaveInEvent? _waveIn;
    private WaveFileWriter? _writer;
    private string? _path;

    public bool IsRecording => _waveIn != null;

    public void Start()
    {
        StopSync();
        _path = Path.Combine(Path.GetTempPath(), $"carolus-mic-{Guid.NewGuid():N}.wav");
        _waveIn = new WaveInEvent
        {
            WaveFormat = new WaveFormat(16000, 16, 1),
            BufferMilliseconds = 100
        };
        _writer = new WaveFileWriter(_path, _waveIn.WaveFormat);
        _waveIn.DataAvailable += (_, a) =>
        {
            if (a.BytesRecorded > 0)
                _writer?.Write(a.Buffer, 0, a.BytesRecorded);
        };
        _waveIn.StartRecording();
    }

    public void StopSync()
    {
        try
        {
            _waveIn?.StopRecording();
        }
        catch
        {
            // ignore
        }

        _waveIn?.Dispose();
        _waveIn = null;
        try
        {
            _writer?.Flush();
            _writer?.Dispose();
        }
        catch
        {
            // ignore
        }

        _writer = null;
        if (_path != null && File.Exists(_path))
        {
            try
            {
                File.Delete(_path);
            }
            catch
            {
                // ignore
            }
        }

        _path = null;
    }

    /// <summary>Beendet Aufnahme und liefert Pfad zur WAV-Datei (oder null).</summary>
    public async Task<string?> StopToFileAsync()
    {
        if (_waveIn == null || _path == null)
            return null;

        var path = _path;
        var waveIn = _waveIn;
        var writer = _writer;
        _waveIn = null;
        _writer = null;
        _path = null;

        var tcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        waveIn.RecordingStopped += OnStopped;
        try
        {
            waveIn.StopRecording();
        }
        catch
        {
            tcs.TrySetResult();
        }

        await tcs.Task.ConfigureAwait(false);

        try
        {
            writer?.Flush();
            writer?.Dispose();
        }
        catch
        {
            // ignore
        }

        waveIn.Dispose();

        return path != null && File.Exists(path) ? path : null;

        void OnStopped(object? sender, StoppedEventArgs e)
        {
            waveIn.RecordingStopped -= OnStopped;
            tcs.TrySetResult();
        }
    }

    public void Dispose()
    {
        StopSync();
    }
}
