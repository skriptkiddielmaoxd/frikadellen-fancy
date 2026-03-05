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
        // Try multiple possible binary names to be robust across build/artifact naming:
        // - frikadellen-fancy (cargo default with hyphen)
        // - frikadellen_fancy (underscore variant)
        // - frikadellen_baf (legacy name)
        var isWindows = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
        var candidates = new[]
        {
            isWindows ? "frikadellen-fancy.exe" : "frikadellen-fancy",
            isWindows ? "frikadellen_fancy.exe" : "frikadellen_fancy",
            isWindows ? "frikadellen_baf.exe" : "frikadellen_baf",
        };

        // Helper to test a path for any candidate names
        string? CheckCandidates(params string[] paths)
        {
            foreach (var name in candidates)
            {
                foreach (var p in paths)
                {
                    var full = Path.Combine(p, name);
                    if (File.Exists(full)) return full;
                }
            }
            return null;
        }

        // 1. Next to the UI executable
        var baseDir = AppContext.BaseDirectory;
        var found = CheckCandidates(baseDir);
        if (found != null) return found;

        // 2. Sibling "target/debug" and "target/release" (development layout)
        var dir = new DirectoryInfo(baseDir);
        for (var i = 0; i < 6 && dir != null; i++)
        {
            found = CheckCandidates(Path.Combine(dir.FullName, "target", "debug"), Path.Combine(dir.FullName, "target", "release"));
            if (found != null) return found;
            dir = dir.Parent;
        }

        // 3. Same directory the working directory points to (manual override)
        found = CheckCandidates(Directory.GetCurrentDirectory());
        if (found != null) return found;

        return null;
    }
}
