# Changelog

All notable changes to Aeromux will be documented in this file.

## [0.7.0] — Unreleased

### Added

- **Web Map Aircraft Photos** — New "Photo" section in the aircraft detail panel showing a representative photo of the airframe sourced from [Planespotters.net](https://www.planespotters.net/). Photos are fetched lazily on selection, with a loading skeleton, photographer attribution, and a clickable link back to the photo's page on planespotters.net (required by Planespotters' usage rules). Aeromux caches **photo metadata only** (URL + photographer + link); the browser fetches the JPEG directly from the Planespotters CDN and uses its own HTTP cache. Toggleable from the settings panel under "Aircraft photos" (default on, persisted in localStorage).
- **REST API: Aircraft Photo Endpoint** — `GET /api/v1/aircraft/{icao}/photo` returns photo metadata (`HasPhoto`, `ThumbnailUrl`, `Photographer`, `Link`) sourced from Planespotters.net. Cached in memory and evicted when the aircraft expires from the tracker, with a 1000-entry LRU safety cap. Transient upstream failures (429, 5xx, network, timeout) return 502 and are not cached. Documented in [API Guide](docs/API.md). Attribution to Planespotters.net is mandatory for downstream consumers.

## [0.6.2] — 2026-04-25

### Fixed

- **Memory Leak: ICAO String Cache** — Fixed unbounded growth of the ICAO hex string cache in `ValidatedFrameFactory`. In AP mode (DF 0/4/5/16/20/21), every noise frame produced a random ICAO that was cached permanently. With multiple factory instances (2 per SDR device + 1 per Beast connection), caches could grow to ~2.3 GB over hours of operation. The cache now clears when it exceeds 65,536 entries (~4.5 MB). Real aircraft ICAOs are re-cached within seconds.
- **Memory Leak: Unbounded Subscriber Channels** — Fixed unbounded memory growth in `ReceiverStream` and `BeastStream` subscriber channels. When a TCP broadcast client was slow or unresponsive, its subscriber channel accumulated frames indefinitely (~1 GB/hour at 300 fps). Subscriber channels are now bounded to 50,000 frames (~20 MB) with a drop-oldest policy. The `AircraftStateTracker` processes frames in microseconds and is unaffected by the limit.
- **Slow TCP Client Starvation** — Fixed a single slow TCP client blocking the entire broadcast loop, starving all other clients. `WriteAsync` had no timeout, so a client with a full send buffer would block indefinitely. Added a 5-second per-client write timeout that disconnects unresponsive clients with a warning log, allowing other clients to continue receiving data.
- **Derived Geometric Altitude Crash** — Fixed `ArgumentOutOfRangeException` crash when the derived geometric altitude (barometric + delta from TC 19) exceeded the valid range [-2000, 126700] feet. The unhandled exception killed the `AircraftStateTracker` consumer task, causing all aircraft to expire and the web map to go empty while Beast/SBS broadcasts continued working. The derivation now validates the sum before creating the `Altitude` value. The consumer task also catches handler exceptions to prevent any single bad frame from killing the tracking pipeline.
- **Surface CPR Southern/Western Hemisphere** — Fixed incorrect surface position decoding for receivers in the Southern or Western Hemisphere. C#'s `%` operator on negative coordinates preserves the sign, producing wrong latitude/longitude zone indices. This shifted decoded surface positions by one zone width (~1.5° latitude / ~90 NM). Added a `CprMod` helper that always returns positive modulo results.
- **Altitude/Velocity Unit Conversion Rounding** — Fixed unit conversions in `Altitude` and `Velocity` value objects truncating instead of rounding. `(int)` cast truncated toward zero, causing systematic 1-unit drift (e.g., `FromFeet(35000).Meters` → `FromMeters(10668).Feet` = 34999 instead of 35000). Affects TC 20-22 GNSS geometric altitudes (decoded in meters, stored as feet) and all metric display values in the web map and REST API. Replaced `(int)(...)` with `(int)Math.Round(...)` in all conversion paths.
- **Supersonic Velocity Crash** — Fixed `ArgumentOutOfRangeException` thrown when parsing TC 19 supersonic velocity subtypes (2 and 4). The `Velocity` value object capped at 1500 knots but supersonic encoding can produce up to 4088 knots per axis. Every supersonic velocity message threw, was caught as an "unexpected error," and the message was silently dropped. Raised the cap to 4096 knots to cover the full supersonic encoding range. Ground speed magnitudes above 4096 (both axes simultaneously maxed) are rejected as corrupt data. Also removed workaround guards in Comm-B handlers that silently dropped valid BDS 5,0 velocities in the 1501-2046 knot range.
- **Web Map Push Service Silent Failures** — Added error logging to `MapHubPushService.ExecuteAsync` catch block. Previously, all exceptions were swallowed silently, causing the web map to stop updating with no diagnostic trail.
- **Web Map Slow Client Blocking** — Added a 5-second per-client push timeout in `MapHubPushService`. A single slow SignalR client could block the sequential push loop, delaying updates to all other clients. Timed-out clients are skipped for the current tick and self-heal on the next push cycle.
- **History API Unbounded Limit** — Clamped the `?limit=` query parameter on the history API endpoint to a maximum of 10,000 entries. Previously, any positive integer was accepted, allowing a single request to serialize unbounded history data.
- **Static File Missing Cache Headers** — Added `Cache-Control: public, max-age=86400` to embedded web map static files. Browsers previously re-fetched unchanged assets (JS, CSS, fonts) on every page load.
- **Database Asset Filename Validation** — Added path traversal validation for the database asset filename before passing it to `InstallDatabase`. Rejects filenames containing invalid characters, directory traversal sequences (`..`), or path separators.
- **Daemon Shutdown Delay** — Removed redundant explicit `StopAsync` call before `DisposeAsync` on the web application. `DisposeAsync` internally calls `StopAsync`, so the explicit call added an unnecessary shutdown timeout delay.
- **Non-Loopback Bind Address Warning** — Added a log warning when the daemon binds to a non-loopback address (including the default `0.0.0.0`), informing that services are accessible from the network.
- **Database Download ANSI Escape Codes** — Fixed garbled progress output when stdout is redirected (piped to file, CI runners, non-VT terminals). ANSI cursor movement codes are now only emitted to TTY terminals; redirected output prints progress at 25% intervals instead.

## [0.6.1] — 2026-04-17

### Changed

- **Web Map Detail Panel Layout Persistence** — Section expand/collapse and "show more" toggle states in the aircraft detail panel now persist when switching between aircraft. Previously, selecting a new aircraft reset all sections to their default state. Added a "Reset layout" link in the detail toolbar to restore defaults.
- **Enum Validation Performance** — Replaced all 55 `Enum.IsDefined` reflection calls in the Mode S parsing hot path with zero-allocation range checks and pre-computed lookup tables, eliminating boxing allocations and reflection overhead per message parse.
- **Frame Extraction Performance** — Reuse pre-allocated buffers during preamble detection phase testing, eliminating ~99% of short-lived byte array allocations in the signal processing hot path.
- **TCP Broadcast Performance** — Replaced per-frame client list copying with a copy-on-write volatile snapshot, eliminating up to 3,000 list allocations per second across Beast, JSON, and SBS broadcasters.
- **Encoder Output Performance** — Replaced per-frame `byte[]` allocations in Beast, JSON, and SBS encoders with reusable instance buffers returning `ReadOnlyMemory<byte>` slices, eliminating 5,000–10,000+ short-lived allocations per second across all three broadcast formats.
- **Database Lookup Performance** — Removed unnecessary lock and pre-created the parameterized SQLite command in the aircraft database lookup service, eliminating per-lookup command allocation and lock overhead.
- **Aircraft Tracking Performance** — Changed the aircraft state dictionary from string to `uint` ICAO keys, replacing per-lookup 6-character string hashing with single-instruction integer hashing on the 1,000+/sec update hot path.
- **Comm-B Parsing Performance** — Replaced per-message `byte[]` allocation and `Array.Copy` for the 7-byte MB field with a zero-copy `ReadOnlySpan<byte>` slice into the existing frame data, eliminating a heap allocation per DF 20/21 message.
- **Update Event Performance** — Replaced `EventHandler<AircraftUpdateEventArgs>` with `Action<Aircraft, Aircraft>` for the aircraft update event, eliminating a per-frame heap allocation on the 1,000+/sec update path.

### Added

- **Web Map Aircraft Category Dots** — Colored category indicator dots in the aircraft list and detail panel header, matching the map marker colors: blue for normal, green for military, red for privacy (LADD/PIA). Category colors are defined as shared CSS classes used across the legend, list, and detail views.
- **Web Map Category-Colored Trail** — Aircraft position trail now uses the selected aircraft's category color (dark blue for normal, dark green for military, dark crimson for privacy) instead of a fixed blue gradient.
- **Web Map Range Outline** — Coverage polygon connecting the farthest aircraft positions received in each 1-degree bearing sector over the last 4 hours. Positions beyond 300 nm are discarded. Rendered as a semi-transparent fill with a dashed border below the range rings. On by default, toggleable from the settings panel. The toggle is disabled when receiver location is not configured. Resets on restart.
- **CPR Position Validation** — Three-layer position validation to eliminate wildly incorrect aircraft positions (appearing as long straight lines across the map) caused by CPR global decode errors from bit corruption passing CRC. Layer 1: receiver range check rejects decoded positions beyond 300 NM from receiver (skipped when receiver location is not configured). Layer 2: speed/distance plausibility check validates that position changes are physically possible given elapsed time and aircraft speed. Layer 3: position persistence counter requires 4 consecutive implausible positions before overwriting a known good position, preventing single bad decodes from corrupting tracks.

### Fixed

- **ICAO Confidence Tracker Thread Safety** — Fixed `NullReferenceException` crash in `IcaoConfidenceTracker.CleanupExpired` caused by concurrent dictionary access from multiple SDR device workers sharing the same tracker instance. Added `ReaderWriterLockSlim` to allow concurrent read access on the high-frequency `IsConfident` hot path while serializing write operations.
- **Beast Timestamp MLAT Precision** — Fixed mlat-client reporting "clock unstable" by deriving 12 MHz Beast timestamps from the cumulative RTL-SDR sample counter instead of wall-clock time. The previous approach captured `DateTime` at USB buffer callbacks, introducing 1–5 ms of OS scheduling jitter — far exceeding MLAT's ~1 μs requirement. The new pipeline computes `(cumulative_sample_count + sample_offset) × 5` at frame extraction, producing timestamps tied to the radio crystal oscillator with zero jitter. BeastEncoder now passes through the pre-computed 12 MHz value directly. BeastParser preserves the 48-bit wire timestamp for relay scenarios.
- **Beast Parser Stream Resynchronization** — Fixed BeastParser silently consuming valid frame data when encountering unknown Beast message types. The parser now skips unrecognized types and rescans for the next frame start marker, preventing stream desync from corrupted or unsupported messages.
- **MLAT Feedback Loop** — Fixed MLAT-computed positions being re-broadcast on the Beast output port (30005), causing mlat-client to warn "Ignored N messages with MLAT magic timestamp". The Beast broadcaster now filters out `FrameSource.Mlat` frames, preventing synthetic DF 17 positions received from mlat-client (port 30104) from looping back.
- **Web Map Selected Aircraft False Expiration** — Fixed the aircraft detail panel incorrectly showing `[EXPIRED]` when panning the map away from the selected aircraft's position. The viewport-based removal logic was sending `AircraftRemoved` for aircraft that simply left the visible area, which the client interpreted as a true expiration. The selected aircraft is now excluded from viewport-based removal, with its lifecycle managed solely by the dedicated selected-aircraft block that checks the actual tracker state.
- **Velocity Flickering Between TC 19 Subtypes** — Fixed Track and Heading values nullifying each other when an aircraft alternates between TC 19 subtype 1-2 (ground speed → Track) and subtype 3-4 (airspeed → Heading) messages. The handler's `with` expression overwrote both fields on every message, setting the one not provided by the current subtype to `null`. This caused flickering in the detail panel and map icon rotation defaulting to north when only Heading was available. The handler now uses a second `with` that only updates the directional field the current subtype provides, preserving the other from previous state.

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

[0.7.0]: https://github.com/aeromux/aeromux/releases/tag/v0.7.0
[0.6.2]: https://github.com/aeromux/aeromux/releases/tag/v0.6.2
[0.6.1]: https://github.com/aeromux/aeromux/releases/tag/v0.6.1
[0.6.0]: https://github.com/aeromux/aeromux/releases/tag/v0.6.0
[0.5.0]: https://github.com/aeromux/aeromux/releases/tag/v0.5.0
