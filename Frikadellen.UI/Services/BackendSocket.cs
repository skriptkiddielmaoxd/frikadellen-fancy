using System;
using System.Diagnostics;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Frikadellen.UI.Services;

/// <summary>
/// WebSocket client for the frikadellen-fancy Rust backend real-time events.
/// Connects to ws://localhost:{port}/ws and dispatches typed events.
/// </summary>
public sealed class BackendSocket : IDisposable
{
    private readonly int _port;
    private ClientWebSocket? _ws;
    private CancellationTokenSource? _cts;
    private bool _disposed;

    /// <summary>Fires when a status update is received from the backend.</summary>
    public event Action<StatusDto>? StatusReceived;

    /// <summary>Fires when an event is received. Args: (kind, message).</summary>
    public event Action<string, string>? EventReceived;

    /// <summary>Fires when a flip is recorded. Args: (item, cost, target, buySpeedMs, tag).</summary>
    public event Action<string, long, long, int, string>? FlipReceived;

    /// <summary>Fires when a bazaar flip is recorded. Args: (item, amount, pricePerUnit, isBuy).</summary>
    public event Action<string, int, long, bool>? BazaarFlipReceived;

    /// <summary>Fires when the connection state changes. True = connected, false = disconnected.</summary>
    public event Action<bool>? ConnectionChanged;

    public BackendSocket(int port) => _port = port;

    /// <summary>Initiates a WebSocket connection asynchronously (fire-and-forget).</summary>
    public void ConnectAsync()
    {
        _ = ConnectInternalAsync();
    }

    private async Task ConnectInternalAsync()
    {
        _cts?.Cancel();
        _cts?.Dispose();
        _cts = new CancellationTokenSource();

        try
        {
            _ws?.Dispose();
            _ws = new ClientWebSocket();
            await _ws.ConnectAsync(new Uri($"ws://localhost:{_port}/ws"), _cts.Token);
            ConnectionChanged?.Invoke(true);
            await ReceiveLoopAsync(_cts.Token);
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            Debug.WriteLine($"[BackendSocket] Connection failed: {ex.GetType().Name}: {ex.Message}");
            ConnectionChanged?.Invoke(false);
        }
    }

    private async Task ReceiveLoopAsync(CancellationToken ct)
    {
        var buffer = new byte[16 * 1024];
        while (!ct.IsCancellationRequested && _ws?.State == WebSocketState.Open)
        {
            try
            {
                var result = await _ws.ReceiveAsync(buffer, ct);
                if (result.MessageType == WebSocketMessageType.Close)
                {
                    ConnectionChanged?.Invoke(false);
                    break;
                }
                var json = Encoding.UTF8.GetString(buffer, 0, result.Count);
                DispatchMessage(json);
            }
            catch (OperationCanceledException) { break; }
            catch (Exception ex)
            {
                Debug.WriteLine($"[BackendSocket] Receive error: {ex.GetType().Name}: {ex.Message}");
                ConnectionChanged?.Invoke(false);
                break;
            }
        }
    }

    private void DispatchMessage(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            if (!root.TryGetProperty("type", out var typeEl)) return;
            switch (typeEl.GetString())
            {
                case "status":
                    var status = root.GetProperty("data").Deserialize<StatusDto>();
                    if (status != null) StatusReceived?.Invoke(status);
                    break;
                case "event":
                    var kind    = root.GetProperty("kind").GetString() ?? "";
                    var message = root.GetProperty("message").GetString() ?? "";
                    EventReceived?.Invoke(kind, message);
                    break;
                case "flip":
                    var item       = root.GetProperty("item").GetString() ?? "";
                    var cost       = root.GetProperty("cost").GetInt64();
                    var target     = root.GetProperty("target").GetInt64();
                    var buySpeedMs = root.GetProperty("buy_speed_ms").GetInt32();
                    var tag        = root.GetProperty("tag").GetString() ?? "";
                    FlipReceived?.Invoke(item, cost, target, buySpeedMs, tag);
                    break;
                case "bazaar_flip":
                    var bzItem = root.GetProperty("item").GetString() ?? "";
                    var amount = root.GetProperty("amount").GetInt32();
                    var price  = root.GetProperty("price_per_unit").GetInt64();
                    var isBuy  = root.GetProperty("is_buy").GetBoolean();
                    BazaarFlipReceived?.Invoke(bzItem, amount, price, isBuy);
                    break;
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[BackendSocket] Message dispatch error: {ex.GetType().Name}: {ex.Message}");
        }
    }

    /// <summary>Closes the WebSocket connection.</summary>
    public void Disconnect()
    {
        _cts?.Cancel();
        if (_ws?.State == WebSocketState.Open)
        {
            try { _ = _ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "", CancellationToken.None); }
            catch (Exception ex)
            {
                Debug.WriteLine($"[BackendSocket] Graceful close failed: {ex.GetType().Name}: {ex.Message}");
            }
        }
        ConnectionChanged?.Invoke(false);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        Disconnect();
        _ws?.Dispose();
        _cts?.Dispose();
    }
}
