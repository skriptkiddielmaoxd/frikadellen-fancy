using System;
using System.Windows.Input;
using Frikadellen.UI.Models;
using Frikadellen.UI.Services;

namespace Frikadellen.UI.ViewModels;

public sealed class ConfigViewModel : ViewModelBase
{
    private readonly SettingsService _svc;
    private UiSettings _cfg;
    private string _saveStatus = "";

    public ConfigViewModel(SettingsService svc)
    {
        _svc = svc;
        _cfg = svc.Load();
        SaveCommand = new RelayCommand(Save);
    }

    // ── Flip toggles ──
    public bool EnableAhFlips      { get => _cfg.EnableAhFlips;      set { _cfg.EnableAhFlips = value;      OnPropertyChanged(); } }
    public bool EnableBazaarFlips  { get => _cfg.EnableBazaarFlips;  set { _cfg.EnableBazaarFlips = value;  OnPropertyChanged(); } }

    // ── Timing ──
    public int FlipActionDelay                { get => _cfg.FlipActionDelay;                set { _cfg.FlipActionDelay = value;                OnPropertyChanged(); } }
    public int CommandDelayMs                 { get => _cfg.CommandDelayMs;                 set { _cfg.CommandDelayMs = value;                 OnPropertyChanged(); } }
    public int BedSpamClickDelay              { get => _cfg.BedSpamClickDelay;              set { _cfg.BedSpamClickDelay = value;              OnPropertyChanged(); } }
    public int BazaarOrderCheckIntervalSeconds{ get => _cfg.BazaarOrderCheckIntervalSeconds; set { _cfg.BazaarOrderCheckIntervalSeconds = value; OnPropertyChanged(); } }
    public int BazaarOrderCancelMinutes       { get => _cfg.BazaarOrderCancelMinutes;       set { _cfg.BazaarOrderCancelMinutes = value;       OnPropertyChanged(); } }
    public int AuctionDurationHours           { get => _cfg.AuctionDurationHours;           set { _cfg.AuctionDurationHours = value;           OnPropertyChanged(); } }

    // ── Behaviour ──
    public bool BedSpam    { get => _cfg.BedSpam;    set { _cfg.BedSpam = value;    OnPropertyChanged(); } }
    public bool UseCoflChat{ get => _cfg.UseCoflChat; set { _cfg.UseCoflChat = value; OnPropertyChanged(); } }
    public bool Fastbuy    { get => _cfg.Fastbuy;    set { _cfg.Fastbuy = value;    OnPropertyChanged(); } }
    public int  AutoCookie { get => _cfg.AutoCookie; set { _cfg.AutoCookie = value; OnPropertyChanged(); } }

    // ── Skip filter ──
    public long   SkipMinProfit         { get => _cfg.SkipMinProfit;         set { _cfg.SkipMinProfit = value;         OnPropertyChanged(); } }
    public double SkipProfitPercentage  { get => _cfg.SkipProfitPercentage;  set { _cfg.SkipProfitPercentage = value;  OnPropertyChanged(); } }
    public long   SkipMinPrice          { get => _cfg.SkipMinPrice;          set { _cfg.SkipMinPrice = value;          OnPropertyChanged(); } }
    public bool   SkipAlways            { get => _cfg.SkipAlways;            set { _cfg.SkipAlways = value;            OnPropertyChanged(); } }
    public bool   SkipUserFinder        { get => _cfg.SkipUserFinder;        set { _cfg.SkipUserFinder = value;        OnPropertyChanged(); } }
    public bool   SkipSkins             { get => _cfg.SkipSkins;             set { _cfg.SkipSkins = value;             OnPropertyChanged(); } }

    // ── Network ──
    public int    WebGuiPort    { get => _cfg.WebGuiPort;    set { _cfg.WebGuiPort = value;    OnPropertyChanged(); } }
    public string WebhookUrl    { get => _cfg.WebhookUrl;    set { _cfg.WebhookUrl = value;    OnPropertyChanged(); } }
    public bool   ProxyEnabled  { get => _cfg.ProxyEnabled;  set { _cfg.ProxyEnabled = value;  OnPropertyChanged(); } }
    public string Proxy         { get => _cfg.Proxy;         set { _cfg.Proxy = value;         OnPropertyChanged(); } }

    // ── Discord ──
    public string DiscordChannelId { get => _cfg.DiscordChannelId; set { _cfg.DiscordChannelId = value; OnPropertyChanged(); } }

    // ── Anti-Detection ──
    public bool   AntiDetectionEnabled          { get => _cfg.AntiDetectionEnabled;          set { _cfg.AntiDetectionEnabled = value;          OnPropertyChanged(); } }
    public bool   EnableJitter                  { get => _cfg.EnableJitter;                  set { _cfg.EnableJitter = value;                  OnPropertyChanged(); } }
    public int    JitterMinMs                   { get => _cfg.JitterMinMs;                   set { _cfg.JitterMinMs = value;                   OnPropertyChanged(); } }
    public int    JitterMaxMs                   { get => _cfg.JitterMaxMs;                   set { _cfg.JitterMaxMs = value;                   OnPropertyChanged(); } }
    public bool   EnableDummyActivity           { get => _cfg.EnableDummyActivity;           set { _cfg.EnableDummyActivity = value;           OnPropertyChanged(); } }
    public int    DummyActivityIntervalSeconds  { get => _cfg.DummyActivityIntervalSeconds;  set { _cfg.DummyActivityIntervalSeconds = value;  OnPropertyChanged(); } }
    public bool   EnableHumanization            { get => _cfg.EnableHumanization;            set { _cfg.EnableHumanization = value;            OnPropertyChanged(); } }
    public double HumanizationStrength          { get => _cfg.HumanizationStrength;          set { _cfg.HumanizationStrength = value;          OnPropertyChanged(); OnPropertyChanged(nameof(HumanizationStrengthPercent)); } }
    public int    HumanizationStrengthPercent   => (int)Math.Round(_cfg.HumanizationStrength * 100);
    public bool   RandomizeClickPosition        { get => _cfg.RandomizeClickPosition;        set { _cfg.RandomizeClickPosition = value;        OnPropertyChanged(); } }
    public bool   EnableFakeMovement            { get => _cfg.EnableFakeMovement;            set { _cfg.EnableFakeMovement = value;            OnPropertyChanged(); } }
    public int    MaxActionsPerMinute           { get => _cfg.MaxActionsPerMinute;           set { _cfg.MaxActionsPerMinute = value;           OnPropertyChanged(); } }

    // ── Save ──
    public string SaveStatus
    {
        get => _saveStatus;
        set => SetField(ref _saveStatus, value);
    }

    public ICommand SaveCommand { get; }

    private async void Save()
    {
        _svc.Save(_cfg);
        SaveStatus = "Saved ✓";
        await System.Threading.Tasks.Task.Delay(2000);
        SaveStatus = "";
    }
}
