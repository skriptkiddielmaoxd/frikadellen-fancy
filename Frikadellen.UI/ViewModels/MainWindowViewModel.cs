using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;
using Avalonia.Threading;
using Frikadellen.UI.Models;
using Frikadellen.UI.Services;

namespace Frikadellen.UI.ViewModels;

public sealed class MainWindowViewModel : ViewModelBase
{
    private readonly BackendClient _backend;
    private readonly BackendSocket _socket;
    private readonly RustProcessLauncher _launcher = new();

    // Shared collections — populated from WebSocket events, consumed by child VMs
    public ObservableCollection<EventItem> Events { get; } = new();
    public ObservableCollection<FlipRecord> Flips { get; } = new();
    public ObservableCollection<ChatMessage> ChatLog { get; } = new();
    public ObservableCollection<InventorySlot> InventorySlots { get; } = new();

    private ViewModelBase _currentView = null!;
    private DashboardViewModel? _dashboard;
    private EventsViewModel? _events;
    private ConfigViewModel? _config;
    private SettingsViewModel? _settings;

    private bool _isSidebarExpanded = true;
    private string _statusText = "Stopped";

    public MainWindowViewModel()
    {
        _backend = new BackendClient(8080);
        _socket = new BackendSocket(8080);

        // Pre-fill 36 inventory slots (Minecraft inventory layout: 4 rows × 9 cols)
        for (int i = 0; i < 36; i++) InventorySlots.Add(new InventorySlot { Index = i });

        _dashboard = new DashboardViewModel(this, _backend, Events, Flips, ChatLog, InventorySlots);
        _events = new EventsViewModel(Events);
        _config = new ConfigViewModel(_backend);
        _settings = new SettingsViewModel();
        _currentView = _dashboard;

        NavigateCommand = new RelayCommand(o => Navigate(o?.ToString()));
        ToggleSidebarCommand = new RelayCommand(() => IsSidebarExpanded = !IsSidebarExpanded);
        ToggleThemeCommand = new RelayCommand(() => App.ToggleTheme());

        // Wire WebSocket events
        _socket.StatusReceived += OnStatusReceived;
        _socket.EventReceived += OnEventReceived;
        _socket.FlipReceived += OnFlipReceived;
        _socket.RelistReceived += OnRelistReceived;
        _socket.BazaarFlipReceived += OnBazaarFlipReceived;
        _socket.ConnectionChanged += OnConnectionChanged;

        // If the Rust process dies unexpectedly, update the UI
        _launcher.ProcessExited += code => Dispatcher.UIThread.Post(() =>
        {
            StatusText = $"Crashed (exit {code})";
            _dashboard?.UpdateRunningState(false);
        });

        // Pipe Rust stdout/stderr into the chat log
        _launcher.OutputReceived += OnRustOutput;
    }

    public ViewModelBase CurrentView
    {
        get => _currentView;
        set => SetField(ref _currentView, value);
    }

    public string StatusText
    {
        get => _statusText;
        set => SetField(ref _statusText, value);
    }

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

    public double SidebarWidth => _isSidebarExpanded ? 200 : 56;
    public string SidebarCollapseIcon => _isSidebarExpanded ? "◀" : "▶";

    public ICommand NavigateCommand { get; }
    public ICommand ToggleSidebarCommand { get; }
    public ICommand ToggleThemeCommand { get; }

    public void Navigate(string? target)
    {
        CurrentView = target switch
        {
            "Events" => _events ??= new EventsViewModel(Events),
            "Config" => _config ??= new ConfigViewModel(_backend),
            "Settings" => _settings ??= new SettingsViewModel(),
            _ => _dashboard ??= new DashboardViewModel(this, _backend, Events, Flips, ChatLog, InventorySlots)
        };
    }

    /// <summary>Spawn the Rust backend process and connect the WebSocket.</summary>
    public void StartScript()
    {
        if (_launcher.IsRunning) return;

        var ok = _launcher.Start();
        if (!ok)
        {
            StatusText = "Backend not found";
            return;
        }

        StatusText = "Starting…";
        // Give the Rust binary a moment to bind the port, then connect
        _socket.ConnectAsync();
    }

    /// <summary>Kill the Rust backend process and disconnect the WebSocket.</summary>
    public void StopScript()
    {
        _socket.Disconnect();
        _launcher.Stop();
        StatusText = "Stopped";
        _dashboard?.UpdateRunningState(false);
    }

    /// <summary>True when the Rust process is alive.</summary>
    public bool IsBackendRunning => _launcher.IsRunning;

    // ────────── WebSocket event handlers ──────────

    private void OnStatusReceived(StatusDto status)
    {
        Dispatcher.UIThread.Post(() =>
        {
            StatusText = status.Running ? "Running" : status.State;
            _dashboard?.UpdateStatus(status);
        });
    }

    private void OnEventReceived(string kind, string message)
    {
        var clean = MinecraftColorParser.StripCodes(message);

        Dispatcher.UIThread.Post(() =>
        {
            if (kind == "chat")
            {
                // Skip empty / whitespace-only chat messages (pure color code strings)
                if (string.IsNullOrWhiteSpace(clean)) return;

                var spans = MinecraftColorParser.Parse(message);
                // Double-check spans have actual visible text
                if (spans.Count == 0 || spans.TrueForAll(s => string.IsNullOrWhiteSpace(s.Text)))
                    return;

                ChatLog.Add(new ChatMessage
                {
                    Sender = "[Server]",
                    Text = clean,
                    Color = "#DFE6E9",
                    Spans = spans,
                });
                if (ChatLog.Count > 500) ChatLog.RemoveAt(0);
            }
            else
            {
                // System events, purchases, sales, bazaar, listings go to the events panel
                var avatar = kind switch
                {
                    "error" => "🔴",
                    "purchase" => "🛒",
                    "sold" => "⚡",
                    "bazaar" => "📦",
                    "listing" => "🏷️",
                    _ => "🔵",
                };
                var type_ = kind switch
                {
                    "error" => "Error",
                    "purchase" => "Purchase",
                    "sold" => "Sale",
                    "bazaar" => "Trade",
                    "listing" => "Listing",
                    _ => "Info",
                };
                var evt = new EventItem
                {
                    Type = type_,
                    Message = clean,
                    Tag = kind,
                    Avatar = avatar,
                };
                Events.Add(evt);
                if (Events.Count > 500) Events.RemoveAt(0);

                // Mark matching inventory slot as listed when an auction listing succeeds
                if (kind == "listing")
                {
                    foreach (var s in InventorySlots)
                    {
                        if (s.HasItem && !s.Listed && clean.Contains(s.CleanName, StringComparison.OrdinalIgnoreCase))
                        {
                            s.Listed = true;
                            break;
                        }
                    }
                }

                // Clear inventory slots when the item is sold
                if (kind == "sold")
                {
                    foreach (var s in InventorySlots)
                    {
                        if (s.HasItem && clean.Contains(s.CleanName, StringComparison.OrdinalIgnoreCase))
                        {
                            var slotRef = s;
                            _ = Task.Delay(TimeSpan.FromSeconds(2)).ContinueWith(_ =>
                                Dispatcher.UIThread.Post(() => slotRef.Clear()));
                            break;
                        }
                    }
                }
            }
        });
    }

    private void OnFlipReceived(string item, long cost, long target, long profit, long? buySpeedMs, string? tag)
    {
        var flip = new FlipRecord
        {
            ItemName = item,
            BuyPrice = cost,
            SellPrice = target,
            BuySpeedMs = buySpeedMs,
            Finder = "SNIPER",
            NameSpans = MinecraftColorParser.Parse(item),
            ItemTag = tag,
        };

        Dispatcher.UIThread.Post(() =>
        {
            Flips.Insert(0, flip);
            if (Flips.Count > 200) Flips.RemoveAt(Flips.Count - 1);
            _dashboard?.TrackFlip(flip);
        });
    }

    private void OnRelistReceived(string item, long sellPrice, long buyCost, long profit, long duration, int slot, string? tag)
    {
        // Accept both mineflayer numbering (9-44) and 0-based (0-35)
        int idx;
        if (slot >= 9 && slot <= 44)
            idx = slot - 9;   // mineflayer inventory window slots
        else if (slot >= 0 && slot < 36)
            idx = slot;        // 0-based inventory index
        else
            idx = -1;

        System.Diagnostics.Debug.WriteLine($"[UI] OnRelistReceived: item='{item}', sell={sellPrice}, buy={buyCost}, profit={profit}, slot={slot}→idx={idx}, tag={tag}");

        Dispatcher.UIThread.Post(() =>
        {
            InventorySlot? target = null;
            if (idx >= 0 && idx < InventorySlots.Count)
                target = InventorySlots[idx];
            else
            {
                // No valid slot — place in first empty slot
                foreach (var s in InventorySlots)
                    if (!s.HasItem) { target = s; break; }
            }
            if (target != null)
            {
                target.Fill(item, sellPrice, buyCost, profit, tag, MinecraftColorParser.Parse(item));
                System.Diagnostics.Debug.WriteLine($"[UI] Filled slot {target.Index}: HasItem={target.HasItem}, ImageUrl={target.ImageUrl}");
            }
            else
            {
                System.Diagnostics.Debug.WriteLine("[UI] OnRelistReceived: No available slot found!");
            }
        });
    }

    private void OnBazaarFlipReceived(string item, int amount, long pricePerUnit, bool isBuy)
    {
        var label = isBuy ? "BUY" : "SELL";
        var msg = $"[BZ] {label}: {item} x{amount} @ {CoinFormat.Short(pricePerUnit)}/unit";
        var evt = new EventItem
        {
            Type = "Trade",
            Message = msg,
            Tag = "bazaar",
            Avatar = "📦",
        };

        Dispatcher.UIThread.Post(() =>
        {
            Events.Add(evt);
            if (Events.Count > 500) Events.RemoveAt(0);
        });
    }

    private void OnConnectionChanged(bool connected)
    {
        Dispatcher.UIThread.Post(() =>
        {
            StatusText = connected ? "Connected" : "Disconnected";
        });
    }

    private void OnRustOutput(string raw, bool isError)
    {
        var clean = LogLineCleaner.Clean(raw);
        if (clean == null) return;

        var level = LogLineCleaner.ExtractLevel(raw);
        var color = level switch
        {
            "ERROR" => "#FF5555",
            "WARN"  => "#FFAA00",
            _       => "#B2BEC3",
        };
        var prefix = isError ? "[BAF ERR]" : "[BAF]";

        Dispatcher.UIThread.Post(() =>
        {
            ChatLog.Add(new ChatMessage
            {
                Sender = prefix,
                Text = clean,
                Color = color,
                IsSystem = true,
                Spans = new List<ChatSpan> { new() { Text = clean, Color = color } },
            });
            if (ChatLog.Count > 500) ChatLog.RemoveAt(0);
        });
    }

    // ────────── Helpers ──────────

    // Color parsing now handled by MinecraftColorParser in Models.cs
}
