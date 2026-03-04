using System.Windows.Input;
using Frikadellen.UI.Models;
using Frikadellen.UI.Services;

namespace Frikadellen.UI.ViewModels;

public sealed class SettingsViewModel : ViewModelBase
{
    private string _token;
    private string _allowedChannelId;
    private string _publishPath;
    private string _saveStatusText = string.Empty;

    public SettingsViewModel()
    {
        var s = SettingsService.Load();
        _token = s.Token;
        _allowedChannelId = s.AllowedChannelId;
        _publishPath = s.PublishPath;

        SaveCommand = new RelayCommand(Save);
    }

    public string Token { get => _token; set => SetField(ref _token, value); }
    public string AllowedChannelId { get => _allowedChannelId; set => SetField(ref _allowedChannelId, value); }
    public string PublishPath { get => _publishPath; set => SetField(ref _publishPath, value); }
    public string SaveStatusText { get => _saveStatusText; set => SetField(ref _saveStatusText, value); }

    public ICommand SaveCommand { get; }

    private void Save()
    {
        SettingsService.Save(new UiSettings
        {
            Token = Token,
            AllowedChannelId = AllowedChannelId,
            PublishPath = PublishPath
        });
        SaveStatusText = "Saved ✓";
    }
}
