using System;
using System.Collections.ObjectModel;
using System.Threading;
using Avalonia.Threading;
using Frikadellen.UI.Models;

namespace Frikadellen.UI.Services;

public sealed class MockDataService : IDisposable
{
    private Timer? _metricsTimer;
    private Timer? _eventsTimer;
    private Timer? _flipsTimer;
    private Timer? _chatTimer;
    private readonly Random _rng = new();

    public bool IsRunning { get; private set; }
    public int Purse { get; private set; } = 12_450;
    public int QueueDepth { get; private set; } = 7;
    public string BotStatus { get; private set; } = "Idle";

    // Flip tracking
    public int TotalFlips { get; private set; }
    public long TotalProfit { get; private set; }
    public long SessionProfit { get; private set; }
    public double AvgBuySpeed { get; private set; }
    private int _speedCount;
    private long _speedSum;

    public ObservableCollection<EventItem> Events { get; } = new();
    public ObservableCollection<FlipRecord> Flips { get; } = new();
    public ObservableCollection<ChatMessage> ChatLog { get; } = new();

    public event Action? MetricsUpdated;

    private static readonly string[] EventTypes = { "Trade", "Alert", "Info", "Warning", "Error" };
    private static readonly string[] EventMessages =
    {
        "Auction flip purchased successfully",
        "Queue flushed – 3 pending orders sent",
        "Heartbeat OK – latency 12 ms",
        "Rate-limit warning – 429 from API",
        "WebSocket reconnected successfully",
        "New channel message processed",
        "Purse rebalanced automatically",
        "Inventory snapshot saved",
        "Webhook delivered to endpoint",
        "Config reloaded from disk",
        "Bazaar order filled – ready to collect",
        "Cookie check passed – 3 days remaining",
        "Listed BIN auction for resale"
    };
    private static readonly string[] Avatars = { "🟢", "🔵", "🟡", "🔴", "🟣" };
    private static readonly string[] Tags = { "trade", "system", "bot", "network", "config" };

    private static readonly string[] FlipItems =
    {
        "Hyperion", "Terminator", "Divan's Alloy", "Necron's Handle",
        "Giant's Sword", "Wither Blade", "Shadow Fury", "Juju Shortbow",
        "Spirit Sceptre", "Aspect of the End", "Livid Dagger",
        "Flower of Truth", "Bonzo's Staff", "Last Breath",
        "Ice Spray Wand", "Midas' Sword", "Pigman Sword",
        "Warden Helmet", "Storm's Boots", "Goldor's Chestplate"
    };
    private static readonly string[] Finders = { "SNIPER", "USER", "SKINS", "TFM", "STONKS", "FLIPPER" };

    private static readonly string[] ChatSenders = { "[BAF]", "Coflnet", "System", "Hypixel" };
    private static readonly string[] ChatMessages =
    {
        "Auction bought in {0}ms",
        "You purchased {1} for {2} coins!",
        "[Auction] {3} bought {1} for {4} coins CLICK",
        "Putting coins in escrow...",
        "Visit the Auction House to collect your item!",
        "Your bazaar order was filled!",
        "Booster Cookie expiring in 2d 14h",
        "Claimed BIN auction successfully!",
        "Profit so far this session: {5} coins",
        "Bed Wars (★142): Technoblade joined the lobby!",
        "RARE DROP! (Wither Catalyst)",
        "Bazaar buy order placed: 64x Enchanted Diamond"
    };

    public void Start()
    {
        if (IsRunning) return;
        IsRunning = true;
        BotStatus = "Running";
        SessionProfit = 0;

        _metricsTimer = new Timer(_ => UpdateMetrics(), null, 0, 1500);
        _eventsTimer = new Timer(_ => AddRandomEvent(), null, 500, 2500);
        _flipsTimer = new Timer(_ => AddRandomFlip(), null, 1200, 4000);
        _chatTimer = new Timer(_ => AddRandomChat(), null, 300, 1800);

        MetricsUpdated?.Invoke();
    }

    public void Stop()
    {
        IsRunning = false;
        BotStatus = "Stopped";
        _metricsTimer?.Dispose();
        _eventsTimer?.Dispose();
        _flipsTimer?.Dispose();
        _chatTimer?.Dispose();
        _metricsTimer = null;
        _eventsTimer = null;
        _flipsTimer = null;
        _chatTimer = null;

        Dispatcher.UIThread.Post(() =>
        {
            ChatLog.Add(new ChatMessage
            {
                Sender = "[BAF]",
                Text = $"Session stopped. Profit: {SessionProfit:N0} coins across {TotalFlips} flips.",
                Color = "#FDCB6E",
                IsSystem = true
            });
        });

        MetricsUpdated?.Invoke();
    }

    private void UpdateMetrics()
    {
        Purse += _rng.Next(-200, 400);
        if (Purse < 0) Purse = 500;
        QueueDepth = Math.Max(0, QueueDepth + _rng.Next(-3, 5));
        MetricsUpdated?.Invoke();
    }

    private void AddRandomEvent()
    {
        var idx = _rng.Next(EventMessages.Length);
        var evt = new EventItem
        {
            Type = EventTypes[_rng.Next(EventTypes.Length)],
            Message = EventMessages[idx],
            Details = $"Triggered at {DateTime.Now:HH:mm:ss.fff} – mock detail for event #{Events.Count + 1}.",
            Tag = Tags[_rng.Next(Tags.Length)],
            Avatar = Avatars[_rng.Next(Avatars.Length)]
        };
        Dispatcher.UIThread.Post(() => Events.Add(evt));
    }

    private void AddRandomFlip()
    {
        var item = FlipItems[_rng.Next(FlipItems.Length)];
        var buyPrice = _rng.Next(50_000, 15_000_000);
        var profitMargin = 0.05 + _rng.NextDouble() * 0.45; // 5-50%
        var sellPrice = (long)(buyPrice * (1 + profitMargin));
        // Occasionally a loss
        if (_rng.NextDouble() < 0.12)
            sellPrice = (long)(buyPrice * (0.7 + _rng.NextDouble() * 0.25));

        var speed = _rng.Next(180, 1800);
        var flip = new FlipRecord
        {
            ItemName = item,
            BuyPrice = buyPrice,
            SellPrice = sellPrice,
            BuySpeedMs = speed,
            Finder = Finders[_rng.Next(Finders.Length)]
        };

        TotalFlips++;
        TotalProfit += flip.Profit;
        SessionProfit += flip.Profit;
        _speedCount++;
        _speedSum += speed;
        AvgBuySpeed = (double)_speedSum / _speedCount;
        Purse += (int)flip.Profit;

        Dispatcher.UIThread.Post(() =>
        {
            Flips.Insert(0, flip); // newest first
            if (Flips.Count > 200) Flips.RemoveAt(Flips.Count - 1);
        });

        MetricsUpdated?.Invoke();
    }

    private void AddRandomChat()
    {
        var sender = ChatSenders[_rng.Next(ChatSenders.Length)];
        var template = ChatMessages[_rng.Next(ChatMessages.Length)];
        var item = FlipItems[_rng.Next(FlipItems.Length)];
        var price = _rng.Next(100_000, 10_000_000);
        var speed = _rng.Next(200, 1500);
        var buyer = "Player" + _rng.Next(100, 9999);
        var sellP = (long)(price * 1.2);

        var text = template
            .Replace("{0}", speed.ToString())
            .Replace("{1}", item)
            .Replace("{2}", price.ToString("N0"))
            .Replace("{3}", buyer)
            .Replace("{4}", sellP.ToString("N0"))
            .Replace("{5}", SessionProfit.ToString("N0"));

        var isSystem = sender == "[BAF]" || sender == "System";
        var color = sender switch
        {
            "[BAF]" => "#00CEC9",
            "Coflnet" => "#6C5CE7",
            "Hypixel" => "#FDCB6E",
            _ => "#DFE6E9"
        };

        Dispatcher.UIThread.Post(() =>
        {
            ChatLog.Add(new ChatMessage
            {
                Sender = sender,
                Text = text,
                Color = color,
                IsSystem = isSystem
            });
            if (ChatLog.Count > 300) ChatLog.RemoveAt(0);
        });
    }

    public void Dispose()
    {
        _metricsTimer?.Dispose();
        _eventsTimer?.Dispose();
        _flipsTimer?.Dispose();
        _chatTimer?.Dispose();
    }
}
