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

    // ── Flips ──

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
