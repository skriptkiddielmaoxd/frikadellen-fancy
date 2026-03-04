using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace Frikadellen.UI.Models;

public sealed class EventItem
{
    public string Id { get; init; } = Guid.NewGuid().ToString("N")[..8];
    public DateTime Timestamp { get; init; } = DateTime.Now;
    public string Type { get; init; } = "Info";
    public string Message { get; init; } = string.Empty;
    public string Details { get; init; } = string.Empty;
    public string Tag { get; init; } = "system";
    public string Avatar { get; init; } = "🔵";
}

public sealed class FlipRecord
{
    public string Id { get; init; } = Guid.NewGuid().ToString("N")[..8];
    public DateTime Timestamp { get; init; } = DateTime.Now;
    public string ItemName { get; init; } = string.Empty;
    public string CleanName => MinecraftColorParser.StripCodes(ItemName);
    public long BuyPrice { get; init; }
    public long SellPrice { get; init; }
    public long Profit => SellPrice - BuyPrice;
    public double ProfitPercent => BuyPrice > 0 ? (double)Profit / BuyPrice * 100 : 0;
    public string ProfitFormatted => CoinFormat.WithCoins(Profit);
    public string ProfitPercentFormatted => $"{ProfitPercent:F1}%";
    public string BuyPriceFormatted => CoinFormat.Short(BuyPrice);
    public string SellPriceFormatted => CoinFormat.Short(SellPrice);
    public long? BuySpeedMs { get; init; }
    public string BuySpeedFormatted => BuySpeedMs.HasValue ? $"{BuySpeedMs}ms" : "—";
    public string Finder { get; init; } = "SNIPER";
    public bool IsProfitable => Profit > 0;
    public string ProfitColor => IsProfitable ? "#00B894" : "#FF6B6B";

    /// <summary>Parsed Minecraft color spans for the item name.</summary>
    public List<ChatSpan> NameSpans { get; init; } = new();

    /// <summary>Hypixel item tag (e.g. ENCHANTED_DIAMOND). Used for the icon URL.</summary>
    public string? ItemTag { get; init; }

    /// <summary>Item icon URL from sky.coflnet.com.</summary>
    public string ImageUrl => !string.IsNullOrEmpty(ItemTag)
        ? $"https://sky.coflnet.com/static/icon/{ItemTag}"
        : $"https://sky.coflnet.com/static/icon/{DeriveTag(CleanName)}";

    private static string DeriveTag(string name)
    {
        // Strip star symbols and extra whitespace, uppercase, replace spaces with underscores
        var clean = name.Replace("\u272A", "").Trim();
        return clean.ToUpperInvariant().Replace(' ', '_').Replace("'", "");
    }
}

/// <summary>One of 36 inventory slots displayed in the grid. Extends ViewModelBase for INPC.</summary>
public sealed class InventorySlot : Frikadellen.UI.ViewModels.ViewModelBase
{
    /// <summary>0-based display index (0→top-left, 35→bottom-right).</summary>
    public int Index { get; init; }

    private bool _hasItem;
    public bool HasItem { get => _hasItem; set => SetField(ref _hasItem, value); }

    private string _itemName = string.Empty;
    public string ItemName
    {
        get => _itemName;
        set { if (SetField(ref _itemName, value)) { OnPropertyChanged(nameof(CleanName)); OnPropertyChanged(nameof(ImageUrl)); } }
    }

    public string CleanName => MinecraftColorParser.StripCodes(ItemName);

    private long _sellPrice;
    public long SellPrice { get => _sellPrice; set { if (SetField(ref _sellPrice, value)) OnPropertyChanged(nameof(SellPriceFormatted)); } }

    private long _buyCost;
    public long BuyCost { get => _buyCost; set { if (SetField(ref _buyCost, value)) { OnPropertyChanged(nameof(BuyCostFormatted)); OnPropertyChanged(nameof(ProfitFormatted)); OnPropertyChanged(nameof(ProfitPercentFormatted)); } } }

    private long _profit;
    public long Profit { get => _profit; set { if (SetField(ref _profit, value)) { OnPropertyChanged(nameof(ProfitFormatted)); OnPropertyChanged(nameof(ProfitColor)); OnPropertyChanged(nameof(ProfitPercentFormatted)); } } }

    private string? _itemTag;
    public string? ItemTag { get => _itemTag; set { if (SetField(ref _itemTag, value)) OnPropertyChanged(nameof(ImageUrl)); } }

    private bool _listed;
    public bool Listed { get => _listed; set { if (SetField(ref _listed, value)) { OnPropertyChanged(nameof(StatusText)); OnPropertyChanged(nameof(StatusColor)); OnPropertyChanged(nameof(SlotBorder)); } } }

    private List<ChatSpan> _nameSpans = new();
    public List<ChatSpan> NameSpans { get => _nameSpans; set => SetField(ref _nameSpans, value); }

    // Formatted values for tooltip
    public string SellPriceFormatted => CoinFormat.Short(SellPrice);
    public string BuyCostFormatted => BuyCost > 0 ? CoinFormat.Short(BuyCost) : "—";
    public string ProfitFormatted => BuyCost > 0 ? CoinFormat.WithCoins(Profit) : "—";
    public double ProfitPercent => BuyCost > 0 ? (double)Profit / BuyCost * 100 : 0;
    public string ProfitPercentFormatted => BuyCost > 0 ? $"{ProfitPercent:F1}%" : "";
    public string ProfitColor => Profit > 0 ? "#00B894" : "#FF6B6B";
    public string StatusText => Listed ? "Listed ✓" : "Pending";
    public string StatusColor => Listed ? "#00B894" : "#FFAA00";
    public string SlotBorder => HasItem ? (Listed ? "#00B894" : "#6C5CE7") : "#20FFFFFF";
    public string SlotBackground => HasItem ? "#28FFFFFF" : "#10FFFFFF";
    public string SlotCursor => HasItem ? "Hand" : "Arrow";

    /// <summary>Returns self when occupied (for rich tooltip), null when empty (no tooltip).</summary>
    public InventorySlot? ToolTipData => HasItem ? this : null;

    /// <summary>Item icon URL from sky.coflnet.com.</summary>
    public string ImageUrl => !string.IsNullOrEmpty(ItemTag)
        ? $"https://sky.coflnet.com/static/icon/{ItemTag}"
        : HasItem ? $"https://sky.coflnet.com/static/icon/{DeriveTag(CleanName)}" : "";

    public void Clear()
    {
        HasItem = false;
        ItemName = string.Empty;
        SellPrice = 0;
        BuyCost = 0;
        Profit = 0;
        ItemTag = null;
        Listed = false;
        NameSpans = new();
        OnPropertyChanged(nameof(SlotBackground));
        OnPropertyChanged(nameof(SlotBorder));
        OnPropertyChanged(nameof(SlotCursor));
        OnPropertyChanged(nameof(ToolTipData));
    }

    public void Fill(string itemName, long sellPrice, long buyCost, long profit, string? tag, List<ChatSpan> nameSpans)
    {
        ItemName = itemName;
        SellPrice = sellPrice;
        BuyCost = buyCost;
        Profit = profit;
        ItemTag = tag;
        Listed = false;
        NameSpans = nameSpans;
        HasItem = true;
        OnPropertyChanged(nameof(SlotBackground));
        OnPropertyChanged(nameof(SlotBorder));
        OnPropertyChanged(nameof(SlotCursor));
        OnPropertyChanged(nameof(ToolTipData));
    }

    private static string DeriveTag(string name)
    {
        var clean = name.Replace("\u272A", "").Trim();
        return clean.ToUpperInvariant().Replace(' ', '_').Replace("'", "");
    }
}

public sealed class ChatMessage
{
    public DateTime Timestamp { get; init; } = DateTime.Now;
    public string Sender { get; init; } = string.Empty;
    public string Text { get; init; } = string.Empty;
    public string Color { get; init; } = "#DFE6E9";
    public bool IsSystem { get; init; }

    /// <summary>Parsed Minecraft color spans for rich rendering.</summary>
    public List<ChatSpan> Spans { get; init; } = new();
}

/// <summary>A segment of text with a specific color, parsed from Minecraft § codes.</summary>
public sealed class ChatSpan
{
    public string Text { get; init; } = string.Empty;
    public string Color { get; init; } = "#DFE6E9";
}

/// <summary>Parses Minecraft §-prefixed color codes into a list of ChatSpan segments.</summary>
public static class MinecraftColorParser
{
    private static readonly Dictionary<char, string> ColorMap = new()
    {
        ['0'] = "#555555", // Black (brightened for dark UI)
        ['1'] = "#5555FF", // Dark Blue
        ['2'] = "#55FF55", // Dark Green
        ['3'] = "#55FFFF", // Dark Aqua
        ['4'] = "#FF5555", // Dark Red
        ['5'] = "#FF55FF", // Dark Purple
        ['6'] = "#FFAA00", // Gold
        ['7'] = "#AAAAAA", // Gray
        ['8'] = "#777777", // Dark Gray (brightened)
        ['9'] = "#5555FF", // Blue
        ['a'] = "#55FF55", // Green
        ['b'] = "#55FFFF", // Aqua
        ['c'] = "#FF5555", // Red
        ['d'] = "#FF55FF", // Light Purple
        ['e'] = "#FFFF55", // Yellow
        ['f'] = "#FFFFFF", // White
        ['r'] = "#DFE6E9", // Reset (default text color)
    };

    /// <summary>
    /// Parse a Minecraft chat string with § color codes into a list of colored spans.
    /// Formatting codes (§l, §m, §n, §o, §k) are stripped.
    /// </summary>
    public static List<ChatSpan> Parse(string text, string defaultColor = "#DFE6E9")
    {
        var spans = new List<ChatSpan>();
        var currentColor = defaultColor;
        var sb = new System.Text.StringBuilder();

        for (int i = 0; i < text.Length; i++)
        {
            if (text[i] == '§' && i + 1 < text.Length)
            {
                var code = char.ToLower(text[i + 1]);
                i++; // skip the code character

                // Formatting codes — just skip them
                if (code is 'l' or 'm' or 'n' or 'o' or 'k')
                    continue;

                // Color code — flush current span, switch color
                if (ColorMap.TryGetValue(code, out var newColor))
                {
                    if (sb.Length > 0)
                    {
                        spans.Add(new ChatSpan { Text = sb.ToString(), Color = currentColor });
                        sb.Clear();
                    }
                    currentColor = newColor;
                }
                continue;
            }
            sb.Append(text[i]);
        }

        // Flush remaining text
        if (sb.Length > 0)
            spans.Add(new ChatSpan { Text = sb.ToString(), Color = currentColor });

        // Fallback: if no codes were found, return a single span with the full text
        if (spans.Count == 0 && text.Length > 0)
            spans.Add(new ChatSpan { Text = text, Color = defaultColor });

        return spans;
    }

    /// <summary>Strip all § color/formatting codes and return plain text.</summary>
    public static string StripCodes(string text)
    {
        var sb = new System.Text.StringBuilder(text.Length);
        for (int i = 0; i < text.Length; i++)
        {
            if (text[i] == '§' && i + 1 < text.Length)
            {
                i++;
                continue;
            }
            sb.Append(text[i]);
        }
        return sb.ToString();
    }
}

public sealed class UiSettings
{
    public string Token { get; set; } = string.Empty;
    public string AllowedChannelId { get; set; } = string.Empty;
    public string PublishPath { get; set; } = string.Empty;
    public bool DarkMode { get; set; } = true;
}

public static class CoinFormat
{
    public static string Short(long value)
    {
        var abs = Math.Abs(value);
        var sign = value < 0 ? "-" : "";

        if (abs >= 1_000_000_000L)
        {
            var b = abs / 1_000_000_000.0;
            var fmt = b >= 100 ? "F1" : b >= 10 ? "F2" : "F3";
            return $"{sign}{b.ToString(fmt)}b";
        }
        if (abs >= 1_000_000L)
        {
            var m = abs / 1_000_000.0;
            var fmt = m >= 100 ? "F1" : m >= 10 ? "F2" : "F3";
            return $"{sign}{m.ToString(fmt)}m";
        }
        return $"{value:N0}";
    }

    public static string WithCoins(long value) => $"{Short(value)} coins";
}

/// <summary>Cleans up Rust tracing log lines for display in the UI chat.</summary>
public static partial class LogLineCleaner
{
    // ANSI escape sequences  (e.g. \x1b[32m, \x1b[0m, \x1b[2m)
    [GeneratedRegex(@"\x1B\[[0-9;]*[A-Za-z]")]
    private static partial Regex AnsiRegex();

    // tracing timestamp + level prefix, e.g.  "2026-03-03T15:23:45.123Z  WARN "
    [GeneratedRegex(@"^\d{4}-\d{2}-\d{2}T[\d:.]+Z?\s+(TRACE|DEBUG|INFO|WARN|ERROR)\s*")]
    private static partial Regex TracingPrefixRegex();

    /// <summary>
    /// Strip ANSI escape codes, tracing timestamp/level prefix, and trim the line.
    /// Returns null if the resulting line is empty or pure noise.
    /// </summary>
    public static string? Clean(string raw)
    {
        // 1. Strip ANSI escape codes
        var line = AnsiRegex().Replace(raw, "");

        // 2. Strip tracing prefix (timestamp + level)
        line = TracingPrefixRegex().Replace(line, "");

        // 3. Trim whitespace
        line = line.Trim();

        // 4. Drop empty or noise-only lines
        if (string.IsNullOrWhiteSpace(line)) return null;

        return line;
    }

    /// <summary>
    /// Extract log level from a raw tracing line. Returns "WARN", "ERROR", "INFO", etc.
    /// Falls back to "INFO" if no level is detected.
    /// </summary>
    public static string ExtractLevel(string raw)
    {
        // Strip ANSI first so level keywords match cleanly
        var clean = AnsiRegex().Replace(raw, "");
        var m = TracingPrefixRegex().Match(clean);
        return m.Success ? m.Groups[1].Value : "INFO";
    }
}
