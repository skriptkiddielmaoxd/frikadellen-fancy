using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading;
using System.Threading.Tasks;
using Frikadellen.UI.Models;

namespace Frikadellen.UI.Services;

// ────────── DTOs (mirrors frikadellen-fancy REST API) ──────────

/// <summary>Response from GET /api/stats</summary>
public record StatsDto(
    long   SessionProfit,
    long   TotalCoinsSpent,
    long   TotalCoinsEarned,
    int    TotalFlips,
    int    WinCount,
    int    LossCount)
{
    public double WinRate => TotalFlips > 0
        ? Math.Round(WinCount / (double)TotalFlips * 100, 1)
        : 0.0;
}

/// <summary>Single flip entry from GET /api/flips</summary>
public record FlipDto(
    string ItemName,
    long   BuyPrice,
    long   SellPrice,
    long?  BuySpeedMs,
    string Finder,
    string? ItemTag,
    DateTimeOffset Timestamp);

/// <summary>
/// Thin HTTP client for the frikadellen-fancy Rust backend.
/// All methods fail gracefully when the backend is offline.
/// </summary>
public sealed class BackendClient : IDisposable
{
    private readonly HttpClient _http;

    public BackendClient(string baseUrl = "http://localhost:8080")
    {
        _http = new HttpClient
        {
            BaseAddress = new Uri(baseUrl),
            Timeout     = TimeSpan.FromSeconds(5),
        };
    }

    // ── Status ──

    /// <summary>GET /api/status — returns null when backend is offline.</summary>
    public async Task<string?> GetStatusAsync(CancellationToken ct = default)
    {
        try
        {
            return await _http.GetStringAsync("/api/status", ct);
        }
        catch { return null; }
    }

    // ── Stats ──

    /// <summary>GET /api/stats — returns null when backend is offline.</summary>
    public async Task<StatsDto?> GetStatsAsync(CancellationToken ct = default)
    {
        try
        {
            return await _http.GetFromJsonAsync<StatsDto>("/api/stats", ct);
        }
        catch { return null; }
    }

    /// <summary>GET /api/events — historical event log snapshot.</summary>
    public async Task<List<EventDto>?> GetEventsAsync()
    {
        try
        {
            var resp = await _http.GetAsync("/api/events");
            resp.EnsureSuccessStatusCode();
            var json = await resp.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);
            var evts = doc.RootElement.GetProperty("events");
            return JsonSerializer.Deserialize<List<EventDto>>(evts.GetRawText(), JsonOpts);
        }
        catch { return null; }
    }

    /// <summary>GET /api/stats — session totals (profit, flips, win rate, etc.).</summary>
    public async Task<StatsDto?> GetStatsAsync()
    {
        try
        {
            var resp = await _http.GetAsync("/api/stats");
            resp.EnsureSuccessStatusCode();
            var json = await resp.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<StatsDto>(json, JsonOpts);
        }
        catch { return null; }
    }

    /// <summary>GET /api/flips — recent flip history.</summary>
    public async Task<List<FlipHistoryDto>?> GetFlipsAsync(int limit = 50)
    {
        try
        {
            var resp = await _http.GetAsync($"/api/flips?limit={limit}");
            resp.EnsureSuccessStatusCode();
            var json = await resp.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty("flips", out var flipsEl))
                return JsonSerializer.Deserialize<List<FlipHistoryDto>>(flipsEl.GetRawText(), JsonOpts);
            return null;
        }
        catch { return null; }
    }

    /// <summary>GET /api/configs — list saved named configs.</summary>
    public async Task<List<string>?> GetNamedConfigsAsync()
    {
        try
        {
            var resp = await _http.GetAsync("/api/configs");
            resp.EnsureSuccessStatusCode();
            var json = await resp.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);
            var arr = doc.RootElement.GetProperty("configs");
            return JsonSerializer.Deserialize<List<string>>(arr.GetRawText(), JsonOpts);
        }
        catch { return null; }
    }

    /// <summary>POST /api/configs — save current config under a name.</summary>
    public async Task<bool> SaveNamedConfigAsync(string name)
    {
        try
        {
            var body = JsonSerializer.Serialize(new { name }, JsonOpts);
            var content = new StringContent(body, Encoding.UTF8, "application/json");
            var resp = await _http.PostAsync("/api/configs", content);
            return resp.IsSuccessStatusCode;
        }
        catch { return false; }
    }

    /// <summary>GET /api/flips — returns an empty array when backend is offline.</summary>
    public async Task<FlipDto[]> GetFlipsAsync(CancellationToken ct = default)
    {
        try
        {
            return await _http.GetFromJsonAsync<FlipDto[]>("/api/flips", ct)
                   ?? Array.Empty<FlipDto>();
        }
        catch { return Array.Empty<FlipDto>(); }
    }

    // ── Config ──

    /// <summary>PUT /api/config — fire-and-forget; silently swallows errors.</summary>
    public async Task PutConfigAsync(object configDto, CancellationToken ct = default)
    {
        try
        {
            await _http.PutAsJsonAsync("/api/config", configDto, ct);
        }
        catch { /* backend offline – ignore */ }
    }

    public void Dispose() => _http.Dispose();
}

// ────────────────────────── DTOs ──────────────────────────

/// <summary>Matches the JSON structure from GET /api/status and WS "status" messages.</summary>
public sealed record StatusDto
{
    [JsonPropertyName("state")]
    public string State { get; init; } = "Unknown";

    [JsonPropertyName("purse")]
    public long Purse { get; init; }

    [JsonPropertyName("queueDepth")]
    public int QueueDepth { get; init; }

    [JsonPropertyName("uptimeSecs")]
    public long UptimeSecs { get; init; }

    [JsonPropertyName("player")]
    public string Player { get; init; } = "";

    [JsonPropertyName("running")]
    public bool Running { get; init; }

    [JsonPropertyName("ahFlips")]
    public bool AhFlips { get; init; }

    [JsonPropertyName("bazaarFlips")]
    public bool BazaarFlips { get; init; }

    [JsonPropertyName("allowsCommands")]
    public bool AllowsCommands { get; init; }
}

/// <summary>Matches a single entry from GET /api/events.</summary>
public sealed record EventDto
{
    [JsonPropertyName("timestamp")]
    public long Timestamp { get; init; }

    [JsonPropertyName("kind")]
    public string Kind { get; init; } = "system";

    [JsonPropertyName("message")]
    public string Message { get; init; } = "";
}

/// <summary>Matches the full Rust Config struct (snake_case field names from serde).</summary>
public sealed record ConfigDto
{
    [JsonPropertyName("enable_ah_flips")]
    public bool? EnableAhFlips { get; init; }

    [JsonPropertyName("enable_bazaar_flips")]
    public bool? EnableBazaarFlips { get; init; }

    [JsonPropertyName("flip_action_delay")]
    public long? FlipActionDelay { get; init; }

    [JsonPropertyName("command_delay_ms")]
    public long? CommandDelayMs { get; init; }

    [JsonPropertyName("bed_spam_click_delay")]
    public long? BedSpamClickDelay { get; init; }

    [JsonPropertyName("bazaar_order_check_interval_seconds")]
    public long? BazaarOrderCheckIntervalSeconds { get; init; }

    [JsonPropertyName("bazaar_order_cancel_minutes")]
    public long? BazaarOrderCancelMinutes { get; init; }

    [JsonPropertyName("bed_spam")]
    public bool? BedSpam { get; init; }

    [JsonPropertyName("use_cofl_chat")]
    public bool? UseCoflChat { get; init; }

    [JsonPropertyName("fastbuy")]
    public bool? Fastbuy { get; init; }

    [JsonPropertyName("auto_cookie")]
    public long? AutoCookie { get; init; }

    [JsonPropertyName("auction_duration_hours")]
    public long? AuctionDurationHours { get; init; }

    [JsonPropertyName("skip")]
    public SkipDto? Skip { get; init; }

    [JsonPropertyName("webhook_url")]
    public string? WebhookUrl { get; init; }

    [JsonPropertyName("web_gui_port")]
    public int? WebGuiPort { get; init; }

    [JsonPropertyName("proxy_enabled")]
    public bool? ProxyEnabled { get; init; }

    [JsonPropertyName("proxy")]
    public string? Proxy { get; init; }

    [JsonPropertyName("discord_bot_token")]
    public string? DiscordBotToken { get; init; }

    [JsonPropertyName("discord_channel_id")]
    public long? DiscordChannelId { get; init; }
}

/// <summary>Matches the Rust SkipConfig struct.</summary>
public sealed record SkipDto
{
    [JsonPropertyName("always")]
    public bool? Always { get; init; }

    [JsonPropertyName("min_profit")]
    public long? MinProfit { get; init; }

    [JsonPropertyName("user_finder")]
    public bool? UserFinder { get; init; }

    [JsonPropertyName("skins")]
    public bool? Skins { get; init; }

    [JsonPropertyName("profit_percentage")]
    public double? ProfitPercentage { get; init; }

    [JsonPropertyName("min_price")]
    public long? MinPrice { get; init; }
}

/// <summary>One slot from GET /api/inventory (real Minecraft inventory data).</summary>
public sealed record InventorySlotDto
{
    /// <summary>Item type name (e.g. "minecraft:diamond_sword"). Empty string when slot is empty.</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>Stack count. 0 means the slot is empty.</summary>
    public int Count { get; init; }
}

/// <summary>Matches the JSON structure from GET /api/stats.</summary>
public sealed record StatsDto
{
    [JsonPropertyName("session_profit")]
    public long SessionProfit { get; init; }

    [JsonPropertyName("total_coins_spent")]
    public long TotalCoinsSpent { get; init; }

    [JsonPropertyName("total_coins_earned")]
    public long TotalCoinsEarned { get; init; }

    [JsonPropertyName("total_flips")]
    public int TotalFlips { get; init; }

    [JsonPropertyName("win_count")]
    public int WinCount { get; init; }

    [JsonPropertyName("session_duration_secs")]
    public long SessionDurationSecs { get; init; }
}

/// <summary>Matches one entry from GET /api/flips.</summary>
public sealed record FlipHistoryDto
{
    [JsonPropertyName("item")]
    public string Item { get; init; } = "";

    [JsonPropertyName("buy_price")]
    public long BuyPrice { get; init; }

    [JsonPropertyName("sell_price")]
    public long SellPrice { get; init; }

    [JsonPropertyName("profit")]
    public long Profit { get; init; }

    [JsonPropertyName("outcome")]
    public string Outcome { get; init; } = "pending";

    [JsonPropertyName("timestamp")]
    public long Timestamp { get; init; }

    [JsonPropertyName("buy_speed_ms")]
    public long? BuySpeedMs { get; init; }

    [JsonPropertyName("tag")]
    public string? Tag { get; init; }
}
