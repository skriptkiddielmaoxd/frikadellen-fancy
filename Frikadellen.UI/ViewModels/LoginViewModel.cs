using System;
using System.Windows.Input;
using Frikadellen.UI.Models;
using Frikadellen.UI.Services;

namespace Frikadellen.UI.ViewModels;

/// <summary>
/// First-run / account-setup screen.
/// Collects the Minecraft username and starts the Microsoft auth flow.
/// </summary>
public sealed class LoginViewModel : ViewModelBase
{
    private readonly SettingsService _settings;
    private string _username = "";
    private string _statusMessage = "";
    private bool _isAuthenticating;
    private bool _canProceed;

    public string Username
    {
        get => _username;
        set
        {
            if (SetField(ref _username, value))
                CanProceed = !string.IsNullOrWhiteSpace(value);
        }
    }

    public string StatusMessage
    {
        get => _statusMessage;
        set => SetField(ref _statusMessage, value);
    }

    public bool IsAuthenticating
    {
        get => _isAuthenticating;
        set => SetField(ref _isAuthenticating, value);
    }

    public bool CanProceed
    {
        get => _canProceed;
        set
        {
            if (SetField(ref _canProceed, value))
                ((RelayCommand)ProceedCommand).RaiseCanExecuteChanged();
        }
    }

    public ICommand ProceedCommand { get; }
    public ICommand SkipCommand { get; }

    /// <summary>Raised when login is complete (or skipped) — shell should show main UI.</summary>
    public event Action? Completed;

    public LoginViewModel(SettingsService settings, UiSettings saved)
    {
        _settings = settings;
        Username = saved.MinecraftUsername;

        ProceedCommand = new RelayCommand(Proceed, () => CanProceed && !IsAuthenticating);
        SkipCommand    = new RelayCommand(Skip);
    }

    private async void Proceed()
    {
        IsAuthenticating = true;
        StatusMessage = "Opening Microsoft login…";

        // INTEGRATION POINT: Launch the real device-code auth flow here.
        // For now, simulate a short delay.
        await System.Threading.Tasks.Task.Delay(1500);

        StatusMessage = "Authenticated ✓";

        var cfg = _settings.Load();
        cfg.MinecraftUsername = Username;
        cfg.FirstRunComplete = true;
        _settings.Save(cfg);

        await System.Threading.Tasks.Task.Delay(600);
        Completed?.Invoke();
    }

    private void Skip()
    {
        Completed?.Invoke();
    }
}
