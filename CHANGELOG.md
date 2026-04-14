# Changelog

All notable changes to Aeromux will be documented in this file.

## [0.6.1] — Unreleased

### Changed

- **Enum Validation Performance** — Replaced all 55 `Enum.IsDefined` reflection calls in the Mode S parsing hot path with zero-allocation range checks and pre-computed lookup tables, eliminating boxing allocations and reflection overhead per message parse.
- **Frame Extraction Performance** — Reuse pre-allocated buffers during preamble detection phase testing, eliminating ~99% of short-lived byte array allocations in the signal processing hot path.
- **TCP Broadcast Performance** — Replaced per-frame client list copying with a copy-on-write volatile snapshot, eliminating up to 3,000 list allocations per second across Beast, JSON, and SBS broadcasters.
- **Encoder Output Performance** — Replaced per-frame `byte[]` allocations in Beast, JSON, and SBS encoders with reusable instance buffers returning `ReadOnlyMemory<byte>` slices, eliminating 5,000–10,000+ short-lived allocations per second across all three broadcast formats.
- **Database Lookup Performance** — Removed unnecessary lock and pre-created the parameterized SQLite command in the aircraft database lookup service, eliminating per-lookup command allocation and lock overhead.
- **Aircraft Tracking Performance** — Changed the aircraft state dictionary from string to `uint` ICAO keys, replacing per-lookup 6-character string hashing with single-instruction integer hashing on the 1,000+/sec update hot path.
- **Comm-B Parsing Performance** — Replaced per-message `byte[]` allocation and `Array.Copy` for the 7-byte MB field with a zero-copy `ReadOnlySpan<byte>` slice into the existing frame data, eliminating a heap allocation per DF 20/21 message.
- **Update Event Performance** — Replaced `EventHandler<AircraftUpdateEventArgs>` with `Action<Aircraft, Aircraft>` for the aircraft update event, eliminating a per-frame heap allocation on the 1,000+/sec update path.

### Fixed

- **ICAO Confidence Tracker Thread Safety** — Fixed `NullReferenceException` crash in `IcaoConfidenceTracker.CleanupExpired` caused by concurrent dictionary access from multiple SDR device workers sharing the same tracker instance. Added `ReaderWriterLockSlim` to allow concurrent read access on the high-frequency `IsConfident` hot path while serializing write operations.

## [0.6.0] — 2026-04-09

### Added

- **Web Map** — Built-in browser-based map for real-time aircraft visualization with interactive aircraft list, detail view, position trails, and real-time updates. Served directly by the daemon with no external web server required. See the [Web Map Guide](docs/WEBMAP.md).
- **Web Map Range Rings** — Three range rings (100/150/200 nm) centered on the receiver location with distance labels and a center point marker. Distances follow the selected unit (nautical miles, kilometers, or miles). Toggleable from the settings panel.
- **Web Map Search Highlighting** — Matched text is highlighted in orange in the search dropdown across callsign, ICAO, and registration fields.
- **Web Map Sortable Columns** — Aircraft list columns (callsign, altitude, speed, distance) are sortable by clicking headers, with sort preferences persisted in the browser.
- **Web Map Settings Panel** — Unified control panel with search input, unit switching (speed, altitude, distance), interface settings, and reset to defaults behind a gear icon.
- **Web Map Dark Overlay** — Semi-transparent dark overlay on map tiles for improved aircraft marker contrast.
- **Web Map Hover Tooltip** — Tooltip showing callsign, ICAO, speed, and altitude when hovering over aircraft markers, tracking the aircraft position continuously.
- **Web Map Flight Profile** — Dual-axis chart in the aircraft detail view showing barometric altitude and ground speed over time, with historical data loaded on selection and real-time updates.

### Changed

- **Substring Search** — Aircraft search API changed from prefix matching to substring matching, allowing queries like "65" to find "WZZ652".
- **Rate Limiting Removed** — Removed API rate limiting. Rate limiting belongs at the reverse proxy layer for local network APIs.
- **Aircraft Detail Fields** — Added magnetic declination, downlink request, utility message, data link capability, and supported BDS registers to the detail endpoint and web map detail view.
- **Nautical Miles** — Added nautical miles (nm) as a distance unit in the web map and TUI, now the default. Distance can be toggled between nautical miles, statute miles, and kilometers.
- **History Buffers** — Circular buffers now skip consecutive duplicate entries, preserving more meaningful data for position trails, altitude graphs, and velocity analysis.
- **Async Notarization** — macOS packaging script now supports async notarization with separate `--submit`, `--staple`, and `--validate` flags, allowing submission without waiting for Apple's approval. The synchronous `--notarize` flag remains available.

### Documentation

- Added [Web Map Guide](docs/WEBMAP.md) with screenshots.
- Updated [API Guide](docs/API.md) for substring search and new VelocityAndDynamics fields.
- Updated [README](README.md) with Web Map feature and screenshot.

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

[0.6.1]: https://github.com/aeromux/aeromux/releases/tag/v0.6.1
[0.6.0]: https://github.com/aeromux/aeromux/releases/tag/v0.6.0
[0.5.0]: https://github.com/aeromux/aeromux/releases/tag/v0.5.0
