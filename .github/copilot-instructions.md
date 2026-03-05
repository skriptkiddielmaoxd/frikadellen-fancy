# Copilot Instructions — frikadellen-fancy

## Prime Directive

> **Anti-detection is the #1 priority in every decision.**
> Every line of code, every feature, every timing value, every packet sent must
> be evaluated through the lens of: *"Does this increase or decrease the chance
> of a Watchdog / anti-cheat ban?"*  If a change improves functionality but
> raises detection risk, it **must not ship** without a mitigation plan.

---

## Project Context

**frikadellen-fancy** is a Hypixel Skyblock auction-house / bazaar flip bot
written in **Rust** (Azalea framework) with optional Discord bot, web GUI,
and Avalonia desktop UI.  It targets the Hypixel Minecraft server and must
remain undetected by Watchdog and any server-side anti-cheat heuristics.

| Layer | Tech |
|---|---|
| Core bot | Rust nightly, Azalea |
| Desktop UI | C# / .NET 8, Avalonia 11 |
| Web dashboard | Rust (built-in HTTP server), HTML/JS |
| Notifications | Discord bot (serenity) + webhook |
| Config | `config.toml` (serde/toml) |

---

## Anti-Detection Rules (MUST follow)

### 1. Timing & Delays
- **Never use fixed / constant delays** for in-game actions.  Always add
  **randomised jitter** (gaussian or uniform) around a configurable baseline.
- `command_delay_ms` and `flip_action_delay` must be treated as *minimums*;
  actual delay = baseline + random(0 … baseline × jitter_factor).
- Between *any two* server-bound packets that a human would not send
  back-to-back, insert a human-plausible pause (50–400 ms range with
  variance).
- Avoid bursty packet patterns.  If multiple actions must happen in sequence,
  spread them with irregular spacing.

### 2. Behavioural Mimicry
- Simulate realistic mouse / click timings when interacting with inventory
  windows (slot clicks, sign GUIs, confirmation dialogs).
- Vary the order of operations occasionally — e.g. don't always check the
  same AH page in the same order.
- Periodically perform *idle / decoy* actions: small inventory opens, chat
  scrolls, or head-rotation packets that a human player would naturally
  produce.
- Never act on a flip faster than a human plausibly could.  Floor the
  reaction time to a configurable minimum (default ≥ 150 ms).

### 3. Packet Hygiene
- Never send packets that the vanilla client would not send in the same game
  state.  Cross-reference Azalea's packet layer with vanilla behaviour.
- Do not send movement packets while a GUI window is supposed to be open
  (vanilla client freezes movement during container screens).
- Match packet field values (action numbers, slot indices, window IDs) to
  what the server expects after the most recent `OpenScreen` packet.
- Implement proper window-transaction / sequence-number tracking.

### 4. Session Behaviour
- Do not flip 24/7 without breaks.  Implement configurable **session
  windows** with randomised online/offline durations.
- Cap flips-per-hour to a human-plausible rate.
- On disconnect or kick, wait a randomised back-off period before
  reconnecting (not a fixed retry timer).
- Rotate behaviour profiles (flip aggressiveness, bazaar vs AH weighting)
  across sessions so the account's pattern isn't static.

### 5. Logging & Fingerprint Avoidance
- **Never** include bot-identifying strings, version tags, or project names
  in any data sent to the server (chat packets, plugin channel messages,
  brand strings).
- Ensure the client brand packet matches a vanilla or popular launcher
  (e.g. `vanilla`, `lunarclient`, `fabric`).
- Strip or spoof any handshake / forge-mod-list data that Azalea may
  include by default.

### 6. Config Safety
- Validate all timing values at startup: reject values that are
  unrealistically low (< 50 ms for action delays) and warn on values that
  are suspiciously uniform.
- Default config values must be **conservative** (slower, safer).
  Power-users may tune down, but defaults should minimise ban risk
  out-of-the-box.

---

## Code Quality Rules

1. **No bare `.unwrap()`** on anything that touches network I/O, config
   parsing, or window state.  Use `?`, `.unwrap_or_default()`, or explicit
   error handling.  (See TODO § 2.)
2. **Compile regexes once** — use `once_cell::sync::Lazy` or `LazyLock` for
   any `Regex::new()` call.
3. **Minimise `pub` surface** — internal helpers must not be `pub` unless
   used cross-module.
4. **Consistent logging** — `tracing` crate; use `warn!` for recoverable
   issues, `error!` for fatal, `debug!`/`trace!` for noisy internals.
   Never log secrets (tokens, passwords).
5. Prefer **`Arc<RwLock<T>>`** or **`DashMap`** over `Mutex` for shared
   state that is read-heavy.
6. Every new feature must include or update **unit tests**.

---

## Architecture Notes

```
main.rs  →  config/       (load & validate config.toml)
         →  bot/client.rs (Azalea client, packet I/O, window tracking)
         →  handlers/
              ├─ flip_handler.rs         (AH flip logic)
              └─ bazaar_flip_handler.rs  (bazaar flip logic)
         →  discord/       (serenity bot + webhook sender)
         →  web/           (HTTP dashboard + API)
```

When modifying **any handler or bot/client code**, always ask:
- Does this change the packet cadence?
- Does this introduce a detectable pattern?
- Is the timing humanised?

---

## PR / Commit Checklist

Before merging any change, verify:

- [ ] No new detection vectors introduced (timing, packet, behavioural).
- [ ] Randomised jitter applied to every new delay or action sequence.
- [ ] No bot-identifying strings leak to the server.
- [ ] No bare `.unwrap()` on fallible paths.
- [ ] Existing tests pass; new tests added where applicable.
- [ ] `cargo clippy` clean (no new warnings).

---

## Language & Stack Preferences

- **Rust** for all bot logic (performance + safety).
- **C# / Avalonia** for desktop UI only.
- **HTML/JS** for the web dashboard (kept lightweight).
- Dependencies: prefer well-audited, minimal crates.  Avoid large frameworks
  that bloat the binary or add detectable fingerprints.

---

*This file is the single source of truth for development guidelines.
Update it whenever anti-detection strategy evolves.*