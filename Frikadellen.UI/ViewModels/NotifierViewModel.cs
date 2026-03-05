using System.Windows.Input;
using Frikadellen.UI.Models;
using Frikadellen.UI.Services;

namespace Frikadellen.UI.ViewModels;

/// <summary>Discord bot / webhook notifier settings.</summary>
public sealed class NotifierViewModel : ViewModelBase
{
    private readonly SettingsService _svc;
    private UiSettings _cfg;

    private bool _isTokenVisible;
    private string _saveStatus = "";

    public string DiscordBotToken
    {
        get => _cfg.DiscordBotToken;
        set { _cfg.DiscordBotToken = value; OnPropertyChanged(); }
    }

    public string DiscordChannelId
    {
        get => _cfg.DiscordChannelId;
        set { _cfg.DiscordChannelId = value; OnPropertyChanged(); }
    }

    public string WebhookUrl
    {
        get => _cfg.WebhookUrl;
        set { _cfg.WebhookUrl = value; OnPropertyChanged(); }
    }

    public bool IsTokenVisible
    {
        get => _isTokenVisible;
        set
        {
            if (SetField(ref _isTokenVisible, value))
                OnPropertyChanged(nameof(TokenPasswordChar));
        }
    }

    public char? TokenPasswordChar => IsTokenVisible ? null : '•';

    public string SaveStatus
    {
        get => _saveStatus;
        set => SetField(ref _saveStatus, value);
    }

    public ICommand ToggleTokenVisibilityCommand { get; }
    public ICommand SaveCommand { get; }

    public NotifierViewModel(SettingsService svc)
    {
        _svc = svc;
        _cfg = svc.Load();
        ToggleTokenVisibilityCommand = new RelayCommand(() => IsTokenVisible = !IsTokenVisible);
        SaveCommand = new RelayCommand(Save);
    }

    private async void Save()
    {
        _svc.Save(_cfg);
        SaveStatus = "Saved ✓";
        await System.Threading.Tasks.Task.Delay(2000);
        SaveStatus = "";
    }
}
