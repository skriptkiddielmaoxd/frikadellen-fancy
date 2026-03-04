using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace Frikadellen.UI.Services;

/// <summary>
/// HTTP client for the Rust backend REST API running on localhost.
/// All methods swallow exceptions and return null/false on failure so the
/// UI remains functional even when the backend is unreachable.
/// </summary>
public sealed class BackendClient : IDisposable
{
    private readonly HttpClient _http;

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public int Port { get; }

    public BackendClient(int port = 8080)
    {
        Port = port;
        _http = new HttpClient
        {
            BaseAddress = new Uri($"http://127.0.0.1:{port}"),
            Timeout = TimeSpan.FromSeconds(5)
        };
    }

    /// <summary>GET /api/status — core telemetry for the dashboard.</summary>
    public async Task<StatusDto?> GetStatusAsync()
    {
        try
        {
            var resp = await _http.GetAsync("/api/status");
            resp.EnsureSuccessStatusCode();
            var json = await resp.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<StatusDto>(json, JsonOpts);
        }
        catch { return null; }
    }

    /// <summary>GET /api/config — full config from the Rust backend.</summary>
    public async Task<ConfigDto?> GetConfigAsync()
    {
        try
        {
            var resp = await _http.GetAsync("/api/config");
            resp.EnsureSuccessStatusCode();
            var json = await resp.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<ConfigDto>(json, JsonOpts);
        }
        catch { return null; }
    }

    /// <summary>PUT /api/config — persist config changes to disk.</summary>
    public async Task<bool> UpdateConfigAsync(ConfigDto config)
    {
        try
        {
            var json = JsonSerializer.Serialize(config, JsonOpts);
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            var resp = await _http.PutAsync("/api/config", content);
            return resp.IsSuccessStatusCode;
        }
        catch { return false; }
    }

    /// <summary>POST /api/command — send a chat/cofl/slash command.</summary>
    public async Task<bool> SendCommandAsync(string command)
    {
        try
        {
            var body = JsonSerializer.Serialize(new { command }, JsonOpts);
            var content = new StringContent(body, Encoding.UTF8, "application/json");
            var resp = await _http.PostAsync("/api/command", content);
            return resp.IsSuccessStatusCode;
        }
        catch { return false; }
    }

    /// <summary>POST /api/toggle — flip the global running flag.</summary>
    public async Task<bool?> ToggleRunningAsync()
    {
        try
        {
            var resp = await _http.PostAsync("/api/toggle", null);
            resp.EnsureSuccessStatusCode();
            var json = await resp.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);
            return doc.RootElement.GetProperty("running").GetBoolean();
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
