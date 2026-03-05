using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Windows.Input;
using Avalonia.Threading;
using Frikadellen.UI.Models;
using Frikadellen.UI.Services;

namespace Frikadellen.UI.ViewModels;

public sealed class DashboardViewModel : ViewModelBase
{
    private readonly DispatcherTimer _mockTimer;
    private bool _isRunning;
    private string _scriptState = "Stopped";
    private string _purse = "0";
    private int _queueDepth;
    private string _botStatus = "Offline";
    private long _sessionProfit;
    private int _sessionFlips;
    private int _sessionWins;
    private long _totalCoinsSpent;
    private long _totalCoinsEarned;
    private FlipRecord? _latestFlip;

    public bool IsRunning
    {
        get => _isRunning;
        set
        {
            if (SetField(ref _isRunning, value))
            {
                OnPropertyChanged(nameof(ToggleButtonLabel));
                OnPropertyChanged(nameof(ToggleButtonColor));
                OnPropertyChanged(nameof(ToggleButtonShadow));
                ((RelayCommand)ToggleCommand).RaiseCanExecuteChanged();
            }
        }
    }

    public string ScriptState
    {
        get => _scriptState;
        set => SetField(ref _scriptState, value);
    }

    public string Purse
    {
        get => _purse;
        set => SetField(ref _purse, value);
    }

    public int QueueDepth
    {
        get => _queueDepth;
        set => SetField(ref _queueDepth, value);
    }

    public string BotStatus
    {
        get => _botStatus;
        set => SetField(ref _botStatus, value);
    }

    public string SessionProfit
        => _sessionProfit > 0 ? $"+{Fmt.Coins(_sessionProfit)}" : Fmt.Coins(_sessionProfit);

    public int SessionFlips => _sessionFlips;

    public string TotalCoinsSpent  => Fmt.Coins(_totalCoinsSpent);
    public string TotalCoinsEarned => Fmt.Coins(_totalCoinsEarned);

    public string WinRate => _sessionFlips > 0
        ? $"{Math.Round(_sessionWins / (double)_sessionFlips * 100, 1):0.#}%"
        : "—";

    public FlipRecord? LatestFlip
    {
        get => _latestFlip;
        set => SetField(ref _latestFlip, value);
    }

    public string ToggleButtonLabel => IsRunning ? "⏹  Stop Script" : "▶  Start Script";

    public string ToggleButtonColor =>
        IsRunning ? "#FB7185" : "#E879F9";

    public string ToggleButtonShadow =>
        IsRunning ? "0 4 20 0 #60FB7185" : "0 4 20 0 #60E879F9";

    public ObservableCollection<FlipRecord> RecentFlips { get; } = new();

    public List<double> ProfitTimeline { get; } = MockDataService.GetProfitTimeline();
    public List<double> HourlyEarnings { get; } = MockDataService.GetHourlyEarnings();
    public double DonutSuccessRate => _sessionFlips > 0 ? (double)_sessionWins / _sessionFlips : 0.87;
    public double DonutFailRate    => _sessionFlips > 0 ? 1.0 - DonutSuccessRate - DonutPendingRate : 0.10;
    public double DonutPendingRate => 0.03;

    public ICommand ToggleCommand { get; }

    /// <summary>
    /// Fired when the user wants to start / stop the script.
    /// The MainWindowViewModel wires this up to the real backend.
    /// </summary>
    public event Action<bool>? ToggleRequested;

    public DashboardViewModel()
    {
        ToggleCommand = new RelayCommand(OnToggle);

        _mockTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(1800) };
        _mockTimer.Tick += OnMockTick;
    }

    private void OnToggle()
    {
        IsRunning = !IsRunning;

        if (IsRunning)
        {
            ScriptState = "Running";
            BotStatus = "Online";
            _mockTimer.Start();
        }
        else
        {
            ScriptState = "Stopped";
            BotStatus = "Offline";
            _mockTimer.Stop();
        }

        // INTEGRATION POINT: notify parent to start/stop the real backend
        ToggleRequested?.Invoke(IsRunning);
    }

    private void OnMockTick(object? sender, EventArgs e)
    {
        // Simulate live data while running
        Purse = MockDataService.RandomPurse();
        QueueDepth = MockDataService.RandomQueue();

        var flip = MockDataService.RandomFlip();
        TrackFlip(flip);
    }

    public void UpdateFromStatus(string state, string purse, int queue, string botStatus)
    {
        ScriptState = state;
        Purse       = purse;
        QueueDepth  = queue;
        BotStatus   = botStatus;
    }

    /// <summary>
    /// Called by the WebSocket handler or mock timer whenever a flip completes.
    /// A flip is counted as a "win" when profit is positive.
    /// </summary>
    public void TrackFlip(FlipRecord flip)
    {
        RecentFlips.Insert(0, flip);
        if (RecentFlips.Count > 50) RecentFlips.RemoveAt(RecentFlips.Count - 1);
        LatestFlip = flip;

        _sessionProfit     += flip.Profit;
        _totalCoinsSpent   += flip.BuyPrice;
        _totalCoinsEarned  += flip.SellPrice;
        _sessionFlips++;
        if (flip.Profit > 0) _sessionWins++;

        OnPropertyChanged(nameof(SessionProfit));
        OnPropertyChanged(nameof(SessionFlips));
        OnPropertyChanged(nameof(TotalCoinsSpent));
        OnPropertyChanged(nameof(TotalCoinsEarned));
        OnPropertyChanged(nameof(WinRate));
    }

    /// <summary>INTEGRATION POINT: push live stats from GET /api/stats.</summary>
    public void ApplyStats(Services.StatsDto stats)
    {
        _sessionProfit    = stats.SessionProfit;
        _totalCoinsSpent  = stats.TotalCoinsSpent;
        _totalCoinsEarned = stats.TotalCoinsEarned;
        _sessionFlips     = stats.TotalFlips;
        _sessionWins      = stats.WinCount;

        OnPropertyChanged(nameof(SessionProfit));
        OnPropertyChanged(nameof(SessionFlips));
        OnPropertyChanged(nameof(TotalCoinsSpent));
        OnPropertyChanged(nameof(TotalCoinsEarned));
        OnPropertyChanged(nameof(WinRate));
    }
}
