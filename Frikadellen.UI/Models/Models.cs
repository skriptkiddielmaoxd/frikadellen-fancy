using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Frikadellen.UI.Models;

// ────────── Event / flip feed ──────────

public class EventItem : INotifyPropertyChanged
{
    private bool _isExpanded;

    public string Type { get; set; } = "";
    public string Message { get; set; } = "";
    public string Tag { get; set; } = "";
    public string Avatar { get; set; } = "🔵";
    public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.Now;

    public bool IsExpanded
    {
        get => _isExpanded;
        set { _isExpanded = value; OnPropertyChanged(); }
    }

    public string TimeLabel => Timestamp.ToString("HH:mm:ss");

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? n = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
}

public class FlipRecord
{
    public string ItemName { get; set; } = "";
    public long BuyPrice { get; set; }
    public long SellPrice { get; set; }
    public long Profit => SellPrice - BuyPrice;
    public long? BuySpeedMs { get; set; }
    public string Finder { get; set; } = "SNIPER";
    public string? ItemTag { get; set; }

    public string ProfitLabel => $"+{Profit:N0}";
    public string BuyLabel => $"{BuyPrice:N0}";
    public string SellLabel => $"{SellPrice:N0}";
    public string SpeedLabel => BuySpeedMs.HasValue ? $"{BuySpeedMs}ms" : "—";
}

// ────────── Stats ──────────

public class StatCard : INotifyPropertyChanged
{
    private string _value = "—";
    private string _trend = "";
    private bool _trendPositive = true;

    public string Label { get; set; } = "";
    public string Icon { get; set; } = "📊";
    public string AccentColor { get; set; } = "#818CF8";

    public string Value
    {
        get => _value;
        set { _value = value; OnPropertyChanged(); }
    }

    public string Trend
    {
        get => _trend;
        set { _trend = value; OnPropertyChanged(); }
    }

    public bool TrendPositive
    {
        get => _trendPositive;
        set { _trendPositive = value; OnPropertyChanged(); }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? n = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
}

// ────────── UI Settings persisted to disk ──────────

public class UiSettings
{
    public string MinecraftUsername { get; set; } = "";
    public string DiscordBotToken { get; set; } = "";
    public string DiscordChannelId { get; set; } = "";
    public string WebhookUrl { get; set; } = "";
    public bool FirstRunComplete { get; set; } = false;

    // Config fields (mirrors config.toml)
    public bool EnableAhFlips { get; set; } = true;
    public bool EnableBazaarFlips { get; set; } = false;
    public int FlipActionDelay { get; set; } = 150;
    public int CommandDelayMs { get; set; } = 500;
    public int BedSpamClickDelay { get; set; } = 100;
    public int BazaarOrderCheckIntervalSeconds { get; set; } = 30;
    public int BazaarOrderCancelMinutes { get; set; } = 5;
    public int AuctionDurationHours { get; set; } = 24;
    public bool BedSpam { get; set; } = false;
    public bool UseCoflChat { get; set; } = true;
    public bool Fastbuy { get; set; } = false;
    public int AutoCookie { get; set; } = 0;
    public int WebGuiPort { get; set; } = 8080;
    public bool ProxyEnabled { get; set; } = false;
    public string Proxy { get; set; } = "";
    public long SkipMinProfit { get; set; } = 1_000_000;
    public double SkipProfitPercentage { get; set; } = 50.0;
    public long SkipMinPrice { get; set; } = 10_000_000;
    public bool SkipAlways { get; set; } = false;
    public bool SkipUserFinder { get; set; } = false;
    public bool SkipSkins { get; set; } = false;

    // ── Anti-detection ([anti_detection] in config.toml) ──
    public bool AntiDetectionEnabled { get; set; } = false;
    public bool EnableJitter { get; set; } = true;
    public int JitterMinMs { get; set; } = 20;
    public int JitterMaxMs { get; set; } = 120;
    public bool EnableDummyActivity { get; set; } = true;
    public int DummyActivityIntervalSeconds { get; set; } = 45;
    public bool EnableHumanization { get; set; } = true;
    public double HumanizationStrength { get; set; } = 0.5;
    public bool RandomizeClickPosition { get; set; } = false;
    public bool EnableFakeMovement { get; set; } = false;
    public int MaxActionsPerMinute { get; set; } = 60;
}

// ────────── Helpers ──────────

public static class Fmt
{
    public static string Coins(long v)
    {
        if (v >= 1_000_000_000) return $"{v / 1_000_000_000.0:0.##}B";
        if (v >= 1_000_000)     return $"{v / 1_000_000.0:0.##}M";
        if (v >= 1_000)         return $"{v / 1_000.0:0.##}K";
        return v.ToString("N0");
    }
}

// ────────── Toast ──────────

public enum ToastType { Success, Warning, Error, Info }

public class ToastItem : INotifyPropertyChanged
{
    public string Message { get; set; } = "";
    public ToastType Type { get; set; }
    public string TypeIcon => Type switch { ToastType.Success => "✓", ToastType.Warning => "⚠", ToastType.Error => "✕", _ => "ℹ" };
    public string TypeColor => Type switch { ToastType.Success => "#34D399", ToastType.Warning => "#FBBF24", ToastType.Error => "#F87171", _ => "#38BDF8" };
    private bool _isVisible = true;
    public bool IsVisible { get => _isVisible; set { _isVisible = value; OnPropertyChanged(); } }
    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? n = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
}

// ────────── FlipRecordEx ──────────

public class FlipRecordEx : FlipRecord
{
    public string Rarity { get; set; } = "RARE";
    public string RarityColor => Rarity switch { "COMMON" => "#FFFFFF", "UNCOMMON" => "#55FF55", "RARE" => "#5555FF", "EPIC" => "#AA00AA", "LEGENDARY" => "#FFAA00", "MYTHIC" => "#FF55FF", "SPECIAL" => "#FF5555", _ => "#AAAAAA" };
    public string ProfitPercent => BuyPrice > 0 ? $"{(Profit * 100.0 / BuyPrice):0.#}%" : "—";
    public bool IsWin => Profit > 0;
    public bool IsExpanded { get; set; }
}

// ────────── BazaarOrder ──────────

public class BazaarOrder
{
    public string ItemName { get; set; } = "";
    public string OrderType { get; set; } = "BUY";
    public long Price { get; set; }
    public int Amount { get; set; }
    public string Status { get; set; } = "ACTIVE";
    public DateTimeOffset PlacedAt { get; set; }
    public string StatusColor => Status switch { "FILLED" => "#34D399", "CANCELLED" => "#F87171", _ => "#FBBF24" };
    public string PriceLabel => Fmt.Coins(Price);
}

// ────────── AnalyticsData ──────────

public class AnalyticsData
{
    public long TotalProfit { get; set; }
    public long AvgProfitPerFlip { get; set; }
    public string BestFlipItem { get; set; } = "";
    public long BestFlipProfit { get; set; }
    public double FlipsPerHour { get; set; }
    public double AvgBuySpeedMs { get; set; }
    public double WinRate { get; set; }
    public List<(string Item, int Count, long TotalProfit, long AvgProfit, long BestProfit)> TopItems { get; set; } = new();
}

// ────────── TopItemEntry ──────────

public class TopItemEntry
{
    public int Rank { get; set; }
    public string ItemName { get; set; } = "";
    public int TimesFlipped { get; set; }
    public string TotalProfitLabel { get; set; } = "";
    public string AvgProfitLabel { get; set; } = "";
    public string BestProfitLabel { get; set; } = "";
    public string RankColor => Rank switch { 1 => "#FFAA00", 2 => "#C0C0C0", 3 => "#CD7F32", _ => "#6B7280" };
    public string RankLabel => Rank switch { 1 => "🥇", 2 => "🥈", 3 => "🥉", _ => $"#{Rank}" };
}
