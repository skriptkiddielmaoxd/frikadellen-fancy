using System;
using System.Windows.Input;
using Frikadellen.UI.Services;

namespace Frikadellen.UI.ViewModels;

/// <summary>
/// Root view-model that drives the entire window lifecycle:
///   Splash → (optional) Login → Shell (nav bar + pages)
/// Bridges the new UI structure with the real Rust backend.
/// </summary>
public sealed class MainWindowViewModel : ViewModelBase
{
    private readonly SettingsService     _settings = new();
    private readonly BackendClient       _backend;
    private readonly BackendSocket       _socket;
    private readonly RustProcessLauncher _launcher = new();

    // ── Phase tracking ──
    public enum Phase { Splash, Login, Shell }

    private Phase _currentPhase = Phase.Splash;
    private ViewModelBase _currentView = null!;

    // ── Shell state ──
    private string _activeNav = "Dashboard";
    private string _statusText = "Stopped";

    // Child view-models (lazy)
    private DashboardViewModel? _dashboard;
    private EventsViewModel?    _events;
    private ConfigViewModel?    _config;
    private NotifierViewModel?  _notifier;
    private ConsoleViewModel?   _console;

    public MainWindowViewModel()
    {
        _backend = new BackendClient(8080);
        _socket  = new BackendSocket(8080);

        // Start with the splash screen
        var splash = new SplashViewModel();
        splash.Completed += OnSplashCompleted;
        CurrentView = splash;

        NavigateCommand    = new RelayCommand(o => Navigate(o?.ToString()));
        ToggleThemeCommand = new RelayCommand(() => App.ToggleTheme());

        // Propagate launcher state to the status chip
        _launcher.RunningChanged += running =>
        {
            Dispatcher.UIThread.Post(() =>
            {
                StatusText = running ? "Running" : "Stopped";
                OnPropertyChanged(nameof(StatusChipColor));
                _dashboard?.UpdateRunningState(running);
            });
        };

        _launcher.ProcessExited += code => Dispatcher.UIThread.Post(() =>
        {
            StatusText = code == 0 ? "Stopped" : $"Crashed ({code})";
            OnPropertyChanged(nameof(StatusChipColor));
        });

        // WebSocket events
        _socket.StatusReceived     += OnStatusReceived;
        _socket.EventReceived      += OnEventReceived;
        _socket.FlipReceived       += OnFlipReceived;
        _socket.BazaarFlipReceived += OnBazaarFlipReceived;
        _socket.ConnectionChanged  += OnConnectionChanged;
    }

    // ── Properties ──

    public ViewModelBase CurrentView
    {
        get => _currentView;
        set => SetField(ref _currentView, value);
    }

    public Phase CurrentPhase
    {
        get => _currentPhase;
        private set
        {
            if (SetField(ref _currentPhase, value))
            {
                OnPropertyChanged(nameof(IsSplash));
                OnPropertyChanged(nameof(IsLogin));
                OnPropertyChanged(nameof(IsShell));
            }
        }
    }

    public bool IsSplash => _currentPhase == Phase.Splash;
    public bool IsLogin  => _currentPhase == Phase.Login;
    public bool IsShell  => _currentPhase == Phase.Shell;

    public string ActiveNav
    {
        get => _activeNav;
        set => SetField(ref _activeNav, value);
    }

    public string StatusText
    {
        get => _currentPhase;
        private set
        {
            if (SetField(ref _currentPhase, value))
            {
                OnPropertyChanged(nameof(IsSplash));
                OnPropertyChanged(nameof(IsLogin));
                OnPropertyChanged(nameof(IsShell));
            }
        }
    }

    public string StatusChipColor => StatusText switch
    {
        "Running"                                => "#4ADE80",
        var s when s.StartsWith("Starting")      => "#FBBF24",
        var s when s.StartsWith("Crashed")       => "#FB7185",
        _                                        => "#6B5F8A",
    };

    public ICommand NavigateCommand    { get; }
    public ICommand ToggleThemeCommand { get; }

    // ── Lifecycle ──

    private void OnSplashCompleted()
    {
        var saved = _settings.Load();
        if (!saved.FirstRunComplete)
        {
            var login = new LoginViewModel(_settings, saved);
            login.Completed += OnLoginCompleted;
            CurrentPhase = Phase.Login;
            CurrentView  = login;
        }
        else
        {
            TransitionToShell();
        }
    }

    private void OnLoginCompleted() => TransitionToShell();

    private void TransitionToShell()
    {
        CurrentPhase = Phase.Shell;
        _dashboard  ??= CreateDashboard();
        CurrentView  = _dashboard;
        ActiveNav    = "Dashboard";
    }

    private DashboardViewModel CreateDashboard()
    {
        var vm = new DashboardViewModel();
        vm.ToggleRequested += isRunning =>
        {
            if (isRunning) StartScript();
            else           StopScript();
        };
        return vm;
    }

    // ── Navigation ──

    public void Navigate(string? target)
    {
        ActiveNav = target ?? "Dashboard";
        CurrentView = target switch
        {
            "Events"   => _events   ??= new EventsViewModel(),
            "Config"   => _config   ??= new ConfigViewModel(_settings),
            "Notifier" => _notifier ??= new NotifierViewModel(_settings),
            "Console"  => _console  ??= new ConsoleViewModel(_launcher),
            _          => _dashboard ??= CreateDashboard(),
        };
    }

    // ── Script control ──

    public void StartScript()
    {
        if (_launcher.IsRunning) return;
        StatusText = "Starting...";
        OnPropertyChanged(nameof(StatusChipColor));
        var ok = _launcher.Start();
        if (!ok)
        {
            StatusText = "Binary not found";
            OnPropertyChanged(nameof(StatusChipColor));
            _dashboard?.UpdateRunningState(false);
            return;
        }
        // Give the Rust binary a moment to bind its port, then connect WS
        _ = System.Threading.Tasks.Task.Delay(800).ContinueWith(_ =>
            Dispatcher.UIThread.Post(() => _socket.ConnectAsync()));
    }

    public void StopScript()
    {
        _socket.Disconnect();
        _launcher.Stop();
        StatusText = "Stopped";
        OnPropertyChanged(nameof(StatusChipColor));
    }

    // ── WebSocket event handlers ──

    private void OnSplashCompleted()
    {
        var saved = _settings.Load();
        if (!saved.FirstRunComplete)
        {
            // Show the login / setup screen on first run
            var login = new LoginViewModel(_settings, saved);
            login.Completed += OnLoginCompleted;
            CurrentPhase = Phase.Login;
            CurrentView  = login;
        }
        else
        {
            StatusText = status.Running ? "Running" : status.State;
            OnPropertyChanged(nameof(StatusChipColor));
            _dashboard?.UpdateFromStatus(
                status.State,
                Fmt.Coins(status.Purse),
                status.QueueDepth,
                status.Running ? "Online" : "Offline");
        });
    }

    private void OnLoginCompleted()
    {
        var avatar = kind switch
        {
            "error"    => "🔴",
            "purchase" => "🛒",
            "sold"     => "⚡",
            "bazaar"   => "📦",
            "listing"  => "🏷️",
            _          => "🔵",
        };
        var typeLabel = kind switch
        {
            "error"    => "Error",
            "purchase" => "Purchase",
            "sold"     => "Sale",
            "bazaar"   => "Bazaar",
            "listing"  => "Listing",
            _          => "Info",
        };

        var evt = new EventItem
        {
            Type    = typeLabel,
            Message = message,
            Tag     = kind,
            Avatar  = avatar,
        };

        Dispatcher.UIThread.Post(() => _events?.AddEvent(evt));
    }

    private void TransitionToShell()
    {
        var flip = new FlipRecord
        {
            ItemName   = item,
            BuyPrice   = cost,
            SellPrice  = target,
            BuySpeedMs = buySpeedMs,
            ItemTag    = tag,
        };
        Dispatcher.UIThread.Post(() => _dashboard?.TrackFlip(flip));
    }

    private void OnBazaarFlipReceived(string item, int amount, long pricePerUnit, bool isBuy)
    {
        var label = isBuy ? "BUY" : "SELL";
        var evt = new EventItem
        {
            Type    = "Bazaar",
            Message = $"[BZ] {label}: {item} x{amount} @ {Fmt.Coins(pricePerUnit)}/unit",
            Tag     = "bazaar",
            Avatar  = "📦",
        };
        Dispatcher.UIThread.Post(() => _events?.AddEvent(evt));
    }

    private void OnConnectionChanged(bool connected)
    {
        Dispatcher.UIThread.Post(() =>
        {
            if (!connected && _launcher.IsRunning)
                StatusText = "Disconnected";
        });
    }
}
