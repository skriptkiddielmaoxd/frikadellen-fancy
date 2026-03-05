using System;
using System.Threading.Tasks;
using Avalonia.Threading;

namespace Frikadellen.UI.ViewModels;

/// <summary>
/// Shown on startup for ~2 s, then signals the shell to transition away.
/// Real init work (config load, network ping) can be added before calling Complete().
/// </summary>
public sealed class SplashViewModel : ViewModelBase
{
    private double _progress;
    private string _statusMessage = "Initialising…";
    private bool _isDone;

    public double Progress
    {
        get => _progress;
        set => SetField(ref _progress, value);
    }

    public string StatusMessage
    {
        get => _statusMessage;
        set => SetField(ref _statusMessage, value);
    }

    /// <summary>App version string shown on the splash screen.</summary>
    public string AppVersion => "v3.0.0";

    public bool IsDone
    {
        get => _isDone;
        private set => SetField(ref _isDone, value);
    }

    /// <summary>Raised when the splash is complete and the shell should be shown.</summary>
    public event Action? Completed;

    public SplashViewModel()
    {
        _ = RunAsync();
    }

    private async Task RunAsync()
    {
        await StepAsync("Loading configuration…", 0.20, 280);
        await StepAsync("Checking for updates…",  0.45, 320);
        await StepAsync("Connecting to backend…", 0.70, 360);
        await StepAsync("Ready.",                 1.00, 280);

        await Task.Delay(300);

        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            IsDone = true;
            Completed?.Invoke();
        });
    }

    private async Task StepAsync(string message, double target, int durationMs)
    {
        const int steps = 30;
        var from = Progress;
        var delta = target - from;
        var interval = durationMs / steps;

        StatusMessage = message;

        for (int i = 1; i <= steps; i++)
        {
            await Task.Delay(interval);
            await Dispatcher.UIThread.InvokeAsync(() =>
                Progress = from + delta * (i / (double)steps));
        }
    }
}
