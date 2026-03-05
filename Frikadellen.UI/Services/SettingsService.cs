using System;
using System.IO;
using System.Text.Json;
using Frikadellen.UI.Models;

namespace Frikadellen.UI.Services;

/// <summary>Persists UiSettings to ./config/ui-settings.json.</summary>
public class SettingsService
{
    private static readonly string ConfigDir = Path.Combine(
        AppContext.BaseDirectory, "config");

    private static readonly string SettingsPath = Path.Combine(
        ConfigDir, "ui-settings.json");

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
    };

    public UiSettings Load()
    {
        try
        {
            if (File.Exists(SettingsPath))
            {
                var json = File.ReadAllText(SettingsPath);
                return JsonSerializer.Deserialize<UiSettings>(json, JsonOpts) ?? new UiSettings();
            }
        }
        catch { /* fall through to defaults */ }
        return new UiSettings();
    }

    public void Save(UiSettings settings)
    {
        try
        {
            Directory.CreateDirectory(ConfigDir);
            var json = JsonSerializer.Serialize(settings, JsonOpts);
            File.WriteAllText(SettingsPath, json);
        }
        catch { /* swallow write errors */ }
    }
}
