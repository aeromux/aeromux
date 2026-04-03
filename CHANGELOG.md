# Changelog

All notable changes to Aeromux will be documented in this file.

## [0.5.0] — 2026-04-03

Initial public release.

### Added

- **Mode S and ADS-B Decoding** — All Mode S downlink formats (DF 0–24), ADS-B Extended Squitter message types (TC 1–31), and Comm-B data registers (BDS 1,0 through BDS 6,5). Covers identification, position, altitude, speed, heading, vertical rate, autopilot, meteorology, ACAS/TCAS, transponder capabilities, and data quality.
- **Multiple RTL-SDR Support** — Simultaneous operation of multiple RTL-SDR USB receivers with independent gain, PPM correction, and preamble threshold settings per device. Automatic frame aggregation and deduplication.
- **Beast TCP Input** — Connect to one or more external Beast-compatible servers (dump1090, readsb, or another Aeromux instance). Can be used alone or combined with local SDR devices. Automatic reconnection with exponential backoff.
- **Unified Input Model** — SDR devices and Beast TCP sources can be freely combined. All sources are merged into a single deduplicated stream.
- **Beast Output** — Binary protocol compatible with dump1090, readsb, and MLAT networks.
- **SBS/BaseStation Output** — CSV text protocol compatible with Virtual Radar Server. AIR, ID, and all eight MSG subtypes.
- **JSON Output** — Streaming NDJSON with consolidated aircraft state. Per-aircraft rate limiting (1 update/sec). Schema matches the REST API for client compatibility.
- **REST API** — Read-only JSON API: aircraft list, aircraft detail (10 sections, selectable), position/altitude/velocity history, receiver statistics, and health check. Per-client rate limiting.
- **MLAT Support** — Receives multilateration position data from mlat-client for aircraft that do not broadcast ADS-B positions.
- **Live Mode (TUI)** — Interactive terminal interface with sortable aircraft list (F1–F6), ICAO/callsign search with highlighting, detail view with field search and jump-and-highlight navigation, and display unit switching (distance, altitude, speed).
- **Daemon Mode** — Background service for continuous, unattended operation with all data served over the network.
- **Aircraft Database Enrichment** — Integration with the [aeromux-db](https://github.com/aeromux/aeromux-db) database for registration, operator, manufacturer, and aircraft type metadata. Built-in commands for download, update, and integrity verification.
- **Device Discovery** — `aeromux device` command to list detected RTL-SDR hardware with optional detailed tuner parameters.
- **Configuration** — Single YAML file with per-setting CLI override (CLI > YAML > Default). Fully documented example configuration with sensible defaults.
- **Cross-Platform** — Self-contained, single-file executables for macOS (Intel and Apple Silicon) and Linux (x64 and ARM64, including Raspberry Pi 4/5).
- **Documentation** — [CLI Reference](docs/CLI.md), [TUI Guide](docs/TUI.md), [REST API Guide](docs/API.md), [Broadcast Guide](docs/BROADCAST.md), [Deployment Scenarios](docs/SCENARIOS.md), and [Architecture Guide](docs/ARCHITECTURE.md).

---

The format is based on [Keep a Changelog](https://keepachangelog.com/), and this project adheres to [Semantic Versioning](https://semver.org/).

[0.5.0]: https://github.com/aeromux/aeromux/releases/tag/v0.5.0
