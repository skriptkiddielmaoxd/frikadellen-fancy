# frikadellen-fancy

[![GitHub issues](https://img.shields.io/github/issues/skriptkiddielmaoxd/frikadellen-fancy)](https://github.com/skriptkiddielmaoxd/frikadellen-fancy/issues)
[![GitHub stars](https://img.shields.io/github/stars/skriptkiddielmaoxd/frikadellen-fancy)](https://github.com/skriptkiddielmaoxd/frikadellen-fancy/stargazers)
[![License](https://img.shields.io/github/license/skriptkiddielmaoxd/frikadellen-fancy)](./LICENSE)

> **⚠️ WARNING — USE AT YOUR OWN RISK**
>
> This project automates interactions with Hypixel Skyblock. Using automation tools like this **DOES** violate Hypixel's Terms of Service and may result in account suspension or a permanent ban. The authors and maintainers are not responsible for any account actions or punishments resulting from use. Proceed only if you understand the risks.

---

## Overview

**frikadellen-fancy** is an extended version of [frikadellen-baf-121](https://github.com/TreXito/frikadellen-baf-121) by [@TreXito](https://github.com/TreXito).

The core bot is written in **Rust** using [Azalea](https://github.com/azalea-rs/azalea) and adds the following on top of the upstream:

- **Discord bot** — control the script and receive flip/status notifications directly in Discord.
- **Discord webhook** — lightweight one-way flip notifications to any webhook URL.
- **Web GUI** — a local dashboard served on `http://localhost:<port>` showing live events and metrics.
- **Avalonia UI** — an optional cross-platform desktop wrapper (`Frikadellen.UI/`) built with .NET 8.
- **Windows installer** — a one-click Inno Setup bundle that ships both the Rust backend and the Avalonia UI.
- **Parallel tracking** — the codebase closely tracks upstream changes for compatibility.

---

## Features

| Feature | Description |
|---|---|
| 🎯 AH Flips | Monitors Coflnet WebSocket for BIN auction opportunities and executes them automatically |
| 📈 Bazaar Flips | Automated bazaar order placement and cancellation |
| 🔐 Microsoft Auth | Secure Microsoft/Minecraft account login via device-code flow |
| 🤖 Discord Bot | `!start` / `!stop` / `!status` commands; rich embed notifications on flips, purchases, bans |
| 🪝 Discord Webhook | One-way webhook for flip notifications (no bot required) |
| 🌐 Web GUI | Real-time dashboard at `http://localhost:8080` (password-protected) |
| 🖥️ Avalonia UI | Optional cross-platform desktop UI (prototype, mocked data) |
| ⚙️ Configurable | All behaviour controlled via a single `config.toml` file |

---

## Getting Started

### Prerequisites

- A Minecraft: Java Edition account linked to Microsoft
- Access to the Hypixel server

### Quick start (pre-built binary)

1. Go to the [Releases](../../releases) page and download the binary for your platform.
2. Run the executable.
3. Enter your Minecraft username when prompted.
4. Complete Microsoft authentication in the browser that opens.
5. The bot connects to Hypixel and starts monitoring for flips.

> **Windows users** – a one-click installer is available: `FrikadellenBAF_Setup_v3.0.0.exe`.
> It bundles the Rust backend and the Avalonia desktop UI and creates a Start-menu / desktop shortcut.

### Build from source

Requires the **Rust nightly** toolchain:

```bash
# Install nightly toolchain (once)
rustup install nightly
rustup default nightly

# Clone & build
git clone https://github.com/skriptkiddielmaoxd/frikadellen-fancy.git
cd frikadellen-fancy
cargo build --release
./target/release/frikadellen-fancy
```

---

## Configuration (`config.toml`)

The application creates `config.toml` in the same directory as the binary on first run.
You can edit it directly while the bot is stopped.

### Key settings

| Key | Default | Description |
|---|---|---|
| `ingame_name` | *(prompted)* | Your Minecraft username |
| `enable_ah_flips` | `true` | Enable auction house flipping |
| `enable_bazaar_flips` | `false` | Enable bazaar order flipping |
| `web_gui_port` | `8080` | Port for the web dashboard |
| `web_gui_password` | `null` | Password to protect the web dashboard (leave unset = no auth) |
| `flip_action_delay` | `150` | Milliseconds to wait before acting on a flip |
| `command_delay_ms` | `500` | Minimum delay between consecutive in-game commands |
| `auction_duration_hours` | `24` | Duration for your own AH listings |
| `auto_cookie` | `0` | Auto-buy booster cookies (0 = disabled) |
| `fastbuy` | `false` | Skip the confirmation window for faster BIN purchases |
| `webhook_url` | `null` | Discord webhook URL for flip notifications (see below) |
| `discord_bot_token` | `null` | Discord bot token to enable the bot (see below) |
| `discord_channel_id` | `null` | Restrict bot commands to a specific channel (optional) |

### Skip filters (`[skip]`)

| Key | Default | Description |
|---|---|---|
| `min_profit` | `1000000` | Skip flips with profit below this value (coins) |
| `min_price` | `10000000` | Skip flips with item price below this value |
| `profit_percentage` | `50.0` | Minimum profit percentage |
| `user_finder` | `false` | Skip user-finder flips |
| `skins` | `false` | Skip skin items |
| `always` | `false` | Skip all flips (pause without stopping the bot) |

### Example `config.toml`

```toml
ingame_name = "YourUsername"
enable_ah_flips = true
enable_bazaar_flips = false
web_gui_port = 8080
discord_bot_token = "YOUR_BOT_TOKEN_HERE"
discord_channel_id = 1234567890123456789

[skip]
min_profit = 2000000
min_price = 5000000
profit_percentage = 20.0
```

---

## Discord Bot Setup

The built-in Discord bot lets you control the script and receive rich notifications without opening a terminal.

### Step 1 — Create a Discord application

1. Go to [https://discord.com/developers/applications](https://discord.com/developers/applications) and click **New Application**.
2. Give it a name (e.g. `Frikadellen BAF`) and click **Create**.
3. In the left sidebar go to **Bot** and click **Add Bot** → **Yes, do it!**
4. Under **Token** click **Reset Token** (or **Copy** if shown), then copy the token — you will need it shortly.
5. Scroll down and make sure **MESSAGE CONTENT INTENT** is **enabled** (the bot reads `!start` / `!stop` / `!status` from chat).

### Step 2 — Invite the bot to your server

1. In the left sidebar go to **OAuth2 → URL Generator**.
2. Under **Scopes** tick `bot`.
3. Under **Bot Permissions** tick:
   - `Send Messages`
   - `Embed Links`
   - `Read Message History`
4. Copy the generated URL, open it in your browser, and invite the bot to your Discord server.

### Step 3 — Add the token to `config.toml`

Open `config.toml` (created next to the binary on first run) and add:

```toml
discord_bot_token = "YOUR_BOT_TOKEN_HERE"
```

Optionally restrict commands to one channel by right-clicking the channel in Discord → **Copy Channel ID** (you may need **Developer Mode** enabled in Settings → Advanced):

```toml
discord_channel_id = 1234567890123456789
```

Start the bot — it will log `Discord bot connected as <BotName>` and send an **online** embed to the channel.

### Available commands

| Command | Description |
|---|---|
| `!start` | Resume flip purchasing |
| `!stop` | Pause flip purchasing (bot stays connected to Hypixel) |
| `!status` | Show current state, purse, bot status, and queue depth |

The bot also sends automatic embeds for:
- Bot coming online
- Startup workflow complete (auth, order discovery, cookie)
- Every item purchased (price, target, profit, buy speed)
- Sell confirmations
- Disconnect / ban events

---

## Discord Webhook (one-way notifications)

A webhook is a lighter alternative if you only need flip notifications and don't need remote control.

1. In your Discord server go to **Channel Settings → Integrations → Webhooks → New Webhook**.
2. Copy the webhook URL.
3. Add it to `config.toml`:

```toml
webhook_url = "https://discord.com/api/webhooks/..."
```

Leave `discord_bot_token` unset — the two features are independent.
Set `webhook_url = ""` to disable webhook prompts entirely.

---

## Web GUI

A real-time dashboard is served at `http://localhost:<web_gui_port>` (default `http://localhost:8080`) while the bot is running.

To password-protect it:

```toml
web_gui_password = "Ch@ngeMe123!"
```

---

## Avalonia UI (optional)

`Frikadellen.UI/` contains a cross-platform desktop prototype built with [Avalonia 11](https://avaloniaui.net/) and .NET 8.
It currently runs on **mocked data** — it is a UI shell, not yet wired to the live backend.

**Requirements:** .NET 8 SDK

```bash
# Build
dotnet build Frikadellen.UI/Frikadellen.UI.sln

# Run
dotnet run --project Frikadellen.UI/Frikadellen.UI.csproj -c Debug
```

See [`Frikadellen.UI/README.md`](./Frikadellen.UI/README.md) for full details.

---

## Troubleshooting

| Problem | Fix |
|---|---|
| Microsoft auth fails | Re-run the app and complete the device-code login flow again in the browser |
| Bot connects but no flips | Check `enable_ah_flips = true` in `config.toml`; verify your Coflnet subscription |
| Discord bot not responding | Confirm `MESSAGE CONTENT INTENT` is enabled in the developer portal and the token is correct |
| Web GUI not loading | Check `web_gui_port` is not blocked by a firewall; default is `8080` |
| `not a terminal` error | Run the binary directly from a terminal, or set the `FRIKADELLEN_INGAME_NAME` env var |

---

## License

[MIT](./LICENSE)
