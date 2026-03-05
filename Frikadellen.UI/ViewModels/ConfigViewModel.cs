using System.Threading.Tasks;
using System.Collections.ObjectModel;
using System.Linq;
using System.IO;
using System.Text.Json;
using System.Windows.Input;
using Avalonia.Threading;
using Frikadellen.UI.Services;

namespace Frikadellen.UI.ViewModels;

/// <summary>
/// Runtime-tunable configuration that mirrors the Rust Config/SkipConfig.
/// Loads from GET /api/config and persists via PUT /api/config.
/// </summary>
public sealed class ConfigViewModel : ViewModelBase
{
    private readonly BackendClient _backend;

    // ── Named configs UI ──
    private string _configName = string.Empty;
    private ObservableCollection<string> _namedConfigs = new();
    private string? _selectedNamedConfig;

    // ── Flip toggles ──
    private bool _enableAhFlips = true;
    private bool _enableBazaarFlips = true;

    // ── Delays (ms) ──
    private long _flipActionDelay = 150;
    private long _commandDelayMs = 500;
    private long _bedSpamClickDelay = 100;

    // ── Bazaar ──
    private long _bazaarOrderCheckIntervalSeconds = 30;
    private long _bazaarOrderCancelMinutes = 5;

    // ── Behaviour toggles ──
    private bool _bedSpam;
    private bool _useCoflChat = true;
    private bool _fastbuy;
    private long _autoCookie;
    private long _auctionDurationHours = 24;

    // ── Skip filter ──
    private long _skipMinProfit = 1_000_000;
    private double _skipProfitPercentage = 50.0;
    private long _skipMinPrice = 10_000_000;
    private bool _skipAlways;
    private bool _skipUserFinder;
    private bool _skipSkins;

    // ── Network ──
    private string _webhookUrl = string.Empty;
    private int _webGuiPort = 8080;
    private bool _proxyEnabled;
    private string _proxy = string.Empty;

    // ── Discord ──
    private string _discordBotToken = string.Empty;
    private string _discordChannelId = string.Empty;

    private string _applyStatusText = string.Empty;
    private bool _isBusy;

    public ICommand SaveNamedConfigCommand { get; private set; }
    public ICommand LoadNamedConfigCommand { get; private set; }
    public ICommand RefreshNamedConfigsCommand { get; private set; }
    public ICommand DeleteNamedConfigCommand { get; private set; }

    public ConfigViewModel(BackendClient backend)
    {
        _backend = backend;
        ApplyCommand = new RelayCommand(Apply);
        ResetDefaultsCommand = new RelayCommand(ResetDefaults);
        SaveNamedConfigCommand = new RelayCommand(async () => await SaveNamedConfigAsync());
        LoadNamedConfigCommand = new RelayCommand(async () => await LoadNamedConfigAsync());
        RefreshNamedConfigsCommand = new RelayCommand(async () => await RefreshNamedConfigsAsync());
        DeleteNamedConfigCommand = new RelayCommand(async () => await DeleteNamedConfigAsync());

        _ = LoadFromBackendAsync();
        _ = RefreshNamedConfigsAsync();
    }

    // ── Flip toggles ──
    public bool EnableAhFlips { get => _enableAhFlips; set => SetField(ref _enableAhFlips, value); }
    public bool EnableBazaarFlips { get => _enableBazaarFlips; set => SetField(ref _enableBazaarFlips, value); }

    // ── Delays ──
    public long FlipActionDelay { get => _flipActionDelay; set => SetField(ref _flipActionDelay, value); }
    public long CommandDelayMs { get => _commandDelayMs; set => SetField(ref _commandDelayMs, value); }
    public long BedSpamClickDelay { get => _bedSpamClickDelay; set => SetField(ref _bedSpamClickDelay, value); }

    // ── Bazaar ──
    public long BazaarOrderCheckIntervalSeconds { get => _bazaarOrderCheckIntervalSeconds; set => SetField(ref _bazaarOrderCheckIntervalSeconds, value); }
    public long BazaarOrderCancelMinutes { get => _bazaarOrderCancelMinutes; set => SetField(ref _bazaarOrderCancelMinutes, value); }

    // ── Behaviour ──
    public bool BedSpam { get => _bedSpam; set => SetField(ref _bedSpam, value); }
    public bool UseCoflChat { get => _useCoflChat; set => SetField(ref _useCoflChat, value); }
    public bool Fastbuy { get => _fastbuy; set => SetField(ref _fastbuy, value); }
    public long AutoCookie { get => _autoCookie; set => SetField(ref _autoCookie, value); }
    public long AuctionDurationHours { get => _auctionDurationHours; set => SetField(ref _auctionDurationHours, value); }

    // ── Skip filter ──
    public long SkipMinProfit { get => _skipMinProfit; set => SetField(ref _skipMinProfit, value); }
    public double SkipProfitPercentage { get => _skipProfitPercentage; set => SetField(ref _skipProfitPercentage, value); }
    public long SkipMinPrice { get => _skipMinPrice; set => SetField(ref _skipMinPrice, value); }
    public bool SkipAlways { get => _skipAlways; set => SetField(ref _skipAlways, value); }
    public bool SkipUserFinder { get => _skipUserFinder; set => SetField(ref _skipUserFinder, value); }
    public bool SkipSkins { get => _skipSkins; set => SetField(ref _skipSkins, value); }

    // ── Network ──
    public string WebhookUrl { get => _webhookUrl; set => SetField(ref _webhookUrl, value); }
    public int WebGuiPort { get => _webGuiPort; set => SetField(ref _webGuiPort, value); }
    public bool ProxyEnabled { get => _proxyEnabled; set => SetField(ref _proxyEnabled, value); }
    public string Proxy { get => _proxy; set => SetField(ref _proxy, value); }

    // ── Discord ──
    public string DiscordBotToken { get => _discordBotToken; set => SetField(ref _discordBotToken, value); }
    public string DiscordChannelId { get => _discordChannelId; set => SetField(ref _discordChannelId, value); }

    public string ApplyStatusText { get => _applyStatusText; set => SetField(ref _applyStatusText, value); }
    public bool IsBusy { get => _isBusy; set => SetField(ref _isBusy, value); }

    public string ConfigName { get => _configName; set => SetField(ref _configName, value); }
    public ObservableCollection<string> NamedConfigs { get => _namedConfigs; }
    public string? SelectedNamedConfig { get => _selectedNamedConfig; set => SetField(ref _selectedNamedConfig, value); }

    public ICommand ApplyCommand { get; }
    public ICommand ResetDefaultsCommand { get; }

    private async Task LoadFromBackendAsync()
    {
        var cfg = await _backend.GetConfigAsync();
        if (cfg == null) return;

        Dispatcher.UIThread.Post(() =>
        {
            if (cfg.EnableAhFlips.HasValue) EnableAhFlips = cfg.EnableAhFlips.Value;
            if (cfg.EnableBazaarFlips.HasValue) EnableBazaarFlips = cfg.EnableBazaarFlips.Value;
            if (cfg.FlipActionDelay.HasValue) FlipActionDelay = cfg.FlipActionDelay.Value;
            if (cfg.CommandDelayMs.HasValue) CommandDelayMs = cfg.CommandDelayMs.Value;
            if (cfg.BedSpamClickDelay.HasValue) BedSpamClickDelay = cfg.BedSpamClickDelay.Value;
            if (cfg.BazaarOrderCheckIntervalSeconds.HasValue) BazaarOrderCheckIntervalSeconds = cfg.BazaarOrderCheckIntervalSeconds.Value;
            if (cfg.BazaarOrderCancelMinutes.HasValue) BazaarOrderCancelMinutes = cfg.BazaarOrderCancelMinutes.Value;
            if (cfg.BedSpam.HasValue) BedSpam = cfg.BedSpam.Value;
            if (cfg.UseCoflChat.HasValue) UseCoflChat = cfg.UseCoflChat.Value;
            if (cfg.Fastbuy.HasValue) Fastbuy = cfg.Fastbuy.Value;
            if (cfg.AutoCookie.HasValue) AutoCookie = cfg.AutoCookie.Value;
            if (cfg.AuctionDurationHours.HasValue) AuctionDurationHours = cfg.AuctionDurationHours.Value;
            if (cfg.WebGuiPort.HasValue) WebGuiPort = cfg.WebGuiPort.Value;
            if (cfg.ProxyEnabled.HasValue) ProxyEnabled = cfg.ProxyEnabled.Value;
            if (cfg.WebhookUrl != null) WebhookUrl = cfg.WebhookUrl;
            if (cfg.Proxy != null) Proxy = cfg.Proxy;
            if (cfg.DiscordChannelId.HasValue) DiscordChannelId = cfg.DiscordChannelId.Value.ToString();

            if (cfg.Skip != null)
            {
                if (cfg.Skip.Always.HasValue) SkipAlways = cfg.Skip.Always.Value;
                if (cfg.Skip.MinProfit.HasValue) SkipMinProfit = cfg.Skip.MinProfit.Value;
                if (cfg.Skip.UserFinder.HasValue) SkipUserFinder = cfg.Skip.UserFinder.Value;
                if (cfg.Skip.Skins.HasValue) SkipSkins = cfg.Skip.Skins.Value;
                if (cfg.Skip.ProfitPercentage.HasValue) SkipProfitPercentage = cfg.Skip.ProfitPercentage.Value;
                if (cfg.Skip.MinPrice.HasValue) SkipMinPrice = cfg.Skip.MinPrice.Value;
            }

            ApplyStatusText = "Loaded from backend";
        });
    }

    private async void Apply()
    {
        var dto = new ConfigDto
        {
            EnableAhFlips = EnableAhFlips,
            EnableBazaarFlips = EnableBazaarFlips,
            FlipActionDelay = FlipActionDelay,
            CommandDelayMs = CommandDelayMs,
            BedSpamClickDelay = BedSpamClickDelay,
            BazaarOrderCheckIntervalSeconds = BazaarOrderCheckIntervalSeconds,
            BazaarOrderCancelMinutes = BazaarOrderCancelMinutes,
            BedSpam = BedSpam,
            UseCoflChat = UseCoflChat,
            Fastbuy = Fastbuy,
            AutoCookie = AutoCookie,
            AuctionDurationHours = AuctionDurationHours,
            WebhookUrl = WebhookUrl,
            WebGuiPort = WebGuiPort,
            ProxyEnabled = ProxyEnabled,
            Proxy = string.IsNullOrEmpty(Proxy) ? null : Proxy,
            // Do not include Discord bot token in saved configs (tokens are private/local)
            DiscordBotToken = null,
            DiscordChannelId = long.TryParse(DiscordChannelId, out var chId) ? chId : null,
            Skip = new SkipDto
            {
                Always = SkipAlways,
                MinProfit = SkipMinProfit,
                UserFinder = SkipUserFinder,
                Skins = SkipSkins,
                ProfitPercentage = SkipProfitPercentage,
                MinPrice = SkipMinPrice,
            },
        };

        var ok = await _backend.UpdateConfigAsync(dto);
        ApplyStatusText = ok ? "Applied ✓" : "Failed ✗";
    }

    private async Task RefreshNamedConfigsAsync()
    {
        if (IsBusy) return;
        IsBusy = true;
        try
        {
            var backendTask = _backend.GetNamedConfigsAsync();
            var localList = ListLocalConfigs();

            var backendList = await backendTask;

            var merged = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (backendList != null) foreach (var n in backendList) merged.Add(n);
            foreach (var n in localList) merged.Add(n);

            Dispatcher.UIThread.Post(() =>
            {
                _namedConfigs.Clear();
                foreach (var n in merged.OrderBy(x => x)) _namedConfigs.Add(n);
                // Restore last-used if present
                var last = ReadLastUsedConfigName();
                if (!string.IsNullOrEmpty(last) && _namedConfigs.Contains(last) && SelectedNamedConfig == null)
                    SelectedNamedConfig = last;
            });
        }
        finally { IsBusy = false; }
    }

    private async Task SaveNamedConfigAsync()
    {
        if (IsBusy) return;
        IsBusy = true;
        try
        {
            var name = ConfigName?.Trim();
            if (string.IsNullOrEmpty(name))
            {
                ApplyStatusText = "Enter a name to save";
                return;
            }
            // Build current DTO from UI state and save locally first so saving works offline.
        var dto = new ConfigDto
        {
            EnableAhFlips = EnableAhFlips,
            EnableBazaarFlips = EnableBazaarFlips,
            FlipActionDelay = FlipActionDelay,
            CommandDelayMs = CommandDelayMs,
            BedSpamClickDelay = BedSpamClickDelay,
            BazaarOrderCheckIntervalSeconds = BazaarOrderCheckIntervalSeconds,
            BazaarOrderCancelMinutes = BazaarOrderCancelMinutes,
            BedSpam = BedSpam,
            UseCoflChat = UseCoflChat,
            Fastbuy = Fastbuy,
            AutoCookie = AutoCookie,
            AuctionDurationHours = AuctionDurationHours,
            WebhookUrl = WebhookUrl,
            WebGuiPort = WebGuiPort,
            ProxyEnabled = ProxyEnabled,
            Proxy = string.IsNullOrEmpty(Proxy) ? null : Proxy,
            // When saving named configs, do not include the Discord bot token so configs remain shareable
            DiscordBotToken = null,
            DiscordChannelId = long.TryParse(DiscordChannelId, out var chId) ? chId : null,
            Skip = new SkipDto
            {
                Always = SkipAlways,
                MinProfit = SkipMinProfit,
                UserFinder = SkipUserFinder,
                Skins = SkipSkins,
                ProfitPercentage = SkipProfitPercentage,
                MinPrice = SkipMinPrice,
            },
        };

            var localOk = await SaveLocalConfigAsync(name, dto);
            // Try to also save to backend if available (best-effort)
            var backendOk = await _backend.SaveNamedConfigAsync(name, dto);
            var ok = localOk || backendOk;
            if (ok) WriteLastUsedConfigName(name);
            ApplyStatusText = ok ? $"Saved {name}" : "Save failed";
            if (ok) await RefreshNamedConfigsAsync();
        }
        finally { IsBusy = false; }
    }

    private async Task LoadNamedConfigAsync()
    {
        if (IsBusy) return;
        IsBusy = true;
        try
        {
            var name = SelectedNamedConfig ?? ConfigName?.Trim();
            if (string.IsNullOrEmpty(name))
            {
                ApplyStatusText = "Select a config to load";
                return;
            }
            // Try backend first
            var cfg = await _backend.LoadNamedConfigAsync(name);
            if (cfg == null)
            {
                // Fallback to local file
                cfg = await LoadLocalConfigAsync(name);
                if (cfg == null)
                {
                    ApplyStatusText = "Load failed";
                    return;
                }
            }

            // Apply the loaded config into the UI fields (same behavior as Apply)
            Dispatcher.UIThread.Post(() => ApplyConfigToUi(cfg));
            WriteLastUsedConfigName(name);
            ApplyStatusText = $"Loaded {name}";
        }
        finally { IsBusy = false; }
    }

    private void ApplyConfigToUi(ConfigDto cfg)
    {
        if (cfg.EnableAhFlips.HasValue) EnableAhFlips = cfg.EnableAhFlips.Value;
        if (cfg.EnableBazaarFlips.HasValue) EnableBazaarFlips = cfg.EnableBazaarFlips.Value;
        if (cfg.FlipActionDelay.HasValue) FlipActionDelay = cfg.FlipActionDelay.Value;
        if (cfg.CommandDelayMs.HasValue) CommandDelayMs = cfg.CommandDelayMs.Value;
        if (cfg.BedSpamClickDelay.HasValue) BedSpamClickDelay = cfg.BedSpamClickDelay.Value;
        if (cfg.BazaarOrderCheckIntervalSeconds.HasValue) BazaarOrderCheckIntervalSeconds = cfg.BazaarOrderCheckIntervalSeconds.Value;
        if (cfg.BazaarOrderCancelMinutes.HasValue) BazaarOrderCancelMinutes = cfg.BazaarOrderCancelMinutes.Value;
        if (cfg.BedSpam.HasValue) BedSpam = cfg.BedSpam.Value;
        if (cfg.UseCoflChat.HasValue) UseCoflChat = cfg.UseCoflChat.Value;
        if (cfg.Fastbuy.HasValue) Fastbuy = cfg.Fastbuy.Value;
        if (cfg.AutoCookie.HasValue) AutoCookie = cfg.AutoCookie.Value;
        if (cfg.AuctionDurationHours.HasValue) AuctionDurationHours = cfg.AuctionDurationHours.Value;
        if (cfg.WebGuiPort.HasValue) WebGuiPort = cfg.WebGuiPort.Value;
        if (cfg.ProxyEnabled.HasValue) ProxyEnabled = cfg.ProxyEnabled.Value;
        if (cfg.WebhookUrl != null) WebhookUrl = cfg.WebhookUrl;
        if (cfg.Proxy != null) Proxy = cfg.Proxy;
        if (cfg.DiscordChannelId.HasValue) DiscordChannelId = cfg.DiscordChannelId.Value.ToString();

        if (cfg.Skip != null)
        {
            if (cfg.Skip.Always.HasValue) SkipAlways = cfg.Skip.Always.Value;
            if (cfg.Skip.MinProfit.HasValue) SkipMinProfit = cfg.Skip.MinProfit.Value;
            if (cfg.Skip.UserFinder.HasValue) SkipUserFinder = cfg.Skip.UserFinder.Value;
            if (cfg.Skip.Skins.HasValue) SkipSkins = cfg.Skip.Skins.Value;
            if (cfg.Skip.ProfitPercentage.HasValue) SkipProfitPercentage = cfg.Skip.ProfitPercentage.Value;
            if (cfg.Skip.MinPrice.HasValue) SkipMinPrice = cfg.Skip.MinPrice.Value;
        }
    }

    // --- Local storage helpers ---
    private string GetLocalConfigsDir()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var dir = Path.Combine(appData, "Frikadellen", "configs");
        Directory.CreateDirectory(dir);
        return dir;
    }

    private string SanitizeFileName(string name)
    {
        foreach (var c in Path.GetInvalidFileNameChars()) name = name.Replace(c, '_');
        return name;
    }

    private async Task<bool> SaveLocalConfigAsync(string name, ConfigDto dto)
    {
        try
        {
            var dir = GetLocalConfigsDir();
            var file = Path.Combine(dir, SanitizeFileName(name) + ".json");
            var json = JsonSerializer.Serialize(dto, new JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(file, json);
            return true;
        }
        catch { return false; }
    }

    private async Task<ConfigDto?> LoadLocalConfigAsync(string name)
    {
        try
        {
            var dir = GetLocalConfigsDir();
            var file = Path.Combine(dir, SanitizeFileName(name) + ".json");
            if (!File.Exists(file)) return null;
            var json = await File.ReadAllTextAsync(file);
            return JsonSerializer.Deserialize<ConfigDto>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        }
        catch { return null; }
    }

    private List<string> ListLocalConfigs()
    {
        try
        {
            var dir = GetLocalConfigsDir();
            var files = Directory.GetFiles(dir, "*.json");
            return files.Select(f => Path.GetFileNameWithoutExtension(f)).ToList();
        }
        catch { return new List<string>(); }
    }

    private async Task DeleteNamedConfigAsync()
    {
        if (IsBusy) return;
        IsBusy = true;
        try
        {
            var name = SelectedNamedConfig ?? ConfigName?.Trim();
            if (string.IsNullOrEmpty(name)) { ApplyStatusText = "Select a config to delete"; return; }

            // Delete local file
            var dir = GetLocalConfigsDir();
            var file = Path.Combine(dir, SanitizeFileName(name) + ".json");
            var localOk = false;
            try { if (File.Exists(file)) { File.Delete(file); localOk = true; } }
            catch { localOk = false; }

            // Try backend delete (best-effort)
            var backendOk = await _backend.DeleteNamedConfigAsync(name);

            var ok = localOk || backendOk;
            if (ok)
            {
                // Clear last-used if it was this
                var last = ReadLastUsedConfigName();
                if (!string.IsNullOrEmpty(last) && string.Equals(last, name, StringComparison.OrdinalIgnoreCase))
                    WriteLastUsedConfigName(string.Empty);
                ApplyStatusText = $"Deleted {name}";
                await RefreshNamedConfigsAsync();
            }
            else
            {
                ApplyStatusText = "Delete failed";
            }
        }
        finally { IsBusy = false; }
    }

    private void WriteLastUsedConfigName(string name)
    {
        try
        {
            var dir = GetLocalConfigsDir();
            File.WriteAllText(Path.Combine(dir, "last_config.txt"), name);
        }
        catch { }
    }

    private string? ReadLastUsedConfigName()
    {
        try
        {
            var dir = GetLocalConfigsDir();
            var file = Path.Combine(dir, "last_config.txt");
            if (!File.Exists(file)) return null;
            return File.ReadAllText(file).Trim();
        }
        catch { return null; }
    }

    private void ResetDefaults()
    {
        EnableAhFlips = true;
        EnableBazaarFlips = true;
        FlipActionDelay = 150;
        CommandDelayMs = 500;
        BedSpamClickDelay = 100;
        BazaarOrderCheckIntervalSeconds = 30;
        BazaarOrderCancelMinutes = 5;
        BedSpam = false;
        UseCoflChat = true;
        Fastbuy = false;
        AutoCookie = 0;
        AuctionDurationHours = 24;
        SkipMinProfit = 1_000_000;
        SkipProfitPercentage = 50.0;
        SkipMinPrice = 10_000_000;
        SkipAlways = false;
        SkipUserFinder = false;
        SkipSkins = false;
        WebhookUrl = string.Empty;
        WebGuiPort = 8080;
        ProxyEnabled = false;
        Proxy = string.Empty;
        // Do not reset the locally stored Discord bot token here — it's managed in the Notifier tab
        DiscordChannelId = string.Empty;
        ApplyStatusText = "Defaults restored";
    }
}
