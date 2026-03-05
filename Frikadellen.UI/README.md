# Frikadellen UI – Overhauled Avalonia Desktop UI

A complete visual overhaul of the Frikadellen Avalonia desktop UI, built for easy merge into `frikadellen-fancy`. All backend logic has been stubbed out with clear `// INTEGRATION POINT` comments — drop in the real `BackendClient`, `BackendSocket` and `RustProcessLauncher` calls to go live.

---

## Design

| Attribute | Detail |
|---|---|
| Theme | Deep midnight-navy dark (`#0A0F1E` base) |
| Accent | Indigo/violet `#818CF8` + sky-blue `#38BDF8` |
| Corners | 12–24 px radius **everywhere** — zero sharp corners |
| Font | Inter (bundled via `Avalonia.Fonts.Inter`) |
| Animations | Splash bounce-in, staggered card entrance, sidebar width transition, button colour morph, view fade+slide |

---

## Screens

### Splash Screen
- Logo mark scales in with a `BackEaseOut` spring bounce
- `FRIKADELLEN` title slides up and fades in
- Tagline and progress bar reveal with staggered delays
- Shimmer gradient progress bar (`#818CF8 → #38BDF8`)
- "Ready." status then transitions to the main shell

### Account Setup (Login)
- Shown on first run (when `config/ui-settings.json → FirstRunComplete: false`)
- Card slides up from below with `CubicEaseOut`
- Form fields stagger in individually
- `Connect Account` → fires Microsoft device-code auth (INTEGRATION POINT)
- `Skip for now` → proceeds directly to main shell

### Main Shell
- **Custom chrome**: no native title bar; draggable top bar with logo, status chip, theme toggle, min/max/close
- **Collapsible sidebar**: 220 px expanded / 60 px collapsed; smooth width transition
- **Page transitions**: fade + 16 px slide-up per view change

### Dashboard
- Live **stat cards** (Script state, Purse, Queue depth, Session profit) with staggered entrance
- Big pill **Start / Stop** button with colour-morph transition (violet ↔ red)
- **Latest Flip** highlight card
- **Recent Flips** table with item, buy/sell prices, profit (+colour), speed badge
- Empty state with friendly placeholder when no flips yet
- Keyboard shortcuts: `Ctrl+S` = Start, `Ctrl+T` = Stop

### Events
- Slide-in feed of purchases, sales, bazaar trades, listings and errors
- Type badge + timestamp per row
- Right panel shows expanded detail for selected event

### Config
- Grouped settings cards: Flip Modes, Timing & Delays, Behaviour, Skip Filter, Network
- All fields bound to `UiSettings` and saved to `config/ui-settings.json`
- Save button with "Saved ✓" flash confirmation

### Notifier
- Discord bot token field with show/hide toggle
- Channel ID, Webhook URL inputs
- Bot command quick-reference table (`!start`, `!stop`, `!status`)

---

## Build & Run

**Requirements:** .NET 8 SDK

```bash
# Build
dotnet build Frikadellen.UI/Frikadellen.UI.sln

# Run (dev)
dotnet run --project Frikadellen.UI/Frikadellen.UI.csproj

# Publish single-file Windows exe
dotnet publish Frikadellen.UI/Frikadellen.UI.csproj \
  -c Release -r win-x64 \
  -p:SelfContained=true \
  -p:PublishSingleFile=true
```

---

## Integration Guide

Search for `// INTEGRATION POINT` across the ViewModels — each comment describes exactly what real call to substitute:

| File | What to plug in |
|---|---|
| `MainWindowViewModel.cs` | `_launcher.Start()` / `_socket.ConnectAsync()` / `_launcher.Stop()` |
| `LoginViewModel.cs` | Real Microsoft device-code auth flow |
| `DashboardViewModel.cs` | Wire `ToggleRequested` to `MainWindowViewModel.StartScript/StopScript` |

The `SettingsService` already reads/writes `config/ui-settings.json` — replace/extend `UiSettings` to match your real `config.toml` schema.

---

## Project Structure

```
Frikadellen.UI/
├── Frikadellen.UI.sln / .csproj
├── Program.cs                        entry point
├── App.axaml / .cs                   global theme + startup
├── Models/Models.cs                  EventItem, FlipRecord, UiSettings, Fmt
├── Services/
│   ├── MockDataService.cs            random events/flips for demo
│   └── SettingsService.cs            JSON persistence
├── ViewModels/
│   ├── MainWindowViewModel.cs        root lifecycle: Splash → Login → Shell
│   ├── SplashViewModel.cs            animated progress steps
│   ├── LoginViewModel.cs             first-run account setup
│   ├── DashboardViewModel.cs         stats, toggle, flip feed
│   ├── EventsViewModel.cs            live event collection
│   ├── ConfigViewModel.cs            all config.toml fields
│   ├── NotifierViewModel.cs          Discord bot + webhook
│   └── BoolToStringConverter.cs      IValueConverter helpers
└── Views/
    ├── MainWindow.axaml/.cs          custom chrome + phase routing
    ├── SplashView.axaml/.cs
    ├── LoginView.axaml/.cs
    ├── DashboardView.axaml/.cs
    ├── EventsView.axaml/.cs
    ├── ConfigView.axaml/.cs
    └── NotifierView.axaml/.cs
```
