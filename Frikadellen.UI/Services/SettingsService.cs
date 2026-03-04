using System;
using System.IO;
using System.Text.Json;
using Frikadellen.UI.Models;

namespace Frikadellen.UI.Services;

public static class SettingsService
{
    private static readonly string SettingsDir =
        Path.Combine(AppContext.BaseDirectory, "config");

    private static readonly string SettingsPath =
        Path.Combine(SettingsDir, "ui-settings.json");

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    public static UiSettings Load()
    {
        try
        {
            if (File.Exists(SettingsPath))
            {
                var json = File.ReadAllText(SettingsPath);
                return JsonSerializer.Deserialize<UiSettings>(json, JsonOpts) ?? new UiSettings();
            }
        }
        catch
        {
            // fall through to defaults
        }
        return new UiSettings();
    }

    public static void Save(UiSettings settings)
    {
        Directory.CreateDirectory(SettingsDir);
        var json = JsonSerializer.Serialize(settings, JsonOpts);
        File.WriteAllText(SettingsPath, json);
    }
}
