using System;
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
    public string ToggleButtonColor => IsRunning ? "#FB7185" : "#E879F9";
    public string ToggleButtonShadow => IsRunning ? "0 4 20 0 #60FB7185" : "0 4 20 0 #60E879F9";

    public ObservableCollection<FlipRecord> RecentFlips { get; } = new();

    public ICommand ToggleCommand { get; }

    /// <summary>Fired when the user toggles the script. The parent VM wires this to the real backend.</summary>
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

        ToggleRequested?.Invoke(IsRunning);
    }

    private void OnMockTick(object? sender, EventArgs e)
    {
        Purse = MockDataService.RandomPurse();
        QueueDepth = MockDataService.RandomQueue();
        var flip = MockDataService.RandomFlip();
        TrackFlip(flip);
    }

    public void UpdateRunningState(bool running)
    {
        IsRunning = running;
        ScriptState = running ? "Running" : "Stopped";
        BotStatus = running ? "Online" : "Offline";
        if (!running) _mockTimer.Stop();
    }

    public void UpdateFromStatus(string state, string purse, int queue, string botStatus)
    {
        ScriptState = state;
        Purse = purse;
        QueueDepth = queue;
        BotStatus = botStatus;
    }

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

    public void ApplyStats(StatsDto stats)
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
