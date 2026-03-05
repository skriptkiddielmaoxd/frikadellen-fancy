using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using Avalonia.Threading;

namespace Frikadellen.UI.Services;

/// <summary>
/// Spawns and manages the lifecycle of the frikadellen-fancy Rust binary.
/// All stdout/stderr lines are posted onto <see cref="Output"/> on the UI thread
/// and also forwarded via <see cref="OutputReceived"/>.
/// </summary>
public sealed class RustProcessLauncher : IDisposable
{
    private Process? _process;
    private CancellationTokenSource? _cts;
    private bool _disposed;

    /// <summary>All console output lines (stdout + stderr). Bound by ConsoleViewModel.</summary>
    public ObservableCollection<ConsoleLineItem> Output { get; } = new();

    /// <summary>True while the Rust process is alive.</summary>
    public bool IsRunning => _process is { HasExited: false };

    /// <summary>Fires when the running state changes (true = started, false = stopped).</summary>
    public event Action<bool>? RunningChanged;

    /// <summary>Fires for each line of stdout/stderr. Args: (line, isError).</summary>
    public event Action<string, bool>? OutputReceived;

    /// <summary>Fires when the backend process exits. Arg: exit code.</summary>
    public event Action<int>? ProcessExited;

    /// <summary>
    /// Start the Rust binary. Auto-detects the path if not provided.
    /// Returns true on success or if already running.
    /// </summary>
    public bool Start(string? exePath = null)
    {
        if (IsRunning) return true;

        exePath ??= ResolveBinaryPath();
        if (exePath == null)
        {
            AppendLine("[launcher] Could not locate the Rust binary.", isError: true);
            return false;
        }

        _cts?.Cancel();
        _cts?.Dispose();
        _cts = new CancellationTokenSource();

        var si = new ProcessStartInfo
        {
            FileName               = exePath,
            WorkingDirectory       = Path.GetDirectoryName(exePath) ?? Directory.GetCurrentDirectory(),
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
            return false;
        }

        if (_process is null)
        {
            AppendLine($"[launcher] Process.Start returned null for '{exePath}'", isError: true);
            return false;
        }

        _process.EnableRaisingEvents = true;
        _process.Exited += OnProcessExited;

        _ = ReadStreamAsync(_process.StandardOutput, isError: false, _cts.Token);
        _ = ReadStreamAsync(_process.StandardError,  isError: true,  _cts.Token);

        AppendLine($"[launcher] Started PID {_process.Id}", isError: false);
        RunningChanged?.Invoke(true);
        return true;
    }

    /// <summary>Stop the backend process.</summary>
    public void Stop()
    {
        if (_process is null || _process.HasExited) return;
        try
        {
            _process.Kill(entireProcessTree: true);
            _process.WaitForExit(3000);
            AppendLine("[launcher] Process stopped.", isError: false);
        }
        catch (Exception ex)
        {
            AppendLine($"[launcher] Stop error: {ex.Message}", isError: true);
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _cts?.Cancel();
        Stop();
        _cts?.Dispose();
    }

    private void OnProcessExited(object? sender, EventArgs e)
    {
        var code = 0;
        try { code = _process?.ExitCode ?? -1; } catch { /* already disposed */ }
        AppendLine($"[launcher] Process exited (code {code}).", isError: code != 0);
        RunningChanged?.Invoke(false);
        ProcessExited?.Invoke(code);
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
                OutputReceived?.Invoke(line, isError);
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
            if (Output.Count > 2000)
                Output.RemoveAt(0);
        });
    }

    private string? ResolveBinaryPath()
    {
        var isWindows = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
        var candidates = new[]
        {
            isWindows ? "frikadellen-fancy.exe" : "frikadellen-fancy",
            isWindows ? "frikadellen_fancy.exe" : "frikadellen_fancy",
            isWindows ? "frikadellen_baf.exe"   : "frikadellen_baf",
        };

        string? CheckCandidates(params string[] dirs)
        {
            foreach (var name in candidates)
                foreach (var d in dirs)
                {
                    var full = Path.Combine(d, name);
                    if (File.Exists(full)) return full;
                }
            return null;
        }

        var baseDir = AppContext.BaseDirectory;
        var found = CheckCandidates(baseDir);
        if (found != null) return found;

        var dirInfo = new DirectoryInfo(baseDir);
        for (int i = 0; i < 6 && dirInfo != null; i++)
        {
            found = CheckCandidates(
                Path.Combine(dirInfo.FullName, "target", "debug"),
                Path.Combine(dirInfo.FullName, "target", "release"));
            if (found != null) return found;
            dirInfo = dirInfo.Parent;
        }

        return CheckCandidates(Directory.GetCurrentDirectory());
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
