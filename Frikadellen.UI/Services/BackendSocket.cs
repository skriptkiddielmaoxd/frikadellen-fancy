using System;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

namespace Frikadellen.UI.Services;

/// <summary>
/// Persistent WebSocket client that connects to the Rust backend's /ws endpoint
/// and raises events for real-time UI streaming. Automatically reconnects on
/// disconnect with a 500ms back-off.
/// </summary>
public sealed class BackendSocket : IDisposable
{
    private ClientWebSocket? _ws;
    private CancellationTokenSource? _cts;
    private readonly int _port;
    private bool _disposed;

    /// <summary>If no message is received within this interval, treat the
    /// connection as dead and reconnect. The Rust backend sends status
    /// every 1.5s, so 10s of silence is very abnormal.</summary>
    private static readonly TimeSpan ReceiveTimeout = TimeSpan.FromSeconds(10);

    public bool IsConnected => _ws?.State == WebSocketState.Open;

    /// <summary>Periodic status snapshot from the broadcast channel.</summary>
    public event Action<StatusDto>? StatusReceived;

    /// <summary>Bot event (chat, system, error).</summary>
    public event Action<string, string>? EventReceived;

    /// <summary>AH flip purchased: item, cost, target, profit, buySpeedMs, tag.</summary>
    public event Action<string, long, long, long, long?, string?>? FlipReceived;

    /// <summary>Relist/createAuction: item, sellPrice, buyCost, profit, duration, slot, tag.</summary>
    public event Action<string, long, long, long, long, int, string?>? RelistReceived;

    /// <summary>Bazaar flip: item, amount, pricePerUnit, isBuy.</summary>
    public event Action<string, int, long, bool>? BazaarFlipReceived;

    /// <summary>Connection state change.</summary>
    public event Action<bool>? ConnectionChanged;

    public BackendSocket(int port = 8080)
    {
        _port = port;
    }

    /// <summary>
    /// Start the connection loop. Runs in the background and will automatically
    /// reconnect until <see cref="Disconnect"/> is called.
    /// </summary>
    public void ConnectAsync()
    {
        _cts?.Cancel();
        _cts?.Dispose();
        _cts = new CancellationTokenSource();
        var token = _cts.Token;

        _ = Task.Run(async () =>
        {
            var uri = new Uri($"ws://127.0.0.1:{_port}/ws");

            while (!token.IsCancellationRequested)
            {
                ClientWebSocket? ws = null;
                try
                {
                    ws = new ClientWebSocket();
                    ws.Options.KeepAliveInterval = TimeSpan.FromSeconds(5);
                    _ws = ws;
                    await ws.ConnectAsync(uri, token);
                    ConnectionChanged?.Invoke(true);

                    await ReceiveLoopAsync(ws, token);
                }
                catch (OperationCanceledException) { break; }
                catch
                {
                    // Connection failed or dropped — will retry
                }
                finally
                {
                    _ws = null;
                    ConnectionChanged?.Invoke(false);
                    try { ws?.Dispose(); }
                    catch { /* ignore dispose errors */ }
                }

                // Back-off before reconnect (shorter if we had a working connection)
                try { await Task.Delay(500, token); }
                catch (OperationCanceledException) { break; }
            }
        }, token);
    }

    private async Task ReceiveLoopAsync(ClientWebSocket ws, CancellationToken token)
    {
        var buf = new byte[16384];
        var sb = new StringBuilder();

        while (ws.State == WebSocketState.Open && !token.IsCancellationRequested)
        {
            // Timeout each receive to detect silently-dead connections
            // (e.g. during Minecraft warps where the Rust process may stall).
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(token);
            timeoutCts.CancelAfter(ReceiveTimeout);

            WebSocketReceiveResult result;
            try
            {
                result = await ws.ReceiveAsync(new ArraySegment<byte>(buf), timeoutCts.Token);
            }
            catch (OperationCanceledException) when (!token.IsCancellationRequested)
            {
                // Receive timed out (not a user-requested cancel) — connection is dead
                break;
            }

            if (result.MessageType == WebSocketMessageType.Close)
                break;

            sb.Append(Encoding.UTF8.GetString(buf, 0, result.Count));

            if (result.EndOfMessage)
            {
                ProcessMessage(sb.ToString());
                sb.Clear();
            }
        }
    }

    private void ProcessMessage(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            var type = root.GetProperty("type").GetString();

            switch (type)
            {
                case "status":
                {
                    var status = JsonSerializer.Deserialize<StatusDto>(json);
                    if (status != null)
                        StatusReceived?.Invoke(status);
                    break;
                }

                case "event":
                {
                    var kind = root.GetProperty("kind").GetString() ?? "system";
                    var message = root.GetProperty("message").GetString() ?? "";
                    EventReceived?.Invoke(kind, message);
                    break;
                }

                case "flip":
                {
                    var item = root.GetProperty("item").GetString() ?? "";
                    var cost = root.GetProperty("cost").GetInt64();
                    var target = root.GetProperty("target").GetInt64();
                    var profit = root.GetProperty("profit").GetInt64();
                    long? buySpeed = null;
                    if (root.TryGetProperty("buySpeed", out var bsEl) && bsEl.ValueKind == System.Text.Json.JsonValueKind.Number)
                        buySpeed = bsEl.GetInt64();
                    string? tag = null;
                    if (root.TryGetProperty("tag", out var tagEl) && tagEl.ValueKind == System.Text.Json.JsonValueKind.String)
                        tag = tagEl.GetString();
                    FlipReceived?.Invoke(item, cost, target, profit, buySpeed, tag);
                    break;
                }

                case "bazaar_flip":
                {
                    var item = root.GetProperty("item").GetString() ?? "";
                    var amount = root.GetProperty("amount").GetInt32();
                    var ppu = root.GetProperty("price_per_unit").GetInt64();
                    var isBuy = root.GetProperty("is_buy").GetBoolean();
                    BazaarFlipReceived?.Invoke(item, amount, ppu, isBuy);
                    break;
                }

                case "relist":
                {
                    var item = root.GetProperty("item").GetString() ?? "";
                    var sellPrice = root.GetProperty("sellPrice").GetInt64();
                    var buyCost = root.GetProperty("buyCost").GetInt64();
                    var profit = root.GetProperty("profit").GetInt64();
                    var duration = root.GetProperty("duration").GetInt64();
                    int slot = -1;
                    if (root.TryGetProperty("slot", out var slotEl) && slotEl.ValueKind == System.Text.Json.JsonValueKind.Number)
                        slot = slotEl.GetInt32();
                    string? tag = null;
                    if (root.TryGetProperty("tag", out var tagEl2) && tagEl2.ValueKind == System.Text.Json.JsonValueKind.String)
                        tag = tagEl2.GetString();
                    System.Diagnostics.Debug.WriteLine($"[BackendSocket] relist: item='{item}', sell={sellPrice}, buy={buyCost}, profit={profit}, slot={slot}, tag={tag}");
                    RelistReceived?.Invoke(item, sellPrice, buyCost, profit, duration, slot, tag);
                    break;
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[BackendSocket] ProcessMessage error: {ex.Message} — json: {json[..Math.Min(json.Length, 200)]}");
        }
    }

    /// <summary>Stop the connection loop and close the socket.</summary>
    public void Disconnect()
    {
        _cts?.Cancel();
        _cts?.Dispose();
        _cts = null;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        Disconnect();
    }
}
