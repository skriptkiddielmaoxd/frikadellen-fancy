using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Threading;
using Avalonia.Threading;

namespace Frikadellen.UI.Services;

/// <summary>
/// Spawns and kills the frikadellen-fancy Rust binary.
/// All stdout / stderr lines are posted onto <see cref="Output"/> on the UI thread.
/// </summary>
public sealed class RustProcessLauncher : IDisposable
{
    private Process? _process;
    private CancellationTokenSource? _cts;

    // ── Public state ──

    /// <summary>All console output lines (stdout + stderr). Bound by ConsoleViewModel.</summary>
    public ObservableCollection<ConsoleLineItem> Output { get; } = new();

    /// <summary>True while the Rust process is alive.</summary>
    public bool IsRunning => _process is { HasExited: false };

    // ── Events ──
    public event Action<bool>? RunningChanged;

    // ── Start / Stop ──

    /// <summary>
    /// Starts the Rust binary at <paramref name="exePath"/>.
    /// Does nothing if the process is already running.
    /// </summary>
    public void Start(string exePath = "frikadellen")
    {
        if (IsRunning) return;

        _cts = new CancellationTokenSource();

        var si = new ProcessStartInfo
        {
            FileName               = exePath,
            UseShellExecute        = false,
            RedirectStandardOutput = true,
            RedirectStandardError  = true,
            CreateNoWindow         = true,
        };

        // INTEGRATION POINT: pass config-file path or other args here if needed
        // si.Arguments = "--config config/config.toml";

        try
        {
            _process = Process.Start(si);
        }
        catch (Exception ex)
        {
            AppendLine($"[launcher] Failed to start '{exePath}': {ex.Message}", isError: true);
            return;
        }

        if (_process is null)
        {
            AppendLine($"[launcher] Process.Start returned null for '{exePath}'", isError: true);
            return;
        }

        _process.EnableRaisingEvents = true;
        _process.Exited += OnProcessExited;

        // Stream stdout
        _ = ReadStreamAsync(_process.StandardOutput, isError: false, _cts.Token);
        // Stream stderr
        _ = ReadStreamAsync(_process.StandardError,  isError: true,  _cts.Token);

        AppendLine($"[launcher] Started PID {_process.Id}", isError: false);
        RunningChanged?.Invoke(true);
    }

    /// <summary>Kills the Rust process if it is running.</summary>
    public void Stop()
    {
        if (_process is null || _process.HasExited) return;

        try
        {
            _process.Kill(entireProcessTree: true);
            AppendLine("[launcher] Process killed.", isError: false);
        }
        catch (Exception ex)
        {
            AppendLine($"[launcher] Kill failed: {ex.Message}", isError: true);
        }
    }

    public void Dispose()
    {
        _cts?.Cancel();
        Stop();
        _process?.Dispose();
        _cts?.Dispose();
    }

    // ── Private helpers ──

    private void OnProcessExited(object? sender, EventArgs e)
    {
        var code = _process?.ExitCode ?? -1;
        AppendLine($"[launcher] Process exited with code {code}.", isError: code != 0);
        RunningChanged?.Invoke(false);
    }

    private async System.Threading.Tasks.Task ReadStreamAsync(
        StreamReader reader, bool isError, CancellationToken ct)
    {
        try
        {
            while (!ct.IsCancellationRequested)
            {
                var line = await reader.ReadLineAsync(ct);
                if (line is null) break;
                AppendLine(line, isError);
            }
        }
        catch (OperationCanceledException) { /* normal shutdown */ }
        catch (Exception ex)
        {
            AppendLine($"[launcher] Stream read error: {ex.Message}", isError: true);
        }
    }

    private void AppendLine(string text, bool isError)
    {
        var item = new ConsoleLineItem(DateTimeOffset.Now, text, isError);
        Dispatcher.UIThread.Post(() =>
        {
            Output.Add(item);
            // Cap at 2000 lines to avoid unbounded memory growth
            if (Output.Count > 2000)
                Output.RemoveAt(0);
        });
    }
}

/// <summary>A single line in the console output pane.</summary>
public sealed record ConsoleLineItem(
    DateTimeOffset Timestamp,
    string Text,
    bool IsError)
{
    public string TimeLabel => Timestamp.ToString("HH:mm:ss");
    public string Foreground => IsError ? "#FB7185" : "#E2E8F0";
}
