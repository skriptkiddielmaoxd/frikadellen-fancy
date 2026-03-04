using System.Collections.ObjectModel;
using System.Windows.Input;
using Frikadellen.UI.Models;
using Frikadellen.UI.Services;

namespace Frikadellen.UI.ViewModels;

public sealed class DashboardViewModel : ViewModelBase
{
    private readonly MainWindowViewModel _parent;
    private readonly BackendClient _backend;

    private bool _isRunning;
    private long _purse;
    private int _queueDepth;
    private string _botStatus = "Idle";
    private string _toggleLabel = "▶  Start";
    private string _toggleColor = "#00B894";

    // Flip stats
    private int _totalFlips;
    private long _totalProfit;
    private long _sessionProfit;
    private double _avgBuySpeed;
    private int _speedCount;
    private long _speedSum;

    // Selected items
    private EventItem? _selectedEvent;
    private FlipRecord? _selectedFlip;

    // Chat input
    private string _chatInput = string.Empty;

    public DashboardViewModel(MainWindowViewModel parent,
                               BackendClient backend,
                               ObservableCollection<EventItem> events,
                               ObservableCollection<FlipRecord> flips,
                               ObservableCollection<ChatMessage> chatLog,
                               ObservableCollection<InventorySlot> inventorySlots)
    {
        _parent = parent;
        _backend = backend;
        Events = events;
        Flips = flips;
        ChatLog = chatLog;
        InventorySlots = inventorySlots;
        ToggleCommand = new RelayCommand(Toggle);
        SendChatCommand = new RelayCommand(SendChat);
    }

    public bool IsRunning
    {
        get => _isRunning;
        set
        {
            if (SetField(ref _isRunning, value))
            {
                ToggleLabel = value ? "■  Stop" : "▶  Start";
                ToggleColor = value ? "#FF6B6B" : "#00B894";
            }
        }
    }

    public long Purse { get => _purse; set => SetField(ref _purse, value); }
    public int QueueDepth { get => _queueDepth; set => SetField(ref _queueDepth, value); }
    public string BotStatus { get => _botStatus; set => SetField(ref _botStatus, value); }
    public string ToggleLabel { get => _toggleLabel; set => SetField(ref _toggleLabel, value); }
    public string ToggleColor { get => _toggleColor; set => SetField(ref _toggleColor, value); }

    public string PurseFormatted => Models.CoinFormat.WithCoins(Purse);

    // Flip stats
    public int TotalFlips { get => _totalFlips; set => SetField(ref _totalFlips, value); }
    public long TotalProfit { get => _totalProfit; set { if (SetField(ref _totalProfit, value)) OnPropertyChanged(nameof(TotalProfitFormatted)); } }
    public long SessionProfit { get => _sessionProfit; set { if (SetField(ref _sessionProfit, value)) { OnPropertyChanged(nameof(SessionProfitFormatted)); OnPropertyChanged(nameof(SessionProfitColor)); } } }
    public double AvgBuySpeed { get => _avgBuySpeed; set { if (SetField(ref _avgBuySpeed, value)) OnPropertyChanged(nameof(AvgBuySpeedFormatted)); } }

    public string TotalProfitFormatted => Models.CoinFormat.WithCoins(TotalProfit);
    public string SessionProfitFormatted => Models.CoinFormat.WithCoins(SessionProfit);
    public string SessionProfitColor => SessionProfit >= 0 ? "#00B894" : "#FF6B6B";
    public string AvgBuySpeedFormatted => AvgBuySpeed > 0 ? $"{AvgBuySpeed:F0}ms" : "—";

    // Collections populated from the shared data store in MainWindowViewModel
    public ObservableCollection<EventItem> Events { get; }
    public ObservableCollection<FlipRecord> Flips { get; }
    public ObservableCollection<ChatMessage> ChatLog { get; }
    public ObservableCollection<InventorySlot> InventorySlots { get; }

    public EventItem? SelectedEvent { get => _selectedEvent; set => SetField(ref _selectedEvent, value); }
    public FlipRecord? SelectedFlip { get => _selectedFlip; set => SetField(ref _selectedFlip, value); }

    public string ChatInput { get => _chatInput; set => SetField(ref _chatInput, value); }

    public ICommand ToggleCommand { get; }
    public ICommand SendChatCommand { get; }

    private void Toggle()
    {
        if (_parent.IsBackendRunning)
            _parent.StopScript();
        else
            _parent.StartScript();
    }

    /// <summary>Called by MainWindowViewModel when a WebSocket status message arrives.</summary>
    public void UpdateStatus(StatusDto status)
    {
        IsRunning = status.Running;
        Purse = status.Purse;
        QueueDepth = status.QueueDepth;
        BotStatus = status.State;
        OnPropertyChanged(nameof(PurseFormatted));
    }

    /// <summary>Called by MainWindowViewModel when the running state changes externally.</summary>
    public void UpdateRunningState(bool running)
    {
        IsRunning = running;
        if (!running) BotStatus = "Stopped";
    }

    /// <summary>Called by MainWindowViewModel when a flip event arrives.</summary>
    public void TrackFlip(FlipRecord flip)
    {
        TotalFlips++;
        TotalProfit += flip.Profit;
        SessionProfit += flip.Profit;
        if (flip.BuySpeedMs.HasValue)
        {
            _speedCount++;
            _speedSum += flip.BuySpeedMs.Value;
            AvgBuySpeed = (double)_speedSum / _speedCount;
        }
    }

    private async void SendChat()
    {
        var text = ChatInput?.Trim();
        if (string.IsNullOrEmpty(text)) return;

        ChatInput = string.Empty;

        // Show in local chat log immediately
        ChatLog.Add(new ChatMessage
        {
            Sender = "[You]",
            Text = text,
            Color = "#74B9FF",
            Spans = new System.Collections.Generic.List<ChatSpan>
            {
                new() { Text = text, Color = "#74B9FF" }
            },
        });

        // Fire to backend
        await _backend.SendCommandAsync(text);
    }
}
