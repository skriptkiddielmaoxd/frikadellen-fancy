using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows.Input;

namespace Frikadellen.UI.ViewModels;

public sealed class NotifierViewModel : ViewModelBase
{
    private string _botToken = string.Empty;

    public NotifierViewModel()
    {
        SaveTokenCommand = new RelayCommand(async () => await SaveTokenAsync());
        ClearTokenCommand = new RelayCommand(async () => await ClearTokenAsync());
        _ = LoadTokenAsync();
    }

    public string BotToken { get => _botToken; set => SetField(ref _botToken, value); }

    public ICommand SaveTokenCommand { get; }
    public ICommand ClearTokenCommand { get; }

    private string GetNotifierDir()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var dir = Path.Combine(appData, "Frikadellen", "notifier");
        Directory.CreateDirectory(dir);
        return dir;
    }

    private string GetTokenFile() => Path.Combine(GetNotifierDir(), "notifier.json");

    private async Task SaveTokenAsync()
    {
        try
        {
            var file = GetTokenFile();
            var json = JsonSerializer.Serialize(new { botToken = BotToken ?? string.Empty }, new JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(file, json);
        }
        catch { }
    }

    private async Task ClearTokenAsync()
    {
        try
        {
            var file = GetTokenFile();
            if (File.Exists(file)) File.Delete(file);
        }
        catch { }
        BotToken = string.Empty;
        await Task.CompletedTask;
    }

    private async Task LoadTokenAsync()
    {
        try
        {
            var file = GetTokenFile();
            if (!File.Exists(file)) return;
            var json = await File.ReadAllTextAsync(file);
            var doc = JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty("botToken", out var t))
                BotToken = t.GetString() ?? string.Empty;
        }
        catch { }
    }
}
