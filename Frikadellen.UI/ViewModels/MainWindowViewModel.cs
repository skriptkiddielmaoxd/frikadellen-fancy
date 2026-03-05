using System;
using System.Windows.Input;
using Frikadellen.UI.Services;

namespace Frikadellen.UI.ViewModels;

/// <summary>
/// Root view-model that drives the entire window lifecycle:
///   Splash → (optional) Login → Shell (sidebar + pages)
/// </summary>
public sealed class MainWindowViewModel : ViewModelBase
{
    private readonly SettingsService     _settings = new();
    private readonly RustProcessLauncher _launcher = new();

    // ── Phase tracking ──
    public enum Phase { Splash, Login, Shell }

    private Phase _currentPhase = Phase.Splash;
    private ViewModelBase _currentView = null!;

    // ── Shell state ──
    private bool _isSidebarExpanded = true;
    private string _activeNav = "Dashboard";
    private string _statusText = "Stopped";

    // Child view-models (lazy)
    private DashboardViewModel?  _dashboard;
    private EventsViewModel?     _events;
    private ConfigViewModel?     _config;
    private NotifierViewModel?   _notifier;
    private ConsoleViewModel?    _console;
    private AnalyticsViewModel?  _analytics;
    private BazaarViewModel?     _bazaar;

    public MainWindowViewModel()
    {
        // Start with splash
        var splash = new SplashViewModel();
        splash.Completed += OnSplashCompleted;
        CurrentView = splash;

        NavigateCommand      = new RelayCommand(o => Navigate(o?.ToString()));
        ToggleSidebarCommand = new RelayCommand(() => IsSidebarExpanded = !IsSidebarExpanded);
        ToggleThemeCommand   = new RelayCommand(() => App.ToggleTheme());

        // Propagate launcher running-state to the status chip
        _launcher.RunningChanged += running =>
        {
            StatusText = running ? "Running" : "Stopped";
            OnPropertyChanged(nameof(StatusChipColor));
        };
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

    public bool IsSidebarExpanded
    {
        get => _isSidebarExpanded;
        set
        {
            if (SetField(ref _isSidebarExpanded, value))
            {
                OnPropertyChanged(nameof(SidebarWidth));
                OnPropertyChanged(nameof(SidebarCollapseIcon));
            }
        }
    }

    public double SidebarWidth => _isSidebarExpanded ? 220 : 60;
    public string SidebarCollapseIcon => _isSidebarExpanded ? "◀" : "▶";

    public string ActiveNav
    {
        get => _activeNav;
        set => SetField(ref _activeNav, value);
    }

    public string StatusText
    {
        get => _statusText;
        set => SetField(ref _statusText, value);
    }

    public string StatusChipColor => StatusText switch
    {
        "Running"     => "#4ADE80",
        var s when s.StartsWith("Starting") => "#FBBF24",
        _             => "#FB7185",
    };

    public ICommand NavigateCommand { get; }
    public ICommand ToggleSidebarCommand { get; }
    public ICommand ToggleThemeCommand { get; }
    public Services.ToastService ToastService { get; } = Services.ToastService.Instance;

    // ── Lifecycle ──

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
            TransitionToShell();
        }
    }

    private void OnLoginCompleted()
    {
        TransitionToShell();
    }

    private void TransitionToShell()
    {
        CurrentPhase = Phase.Shell;
        _dashboard  ??= new DashboardViewModel();
        CurrentView  = _dashboard;
        ActiveNav    = "Dashboard";
    }

    // ── Navigation ──

    public void Navigate(string? target)
    {
        ActiveNav = target ?? "Dashboard";
        CurrentView = target switch
        {
            "Events"    => _events    ??= new EventsViewModel(),
            "Config"    => _config    ??= new ConfigViewModel(_settings),
            "Notifier"  => _notifier  ??= new NotifierViewModel(_settings),
            "Console"   => _console   ??= new ConsoleViewModel(_launcher),
            "Analytics" => _analytics ??= new AnalyticsViewModel(),
            "Bazaar"    => _bazaar    ??= new BazaarViewModel(),
            _           => _dashboard ??= new DashboardViewModel(),
        };
    }

    // ── Script control (INTEGRATION POINT) ──

    /// <summary>
    /// Start the Rust backend process and connect the WebSocket.
    /// Replace the body with real BackendClient / process launcher calls.
    /// </summary>
    public void StartScript()
    {
        StatusText = "Starting…";
        OnPropertyChanged(nameof(StatusChipColor));
        // INTEGRATION POINT: _launcher.Start(); _socket.ConnectAsync();
    }

    /// <summary>Stop the Rust backend process and disconnect the WebSocket.</summary>
    public void StopScript()
    {
        StatusText = "Stopped";
        OnPropertyChanged(nameof(StatusChipColor));
        // INTEGRATION POINT: _socket.Disconnect(); _launcher.Stop();
    }
}
