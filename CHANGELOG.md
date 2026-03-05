# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/).

---

## [Unreleased]

### Added
- **Anti-detection & humanisation module** (`src/anti_detection/`) — jittered delays, sign-typing cadence, movement simulation, bazaar check intervals, session-cycling helpers, and a full `AntiDetectionConfig` TOML section (`[anti_detection]`).
- `TODO.md` — prioritised list of next steps across features, error handling, testing, and docs.

### Changed
- **CI workflow** reworked to check-only (no release build in CI); sccache enabled; release job chain fixed.
- **Release workflow** now passes the version tag as an ISCC preprocessor define so the installer filename always matches the tag (e.g. `FrikadellenBAF_Setup_v3.1.0.exe`).
- **README** expanded with a complete Discord bot setup guide, full `config.toml` reference table, skip-filter table, and troubleshooting section.
- Separated bot token from the shareable `config.toml` so users can share configs safely.
- GitHub Actions YAML updated to pass all actionlint / shellcheck checks.

---

## [3.0.0] — 2026-03-05

Tag: [`fixedish`](https://github.com/skriptkiddielmaoxd/frikadellen-fancy/releases/tag/fixedish)

### Added
- **Windows installer** — one-click Inno Setup bundle (`FrikadellenBAF_Setup_v3.0.0.exe`) shipping both the Rust backend and the Avalonia desktop UI with a Start-menu / desktop shortcut.
  - SHA-256: `5C7C59BA32EB4E410AE17D36355D48C76DCA8948390D82CA78C30E73224D3909`

### Fixed
- Suppressed noisy Azalea entity/packet overflow logs by tightening the application logging filter.

### Changed
- Added `recover_clone/` to `.gitignore` to prevent accidental commits of local recovery data.

---

## Earlier history

This project is an extended fork of [frikadellen-baf-121](https://github.com/TreXito/frikadellen-baf-121) by [@TreXito](https://github.com/TreXito). See the upstream repository for the history prior to the fork.

[Unreleased]: https://github.com/skriptkiddielmaoxd/frikadellen-fancy/compare/fixedish...HEAD
[3.0.0]: https://github.com/skriptkiddielmaoxd/frikadellen-fancy/releases/tag/fixedish
