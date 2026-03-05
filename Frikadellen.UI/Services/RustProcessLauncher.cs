using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;

namespace Frikadellen.UI.Services;

/// <summary>
/// Manages the lifecycle of the Rust backend process (frikadellen_fancy).
/// The binary is expected to live next to the UI executable, or in a known
/// sibling directory.  Stdout/stderr are discarded (the UI reads everything
/// via the HTTP+WebSocket API).
/// </summary>
public sealed class RustProcessLauncher : IDisposable
{
    private Process? _process;
    private bool _disposed;

    /// <summary>True while the child process is alive.</summary>
    public bool IsRunning => _process is { HasExited: false };

    /// <summary>Fires when the backend process exits unexpectedly.</summary>
    public event Action<int>? ProcessExited;

    /// <summary>Fires for each line of stdout/stderr from the Rust process.
    /// Args: (line, isError).</summary>
    public event Action<string, bool>? OutputReceived;

    /// <summary>
    /// Locate and start the Rust binary.  Returns true on success.
    /// If the process is already running this is a no-op that returns true.
    /// </summary>
    public bool Start()
    {
        if (IsRunning) return true;

        var exePath = ResolveBinaryPath();
        if (exePath == null) return false;

        var psi = new ProcessStartInfo
        {
            FileName = exePath,
            WorkingDirectory = Path.GetDirectoryName(exePath)!,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };

        try
        {
            var proc = Process.Start(psi);
            if (proc == null) return false;

            // Forward stdout/stderr lines to subscribers
            proc.OutputDataReceived += (_, e) =>
            {
                if (e.Data is { } line) OutputReceived?.Invoke(line, false);
            };
            proc.ErrorDataReceived += (_, e) =>
            {
                if (e.Data is { } line) OutputReceived?.Invoke(line, true);
            };
            proc.BeginOutputReadLine();
            proc.BeginErrorReadLine();

            proc.EnableRaisingEvents = true;
            proc.Exited += (_, _) =>
            {
                var code = 0;
                try { code = proc.ExitCode; } catch { /* already disposed */ }
                ProcessExited?.Invoke(code);
            };

            _process = proc;
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>Gracefully stop the backend (sends SIGTERM / kill).</summary>
    public void Stop()
    {
        if (!IsRunning) return;
        try
        {
            _process!.Kill(entireProcessTree: true);
            _process.WaitForExit(3000);
        }
        catch { /* process may have already exited */ }
        finally
        {
            _process?.Dispose();
            _process = null;
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        Stop();
    }

    // ────────── Binary resolution ──────────

    private static string? ResolveBinaryPath()
    {
        var binaryName = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            ? "frikadellen_fancy.exe"
            : "frikadellen_fancy";

        // 1. Next to the UI executable
        var baseDir = AppContext.BaseDirectory;
        var candidate = Path.Combine(baseDir, binaryName);
        if (File.Exists(candidate)) return candidate;

        // 2. Sibling "target/debug" (development layout)
        //    UI runs from Frikadellen.UI/bin/Debug/net8.0/ → walk up to repo root
        var dir = new DirectoryInfo(baseDir);
        for (var i = 0; i < 6 && dir != null; i++)
        {
            candidate = Path.Combine(dir.FullName, "target", "debug", binaryName);
            if (File.Exists(candidate)) return candidate;

            candidate = Path.Combine(dir.FullName, "target", "release", binaryName);
            if (File.Exists(candidate)) return candidate;

            dir = dir.Parent;
        }

        // 3. Same directory the working directory points to (manual override)
        candidate = Path.Combine(Directory.GetCurrentDirectory(), binaryName);
        if (File.Exists(candidate)) return candidate;

        return null;
    }
}
