# Frikadellen UI – Avalonia Desktop Prototype

A standalone Avalonia 11 desktop application that visually resembles the Frikadellen
localhost web UI but runs entirely on mocked data. The window uses custom chrome
(no native title bar), rounded corners, drop shadow, and animated interactions.

---

## Prerequisites

| Requirement | Version |
|---|---|
| .NET SDK | **8.0** or later |
| OS | Windows 10+, Linux, or macOS (built primarily for Windows) |

Avalonia packages are pulled automatically via NuGet – no Avalonia workload or
template install is required.

## Build

```bash
dotnet build src/Frikadellen.UI/Frikadellen.UI.sln
```

## Run

```bash
dotnet run --project src/Frikadellen.UI/Frikadellen.UI.csproj -c Debug
```

## Publish (single-file Windows exe)

```bash
dotnet publish src/Frikadellen.UI/Frikadellen.UI.csproj \
  -c Release -r win-x64 \
  -p:SelfContained=true \
  -p:PublishSingleFile=true
```

The output will be in `src/Frikadellen.UI/bin/Release/net8.0/win-x64/publish/`.

---

## Features

### Custom Window Chrome
- Native title bar removed (`SystemDecorations=None`)
- Draggable custom top bar with logo, app title, and status chip
- Rounded 16 px corners with soft drop shadow
- Animated close / minimize / maximize buttons (double-click title bar to maximise)
- Light / dark theme toggle (◐ button) with smooth crossfade

### Dashboard (mocked)
- Big pill-shaped Start/Stop toggle with colour morph animation
  - Keyboard shortcuts: **Ctrl+S** = Start, **Ctrl+T** = Stop
- Four status cards (Script state, Purse, Queue Depth, Bot Status)
  - Staggered entrance animation (translate + fade)
  - Hover lift (scale + shadow)
- Metrics update on 1.5 s timer when running

### Live Events (mocked)
- Random events appended every 2.5 s while script runs
- Slide-in animation per item, avatar & tag chips
- Click item to expand details in the right panel

### Settings
- Token (masked), Channel ID, Publish Path inputs
- Save button persists to `./config/ui-settings.json`

### Sidebar
- Collapsible sidebar with icon + label navigation
- Smooth width transition animation

### Accessibility
- Keyboard navigation & focus visuals (Avalonia Fluent theme)
- Accessible labels / tooltips on all interactive controls
- High-contrast readable colour palette

---

## Project Structure

```
src/Frikadellen.UI/
├── Frikadellen.UI.sln
├── Frikadellen.UI.csproj
├── app.manifest
├── Program.cs
├── App.axaml / App.axaml.cs
├── Assets/
│   ├── logo.png          # 128×128 brand logo
│   ├── logo.svg          # Vector version
│   └── icon.ico          # 32×32 app icon
├── Models/
│   └── Models.cs         # EventItem, UiSettings
├── Services/
│   ├── MockDataService.cs
│   └── SettingsService.cs
├── ViewModels/
│   ├── ViewModelBase.cs
│   ├── RelayCommand.cs
│   ├── BoolToStringConverter.cs
│   ├── MainWindowViewModel.cs
│   ├── DashboardViewModel.cs
│   ├── EventsViewModel.cs
│   └── SettingsViewModel.cs
├── Views/
│   ├── MainWindow.axaml / .axaml.cs
│   ├── DashboardView.axaml / .axaml.cs
│   ├── EventsView.axaml / .axaml.cs
│   └── SettingsView.axaml / .axaml.cs
└── README.md
```

---

## Note

This app is a **standalone prototype** – all data is mocked locally.
No network calls, no real tokens, no external dependencies beyond Avalonia.
Settings are stored as plain JSON on disk for convenience; this is acceptable
for prototype use only.
