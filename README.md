# Aeromux

[![License: GPL v3](https://img.shields.io/badge/License-GPLv3-blue.svg)](LICENSE.md)
[![.NET](https://img.shields.io/badge/.NET-10.0-512bd4)](https://dotnet.microsoft.com)
[![Platform](https://img.shields.io/badge/Platform-macOS%20%7C%20Linux-lightgrey)]()

**A multi-SDR Mode S and ADS-B demodulator and decoder for .NET**

Aeromux receives aircraft transponder signals on 1090 MHz using inexpensive RTL-SDR USB receivers, decodes Mode S and ADS-B messages, and serves the decoded aircraft data over the network. It can also connect to external Beast TCP sources (dump1090, readsb, or another Aeromux instance) and aggregate their frames alongside local SDR devices. It supports multiple SDR devices simultaneously for improved coverage and runs on macOS and Linux, including Raspberry Pi.

**Plug in your SDR, edit one YAML file, and start tracking.**

Most ADS-B setups involve chaining separate tools for demodulation, decoding, and output — each with its own quirks, configuration format, and failure modes. Aeromux is a single, self-contained binary controlled by one [fully documented](aeromux.example.yaml) YAML file with sensible defaults. Everything works together out of the box — from raw signal processing to network output — without requiring you to wire up separate components. Seven complete [deployment scenarios](docs/SCENARIOS.md), from a single SDR on a Raspberry Pi to multi-site aggregation across a network, are documented end to end with working configurations.

## Features

- **Mode S and ADS-B Decoding** — Decodes aircraft identification, position, altitude, speed, heading, and more from transponder broadcasts. Covers all Mode S downlink formats, ADS-B Extended Squitter message types, and Comm-B data registers.

- **Multiple Receiver Support** — Use several RTL-SDR devices at once to improve reception coverage, or connect to external Beast TCP sources, or both. Multi-device operation is a built-in feature — just list your sources in the configuration file. Frames from all sources are automatically combined and deduplicated.

- **Network Output** — In daemon mode, serves decoded data over TCP in three standard formats. See the [Broadcast Guide](docs/BROADCAST.md) for full documentation.
  - **Beast** — Binary protocol compatible with dump1090, readsb, and most ADS-B tools
  - **SBS/BaseStation** — Text protocol compatible with Virtual Radar Server
  - **JSON** — Streaming format for web applications and custom integrations

- **MLAT Support** — Accepts multilateration position data from mlat-client, enabling position tracking of aircraft that do not broadcast ADS-B.

- **REST API** — In daemon mode, serves a read-only JSON API for web interfaces, map visualizations, and third-party integrations. Provides aircraft list, detail, history, statistics, and health endpoints with rate limiting. See the [API Guide](docs/API.md) for full documentation.

- **Beast TCP Input** — Connect to one or more external Beast-compatible servers (dump1090, readsb, or another Aeromux daemon) over the network. Beast sources can be used alone or combined with local SDR devices. Includes automatic reconnection with exponential backoff if a connection drops.

- **Live Mode** — Interactive terminal interface showing tracked aircraft in real time, with a detail view displaying aircraft registration, operator, and type information from the [aeromux-db](https://github.com/nandortoth/aeromux-db) database. Includes column sorting, search, unit switching, and detail view field search with jump-and-highlight navigation. See the [TUI Guide](docs/TUI.md) for full documentation.

- **Daemon Mode** — Runs as a background service for continuous, unattended operation with all data served over the network via TCP protocols and the REST API.

- **Cross-Platform** — Runs on macOS (Intel and Apple Silicon) and Linux (x64 and ARM64, including Raspberry Pi 4/5).

### Live Mode Preview

```
                                        AIRCRAFT LIST - Aeromux
┌────────┬──────────┬──────────┬────────────┬───────────┬──────────┬──────────┬────────┬───────────┬───┐
│ ICAO   │ Callsign │ Altitude │   Vertical │  Distance │    Speed │ Messages │ Signal │ Last seen │   │
├────────┼──────────┼──────────┼────────────┼───────────┼──────────┼──────────┼────────┼───────────┼───┤
│ 06A13C │ QTR3293  │ 37000 ft │     0 ft/m │  193.2 mi │  472 kts │      191 │  -25.9 │  0.1s ago │ █ │
│ 392AED │ N/A      │ 37000 ft │   -64 ft/m │  203.2 mi │  450 kts │       40 │  -27.3 │  0.1s ago │ █ │
│ 3965AF │ AFR274   │ 31000 ft │     0 ft/m │   55.3 mi │  477 kts │      322 │   -4.3 │  0.0s ago │ █ │
│ 3C6593 │ DLH8KC   │ 39025 ft │     0 ft/m │    8.8 mi │  461 kts │      300 │   -5.7 │  0.0s ago │ █ │
│ 4007EE │ BAW2231  │ 33000 ft │     0 ft/m │   79.2 mi │  470 kts │      340 │   -9.7 │  0.1s ago │ █ │
│ 407994 │ BAW15    │ 31000 ft │     0 ft/m │   37.7 mi │  493 kts │      360 │  -10.2 │  0.1s ago │ █ │
│ 4081BF │ N/A      │ 39000 ft │    64 ft/m │  137.6 mi │  454 kts │      319 │  -19.8 │  0.0s ago │ █ │
│ 440020 │ AUA7     │ 30150 ft │   832 ft/m │   32.2 mi │  494 kts │      419 │   -2.9 │  0.1s ago │ ░ │
│ 4408FB │ AUA15    │ 30725 ft │   576 ft/m │   21.4 mi │  485 kts │      396 │   -3.1 │  0.0s ago │ ░ │
│ 440A8D │ TAY1841  │ 33000 ft │     0 ft/m │   30.7 mi │  485 kts │      427 │   -3.8 │  0.0s ago │ ░ │
│ 471DBD │ WZZ5070  │  3425 ft │ -1280 ft/m │   29.6 mi │  200 kts │       20 │  -27.3 │  0.3s ago │ ░ │
│ 471F5C │ WMT559   │ 39000 ft │     0 ft/m │  122.3 mi │  451 kts │      208 │  -17.8 │  0.1s ago │ ░ │
│ 471FA2 │ N/A      │ 35000 ft │     0 ft/m │  136.5 mi │  429 kts │       28 │  -30.1 │  0.9s ago │ ░ │
│ 474808 │ N/A      │      N/A │        N/A │       N/A │      N/A │        7 │  -29.0 │  0.0s ago │ ░ │
│ 4891B2 │ ENT7646  │ 38000 ft │     0 ft/m │   24.3 mi │  422 kts │      393 │  -14.9 │  0.1s ago │ ░ │
│ 48C2B6 │ RYR46TT  │ 10675 ft │ -1856 ft/m │   20.5 mi │  320 kts │      274 │   -2.7 │  0.2s ago │ ░ │
└────────┴──────────┴──────────┴────────────┴───────────┴──────────┴──────────┴────────┴───────────┴───┘
  F1: ICAO ▲  F2: Callsign  F3: Altitude  F4: Vertical  F5: Distance  F6: Speed             F12: Reset
  Aircraft: 34 | Selected: 1/34 | Viewport: 1-16                         Dist: mi | Alt: ft | Spd: kts
  ↑/↓: Row, ←/→: Page, Home/End                       ENTER: Details, D/A/S: Units, /: Search, Q: Quit
```

See the [TUI Guide](docs/TUI.md) for full keyboard reference, sorting, search, and detail view documentation.

## Installation

Pre-built packages are available on the [GitHub Releases](https://github.com/nandortoth/aeromux/releases) page.

### Debian / Raspberry Pi

Download the `.deb` package for your architecture (ARM64 for Raspberry Pi, x64 for standard Linux) and install:

```bash
sudo dpkg -i aeromux_0.5.0-1_arm64.deb
```

The package installs a systemd service that starts automatically. Configuration is at `/etc/aeromux/aeromux.yaml`. Requires `librtlsdr0` — install with `sudo apt-get install librtlsdr0`.

See the [Debian Packaging Guide](docs/PACKAGING-DEB.md) for details on configuration, service management, and upgrade behavior.

### macOS

Download the `.pkg` installer for your architecture (Apple Silicon or Intel) and double-click to install, or use the command line:

```bash
sudo installer -pkg aeromux_0.5.0_macos_arm64.pkg -target /
```

The installer places the binary at `/opt/aeromux/bin/aeromux` with a symlink in `/usr/local/bin/` for PATH access. Configuration and data are stored under `~/Library/Application Support/aeromux/`. Requires `librtlsdr` — install with `brew install librtlsdr`.

See the [macOS Packaging Guide](docs/PACKAGING-PKG.md) for details on configuration, uninstall, and upgrade behavior.

### Docker

A multi-arch Docker image is available on GitHub Container Registry, supporting both ARM64 (Raspberry Pi) and x86-64:

```bash
# Copy the Compose template and start
curl -O https://raw.githubusercontent.com/nandortoth/aeromux/main/docker/docker-compose.yaml
docker compose up -d
```

The image runs Aeromux in daemon mode with a default configuration. The aircraft database is downloaded automatically on first start. To customize, extract the default config, edit it, and mount it as a volume:

```bash
docker run --rm ghcr.io/nandortoth/aeromux:latest cat /etc/aeromux/aeromux.yaml > aeromux.yaml
nano aeromux.yaml
# Uncomment the config volume mount in docker-compose.yaml, then restart
docker compose up -d
```

USB device passthrough for RTL-SDR is supported on Linux and Raspberry Pi. On macOS and Windows, USB passthrough is not supported — use Beast TCP input sources instead.

See the [Docker Guide](docs/DOCKER.md) for details on configuration, USB passthrough, database management, and offline installation.

## Building from Source

### Prerequisites

- Microsoft [.NET 10.0 SDK](https://dotnet.microsoft.com/download) or later
- An RTL-SDR USB receiver (with R820T/R820T2 tuner)
- The `librtlsdr` native library:
  - **macOS:** `brew install librtlsdr`
  - **Debian/Ubuntu:** `sudo apt-get install librtlsdr0`

### Build and Run

```bash
# Clone the repository
git clone https://github.com/nandortoth/aeromux.git
cd aeromux

# Build a self-contained executable (auto-detects your platform)
./build.sh

# Or build and download the aircraft metadata database in one step
./build.sh --with-database

# Copy the example configuration and edit it for your setup
cp aeromux.example.yaml aeromux.yaml

# Run in daemon mode (the build output shows the exact binary path for your platform)
./artifacts/binaries/osx-arm64/aeromux daemon --config aeromux.yaml
```

The binary path depends on your platform: `osx-arm64`, `osx-x64`, `linux-x64`, or `linux-arm64`. The build script prints the correct path when it finishes.

Alternatively, use the convenience script, which builds and presents an interactive menu:

```bash
./run.sh
```

## Usage

Aeromux provides five commands:

```bash
# Daemon mode — runs in the background, serves data on network ports
aeromux daemon --config aeromux.yaml

# Daemon mode with Beast input — aggregates frames from an external Beast source
aeromux daemon --beast-source 192.168.1.100:30005 --config aeromux.yaml

# Live mode — interactive terminal display using SDR device(s) from config
aeromux live --config aeromux.yaml

# Live mode with Beast source — connects to an existing Beast data source
aeromux live --beast-source host:port --config aeromux.yaml

# Combined mode — SDR devices and Beast sources together
aeromux live --sdr-source --beast-source host:port --config aeromux.yaml

# Database management — download, update, and verify the aircraft metadata database
aeromux database update --database artifacts/db/
aeromux database info --database artifacts/db/

# Device discovery — list RTL-SDR devices to find device indices and tuner gains for aeromux.yaml
aeromux device
aeromux device --verbose

# Version — shows version and runtime information
aeromux version
aeromux version --verbose
```

**Daemon mode** is for unattended operation: it decodes signals and makes the data available over the network for other tools to consume. **Live mode** adds a real-time terminal display showing all tracked aircraft. Both commands support SDR sources, Beast TCP sources, or both simultaneously. **Database** manages the aircraft metadata database downloaded from GitHub releases, with integrity verification. **Device** lists RTL-SDR hardware detected on the system and, with `--verbose`, shows detailed tuner parameters. See the [CLI Reference](docs/CLI.md) for all commands, parameters, and the configuration priority model.

## Configuration

Aeromux uses a YAML configuration file. Copy [`aeromux.example.yaml`](aeromux.example.yaml) as your starting point — it contains detailed comments explaining every option.

The main sections are:

- **`sdrSources`** — Your RTL-SDR receivers. Configure gain, frequency correction (PPM), preamble sensitivity, and enable or disable individual devices.
- **`beastSources`** — External Beast TCP input sources. Connect to dump1090, readsb, or another Aeromux instance. Can be used alongside SDR sources.
- **`network`** — Which output protocols to enable (Beast, SBS, JSON) and their TCP ports. Also configures the REST API port/toggle and bind address.
- **`tracking`** — Controls how strictly aircraft are filtered. The confidence level determines how many detections are required before an aircraft is reported, reducing false positives from noise.
- **`receiver`** — Your station's geographic location (latitude, longitude, altitude). This is needed for surface vehicle position decoding and for MLAT triangulation.
- **`database`** — Aircraft metadata database settings. Configure the storage path and enable database enrichment for aircraft identification data.
- **`mlat`** — Multilateration input settings. When enabled, Aeromux can receive position data from mlat-client for aircraft that do not broadcast ADS-B positions.
- **`logging`** — Log level, console and file output, log rotation, and retention.

## Network Ports

| Port  | Protocol   | Description                                          |
|-------|------------|------------------------------------------------------|
| 30005 | Beast      | Binary protocol, compatible with dump1090 and readsb |
| 30003 | SBS        | BaseStation text format, compatible with VRS         |
| 30006 | JSON       | Streaming JSON for web applications                  |
| 8080  | HTTP       | REST API (JSON, read-only)                           |
| 30104 | MLAT Input | Receives positions from mlat-client                  |

All ports are configurable in the YAML configuration file. Protocols can be individually enabled or disabled.

## Supported Platforms

Aeromux builds as a self-contained, single-file executable for the following platforms:

| Platform             | Architecture  | Runtime ID     |
|----------------------|---------------|----------------|
| macOS (Apple Silicon)| ARM64         | `osx-arm64`    |
| macOS (Intel)        | x64           | `osx-x64`      |
| Linux                | x64           | `linux-x64`    |
| Linux (Raspberry Pi) | ARM64         | `linux-arm64`  |

The build script auto-detects your platform, or you can cross-compile for a specific target:

```bash
./build.sh --target linux-arm64
```

Pre-built `.deb` and `.pkg` packages are available on the [GitHub Releases](https://github.com/nandortoth/aeromux/releases) page. See [Installation](#installation) for details.

## Contributing

Contributions are welcome! Whether it is a bug fix, a new feature, improved documentation, or additional tests, we appreciate your help.

Please read the [Contributing Guide](CONTRIBUTING.md) for development setup, coding standards, and the pull request process. For an in-depth look at the data flow, signal processing pipeline, and concurrency model, see the [Architecture Guide](docs/ARCHITECTURE.md). For complete, end-to-end deployment examples — from a single SDR to multi-site aggregation — see the [Deployment Scenarios](docs/SCENARIOS.md) guide. This project follows the [Contributor Covenant Code of Conduct](CODE_OF_CONDUCT.md).

## License

Aeromux is free software, released under the [GNU General Public License v3.0 or later](LICENSE.md).

## Acknowledgments

- **[aeromux-db](https://github.com/nandortoth/aeromux-db)** — Aircraft metadata database for registration, type, and operator enrichment
- **[readsb](https://github.com/wiedehopf/readsb)** — Reference implementation for Mode S demodulation techniques
- **[pyModeS](https://github.com/junzis/pyModeS)** — Comprehensive Mode S/ADS-B decoder and reference for decoding algorithms
- **[Mode S Made Easy](https://mode-s.org)** — Excellent technical documentation on Mode S and ADS-B protocols
- **[RtlSdrManager](https://github.com/nandortoth/RtlSdrManager)** — RTL-SDR device management library for .NET

## Contact

- **Author:** Nandor Toth
- **Email:** dev@nandortoth.com
- **Issues:** [github.com/nandortoth/aeromux/issues](https://github.com/nandortoth/aeromux/issues)
