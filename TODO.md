# TODO — Next Steps for frikadellen-fancy

A prioritised list of improvements, features, and fixes that should be tackled next.
Items are grouped by area and roughly ordered by impact.

---

## 1 · Incomplete Features (in-code TODOs)

- [ ] **Bazaar window handling** (`src/handlers/bazaar_flip_handler.rs:459`)
  Implement the `open_window` event listener so bazaar flip automation can
  actually interact with the in-game GUI (parse windows, click slots, handle
  sign input).

- [ ] **Profile selection** (`src/bot/client.rs:1841`)
  The `SwapProfile` command currently sends `/profiles` but never reads the
  resulting menu window. Add window-open handling to select the correct
  profile slot.

- [ ] **Trade window handling** (`src/bot/client.rs:1848`)
  The `AcceptTrade` command sends `/trade` but does not process the trade
  window UI. Implement the full accept/decline flow.

- [ ] **Operation interruption** (`src/handlers/flip_handler.rs:342`)
  When a higher-priority flip arrives the bot currently waits for the idle
  state. Add the ability to interrupt an in-progress auction-house operation
  so more profitable flips are not missed.

- [ ] **Additional local commands** (`src/main.rs:1814`)
  Implement `forceClaim`, `connect`, and `sellbz` local console commands.

---

## 2 · Error Handling & Robustness

- [ ] **Replace critical `.unwrap()` calls with proper error handling**
  Over 25 bare `.unwrap()` calls exist. High-priority targets:
  - `src/web/mod.rs:41` — server panics on port-bind failure.
  - `src/discord/mod.rs` — multiple `data.get::<T>().unwrap()` calls that
    crash if a TypeMap key is absent.
  - `src/main.rs:1825` — JSON serialisation unwrap.

- [ ] **Move Regex compilation to statics**
  Several `Regex::new(...).unwrap()` calls in hot paths
  (`src/bot/handlers.rs`, `src/handlers/bazaar_flip_handler.rs`). Use
  `once_cell::sync::Lazy` (already a dependency) to compile them once.

- [ ] **Config input validation**
  Add validation in `src/config/` for obviously wrong values (negative
  delays, port numbers outside 1–65535, empty `ingame_name`, etc.) and
  surface clear errors at startup.

---

## 3 · Testing

- [ ] **Expand unit-test coverage**
  The project has 14 test-bearing files. Areas that still need coverage:
  - Flip skip-filter logic (profit thresholds, percentage, skin flag).
  - Config parsing edge cases (missing keys, invalid types).
  - Discord embed builder output.
  - Web API route responses (status codes, JSON shapes).

- [ ] **Add integration / end-to-end smoke tests**
  A basic test that boots the bot in a mock environment and verifies the
  startup flow (config load → auth skip → WebSocket mock → idle state)
  would catch regressions early.

---

## 4 · Web GUI

- [ ] **Flip history endpoint**
  Add `GET /api/flips` returning past flip attempts (item, price, profit,
  outcome) so the dashboard can display a log/table.

- [ ] **Profit summary endpoint**
  Add `GET /api/stats` with session totals (coins spent, coins earned, net
  profit, flips attempted/succeeded).

- [ ] **Frontend polish**
  The web dashboard is functional but minimal. Consider adding charts
  (profit over time), a settings editor, and mobile-friendly styling.

---

## 5 · Discord Bot

- [ ] **`!flips` command**
  List recent flip attempts with profit/outcome (mirrors the proposed
  `/api/flips` endpoint).

- [ ] **`!config` command**
  Allow changing hot-reloadable settings (skip filters, delays) without
  editing `config.toml` and restarting.

- [ ] **Slash-command migration**
  Discord is deprecating message-content-based bots. Migrate `!start`,
  `!stop`, `!status` to native `/start`, `/stop`, `/status` slash commands.

---

## 6 · Avalonia UI

- [ ] **Wire up to live backend**
  `Frikadellen.UI/` currently runs on mocked data. Connect it to the web
  GUI's REST + WebSocket API so it becomes a real control panel.

- [ ] **Launch backend from UI**
  Optionally spawn the Rust binary as a child process when the Avalonia app
  starts, and surface its stdout/stderr in a console pane.

---

## 7 · CI / CD & DevOps

- [ ] **Dependency security auditing**
  Add `cargo audit` (or `cargo deny`) to the CI pipeline to catch known
  vulnerabilities in dependencies.

- [ ] **Code-coverage reporting**
  Integrate `cargo-tarpaulin` or `cargo-llvm-cov` and publish results (e.g.
  to Codecov) so coverage trends are visible.

- [ ] **Clippy in sccache-enabled step**
  `ci.yml` currently sets `RUSTC_WRAPPER: ""` for the Clippy step, forcing a
  redundant build. Investigate whether sccache can be kept enabled.

---

## 8 · Documentation

- [ ] **Architecture overview**
  Add a `docs/architecture.md` (or a section in the README) with a
  high-level diagram of the modules and data flow (auth → WebSocket →
  flip handler → bot client → notifications).

- [ ] **CONTRIBUTING.md**
  Guide for new contributors: how to set up the dev environment, run tests,
  and submit PRs.

- [ ] **Changelog**
  Start a `CHANGELOG.md` (or use GitHub Releases notes consistently) so
  users can see what changed between versions.

---

## 9 · Code Quality

- [ ] **Reduce `pub` surface**
  Several internal helpers are marked `pub` without being used outside their
  module. Tighten visibility where possible to keep the public API small.

- [ ] **Consistent logging levels**
  Audit `tracing::info!` / `warn!` / `error!` usage to make sure log levels
  are consistent (e.g. recoverable issues → `warn`, fatal → `error`).

- [ ] **Clippy pedantic pass**
  Run `cargo clippy -- -W clippy::pedantic` and address the extra warnings
  to raise overall code quality.
