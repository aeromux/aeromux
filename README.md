# Aeromux

[![License: GPL v3](https://img.shields.io/badge/License-GPLv3-blue.svg)](LICENSE.md)
[![.NET](https://img.shields.io/badge/.NET-10.0-512bd4)](https://dotnet.microsoft.com)
[![Platform](https://img.shields.io/badge/Platform-macOS%20%7C%20Linux-lightgrey)]()

**A multi-SDR Mode S and ADS-B demodulator and decoder for .NET**

Aeromux receives aircraft transponder signals on 1090 MHz using inexpensive RTL-SDR USB receivers, decodes Mode S and ADS-B messages, and serves the decoded aircraft data over the network. It supports multiple SDR devices simultaneously for improved coverage and runs on macOS and Linux, including Raspberry Pi.

## Features

- **Mode S and ADS-B Decoding** — Decodes aircraft identification, position, altitude, speed, heading, and more from transponder broadcasts. Covers all Mode S downlink formats, ADS-B Extended Squitter message types, and Comm-B data registers.

- **Multiple Receiver Support** — Use several RTL-SDR devices at once to improve reception coverage. Multi-device operation is a built-in feature — just list your devices in the configuration file. Frames from all devices are automatically combined and deduplicated.

- **Network Output** — In daemon mode, serves decoded data over TCP in three standard formats:
  - **Beast** — Binary protocol compatible with dump1090, readsb, and most ADS-B tools
  - **SBS/BaseStation** — Text protocol compatible with Virtual Radar Server
  - **JSON** — Streaming format for web applications and custom integrations

- **MLAT Support** — Accepts multilateration position data from mlat-client, enabling position tracking of aircraft that do not broadcast ADS-B.

- **Live Mode** — Interactive terminal interface showing tracked aircraft in real time, with keyboard controls for sorting and filtering.

- **Daemon Mode** — Runs as a background service for continuous, unattended operation with all data served over the network.

- **Cross-Platform** — Runs on macOS (Intel and Apple Silicon) and Linux (x64 and ARM64, including Raspberry Pi 4/5).

### Live Mode Preview

```
                                       AIRCRAFT LIST - Aeromux
┌────────┬──────────┬──────────┬────────────┬───────────┬──────────┬──────────┬────────┬───────────┬───┐
│ ICAO   │ Callsign │ Altitude │   Vertical │  Distance │    Speed │ Messages │ Signal │ Last seen │   │
├────────┼──────────┼──────────┼────────────┼───────────┼──────────┼──────────┼────────┼───────────┼───┤
│ 040047 │ ETH761   │ 37000 ft │     0 ft/m │  149.0 mi │  479 kts │      197 │  -25.2 │  0.4s ago │ █ │
│ 06A1BC │ QTR69E   │ 35000 ft │     0 ft/m │  130.7 mi │  489 kts │       75 │  -26.5 │  0.3s ago │ █ │
│ 3C55C3 │ EWG22HP  │ 37975 ft │     0 ft/m │  140.1 mi │  435 kts │      653 │  -23.5 │  0.1s ago │ █ │
│ 3C6742 │ DLH2HW   │ 37050 ft │  -128 ft/m │  129.4 mi │  475 kts │      352 │  -23.0 │  0.2s ago │ █ │
│ 406A9F │ BAW257   │ 39000 ft │   -64 ft/m │  116.4 mi │  529 kts │      438 │  -21.3 │  0.2s ago │ █ │
│ 4079CD │ VIR300   │ 37000 ft │     0 ft/m │   21.4 mi │  513 kts │      573 │   -2.8 │  0.0s ago │ █ │
│ 407FDA │ WUK7600  │ 35950 ft │   192 ft/m │  117.4 mi │  423 kts │      732 │  -14.7 │  0.2s ago │ █ │
│ 408142 │ DHK593   │ 36000 ft │     0 ft/m │   59.3 mi │  452 kts │      744 │   -4.8 │  0.0s ago │ █ │
│ 471D5C │ WZZ2302  │ 30150 ft │ -1088 ft/m │   37.4 mi │  451 kts │      566 │  -11.2 │  0.2s ago │ █ │
│ 4B8750 │ PGT77QG  │ 35000 ft │    64 ft/m │   74.6 mi │  447 kts │      633 │  -20.2 │  0.1s ago │ █ │
│ 4BC8D4 │ PGT96WD  │ 34000 ft │     0 ft/m │   28.8 mi │  421 kts │      555 │  -14.7 │  0.1s ago │ █ │
│ 4CADF2 │ RYR7GQ   │ 37000 ft │    64 ft/m │   86.5 mi │  410 kts │      448 │  -12.7 │  0.0s ago │ █ │
│ 4D24B1 │ WZZ3773  │ 34950 ft │  -512 ft/m │   13.9 mi │  415 kts │      735 │   -5.7 │  0.1s ago │ █ │
│ 5140D7 │ GEL1903  │ 41000 ft │     0 ft/m │   94.5 mi │  488 kts │      725 │  -10.3 │  0.2s ago │ ░ │
│ 71C009 │ KAL908   │ 33000 ft │     0 ft/m │   51.2 mi │  488 kts │      580 │  -22.1 │  0.1s ago │ ░ │
│ 8695A4 │ ANA212   │ 28975 ft │     0 ft/m │   62.8 mi │  472 kts │      729 │   -5.5 │  0.0s ago │ ░ │
└────────┴──────────┴──────────┴────────────┴───────────┴──────────┴──────────┴────────┴───────────┴───┘
  Aircraft: 54 | Viewport: 1-16                                          Dist: mi | Alt: ft | Spd: kts
  ↑/↓: Row, ←/→: Page                                            ENTER: Details, D/A/S: Units, Q: Quit
```

## Quick Start

### Prerequisites

- Microsoft [.NET 10.0 SDK](https://dotnet.microsoft.com/download) or later
- An RTL-SDR USB receiver (with R820T/R820T2 tuner)
- The `librtlsdr` native library:
  - **macOS:** `brew install librtlsdr`
  - **Debian/Ubuntu:** `sudo apt-get install librtlsdr-dev`

### Build and Run

```bash
# Clone the repository
git clone https://github.com/nandortoth/aeromux.git
cd aeromux

# Build a self-contained executable (auto-detects your platform)
./build.sh

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

Aeromux provides four commands:

```bash
# Daemon mode — runs in the background, serves data on network ports
aeromux daemon --config aeromux.yaml

# Live mode (standalone) — interactive terminal display reading directly from your SDR device(s)
aeromux live --standalone --config aeromux.yaml

# Live mode (connect) — interactive terminal display connecting to an existing Beast data source
aeromux live --connect host:port --config aeromux.yaml

# Database management — download, update, and verify the aircraft metadata database
aeromux database update --database artifacts/db/
aeromux database info --database artifacts/db/

# Version — shows version and runtime information
aeromux version
```

**Daemon mode** is for unattended operation: it decodes signals and makes the data available over the network for other tools to consume. **Live mode** adds a real-time terminal display showing all tracked aircraft. **Database** manages the aircraft metadata database downloaded from GitHub releases, with integrity verification.

## Configuration

Aeromux uses a YAML configuration file. Copy [`aeromux.example.yaml`](aeromux.example.yaml) as your starting point — it contains detailed comments explaining every option.

The main sections are:

- **`devices`** — Your RTL-SDR receivers. Configure gain, frequency correction (PPM), preamble sensitivity, and enable or disable individual devices.
- **`network`** — Which output protocols to enable (Beast, SBS, JSON) and their TCP ports. Also configures the HTTP port and bind address.
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
| 8080  | HTTP       | API and web interface (to be implemented)            |
| 30104 | MLAT Input | Receives positions from mlat-client                  |

All ports are configurable in the YAML configuration file. Protocols can be individually enabled or disabled.

## Supported Platforms

Aeromux builds as a self-contained, single-file executable for the following platforms:

| Platform             | Architecture | Runtime ID    |
|----------------------|--------------|---------------|
| macOS (Apple Silicon)| ARM64        | `osx-arm64`   |
| macOS (Intel)        | x64          | `osx-x64`     |
| Linux                | x64          | `linux-x64`   |
| Linux (Raspberry Pi) | ARM64        | `linux-arm64`  |

The build script auto-detects your platform, or you can cross-compile for a specific target:

```bash
./build.sh --target linux-arm64
```

## Contributing

Contributions are welcome! Whether it is a bug fix, a new feature, improved documentation, or additional tests, we appreciate your help.

Please read the [Contributing Guide](CONTRIBUTING.md) for development setup, architecture overview, coding standards, and the pull request process. This project follows the [Contributor Covenant Code of Conduct](CODE_OF_CONDUCT.md).

## License

Aeromux is free software, released under the [GNU General Public License v3.0 or later](LICENSE.md).

## Acknowledgments

- **[readsb](https://github.com/wiedehopf/readsb)** — Reference implementation for Mode S demodulation techniques
- **[pyModeS](https://github.com/junzis/pyModeS)** — Comprehensive Mode S/ADS-B decoder and reference for decoding algorithms
- **[Mode S Made Easy](https://mode-s.org)** — Excellent technical documentation on Mode S and ADS-B protocols
- **[RtlSdrManager](https://github.com/nandortoth/RtlSdrManager)** — RTL-SDR device management library for .NET

## Contact

- **Author:** Nandor Toth
- **Email:** dev@nandortoth.com
- **Issues:** [github.com/nandortoth/aeromux/issues](https://github.com/nandortoth/aeromux/issues)
