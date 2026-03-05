using System;
using System.Threading.Tasks;
using System.Windows.Input;
using Avalonia.Threading;

namespace Frikadellen.UI.ViewModels;

/// <summary>
/// Shown on startup for ~2.5 s, then signals the shell to transition away.
/// Plug in real init work (config load, network ping) before calling Complete().
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

    /// <summary>App version displayed on the splash screen — update when bumping the release tag.</summary>
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
        // Simulate init steps with smooth progress
        await StepAsync("Loading configuration…", 0.20, 300);
        await StepAsync("Checking for updates…",  0.45, 350);
        await StepAsync("Connecting to backend…", 0.70, 400);
        await StepAsync("Ready.",                 1.00, 300);

        // Small pause so "Ready." is visible
        await Task.Delay(350);

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
