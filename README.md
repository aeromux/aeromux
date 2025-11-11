# Aeromux - Multi-SDR Mode S and ADS-B Demodulator and Decoder for .NET

**A high-performance, multi-device ADS-B/Mode S receiver written in C# for .NET**

Aeromux receives and decodes aircraft transponder signals from RTL-SDR devices, providing real-time aircraft tracking data via industry-standard protocols (Beast, SBS, JSON).

## Status

**Current Phase:** Phase 5 ✅ COMPLETE (Message Parsing)
**Next Phase:** Phase 6 (Multi-Device Aggregation and TCP Broadcasting)

### Phase 5 Completion Summary

✅ **100% Mode S Protocol Coverage**
- All 24 Downlink Formats (DF 0-24) implemented
- All Type Codes (TC 1-31) for ADS-B Extended Squitter
- All 10 Comm-B BDS registers (1,0 through 6,0)
- 15 message types with 21 strongly-typed enums
- MessageParser split across 6 partial classes for maintainability

**Coverage Breakdown:**
- ~70% Priority 1: Essential ADS-B (TC 1-4, 9-18, 19)
- ~16% Priority 2: Common Surveillance (DF 0, 4, 5, 11)
- ~9% Priority 3: Advanced ADS-B (TC 5-8, 20-22, 28, 29, 31)
- ~5% Priority 4: Comm-B and ACAS (DF 16, 20, 21, all BDS registers)

## Features

### Current (Phases 0-5)
- ✅ Multi-device support (multiple RTL-SDR dongles simultaneously)
- ✅ Industry-standard 2.4 MSPS sampling (aligned with readsb)
- ✅ High-performance IQ demodulation with lookup tables
- ✅ Advanced preamble detection with local noise estimation
- ✅ CRC-24 validation with single-bit error correction
- ✅ ICAO confidence tracking (filters noise from real aircraft)
- ✅ Complete Mode S message parsing (all DFs, TCs, BDS registers)
- ✅ CPR position decoding (airborne and surface vehicles)
- ✅ Gillham altitude decoding for legacy transponders
- ✅ BDS register inference for Comm-B messages
- ✅ Structured logging with Serilog
- ✅ YAML-based configuration

### Upcoming (Phase 6+)
- 🔄 Frame aggregation across multiple devices
- 🔄 TCP broadcasting (Beast, SBS, JSON formats)
- 🔄 HTTP REST API for statistics and aircraft data
- 🔄 WebSocket real-time updates
- 🔄 Web-based aircraft map visualization

## Quick Start

### Prerequisites
- .NET 9.0 SDK
- RTL-SDR device(s) with R820T/R820T2 tuner
- librtlsdr installed

### Installation

```bash
# Clone repository
git clone https://github.com/nandortoth/aeromux.git
cd aeromux

# Build single executable
./build.sh

# Run (uses defaults if aeromux.yaml doesn't exist)
./aeromux daemon
```

### Configuration

Create `aeromux.yaml`:

```yaml
devices:
  - name: primary
    deviceIndex: 0
    centerFrequency: 1090      # MHz (ADS-B frequency)
    sampleRate: 2.4            # MHz (industry standard)
    tunerGain: 40.0            # dB
    gainMode: manual
    enabled: true

network:
  beastPort: 30002
  httpPort: 8080
  bindAddress: "0.0.0.0"

tracking:
  confidenceLevel: medium      # low (5) | medium (10) | high (15) detections

# Optional: For surface vehicle position decoding (TC 5-8)
receiver:
  latitude: 46.907982          # Your receiver latitude
  longitude: 19.693172         # Your receiver longitude
  altitude: 120                # Meters above sea level
  name: "Kecskemét"

logging:
  level: information           # verbose | debug | information | warning | error
```

## Architecture

Aeromux follows clean architecture principles with clear separation of concerns:

```
Aeromux.CLI (executable)
   ↓
   ├─> Aeromux.Infrastructure (TCP, HTTP, Device I/O)
   │      ↓
   │      └─> Aeromux.Core (Domain logic, no dependencies)
   │
   └─> RtlSdrManager (SDR device driver)
```

### Key Design Decisions
- **Single executable** - No separate processes to manage
- **Direct RtlSdrManager integration** - No abstraction layers
- **YAML configuration** - Simple, readable, version-controllable
- **Structured logging** - Serilog for production-grade observability
- **Coordinator Pattern** - Zero overhead statistics in hot paths

See [Architecture Documentation](docs/dev/design/ARCHITECTURE.md) for details.

## Documentation

- **[Development Documentation](docs/dev/design/README.md)** - Architecture, design decisions, implementation guides
- **[Implementation Status](docs/dev/implementation/README.md)** - Phase-by-phase progress tracking
- **[Architecture Overview](docs/dev/design/ARCHITECTURE.md)** - System design and structure
- **[Configuration Guide](docs/dev/design/CONFIGURATION.md)** - Detailed configuration reference
- **[Data Flow](docs/dev/design/DATA_FLOW.md)** - How data flows through the system

### Architecture Decision Records (ADRs)
- [ADR-001: Single Executable](docs/dev/design/decisions/001-single-executable.md)
- [ADR-002: HTTP for Stats](docs/dev/design/decisions/002-no-ipc-http-stats.md)
- [ADR-003: YAML Configuration](docs/dev/design/decisions/003-yaml-configuration.md)
- [ADR-004: No Environment Variables](docs/dev/design/decisions/004-no-environment-variables.md)
- [ADR-005: Direct RtlSdrManager](docs/dev/design/decisions/005-direct-rtlsdrmanager.md)
- [ADR-006: Spectre.Console.Cli](docs/dev/design/decisions/006-spectre-console-cli.md)
- [ADR-007: Structured Logging (Serilog)](docs/dev/design/decisions/007-structured-logging.md)
- [ADR-008: Use RtlSdrManager Types Directly](docs/dev/design/decisions/008-use-rtlsdrmanager-types.md)
- [ADR-009: Statistics Logging Pattern](docs/dev/design/decisions/009-statistics-logging-pattern.md)

## Technology Stack

- **.NET 9.0** - Modern, high-performance runtime
- **RtlSdrManager** - RTL-SDR device management library
- **Spectre.Console.Cli** - CLI framework with rich terminal output
- **YamlDotNet** - YAML configuration parsing
- **Serilog** - Structured logging framework

## Performance

- CPU: ~20-30% single core at 2.4 MSPS (with lookup tables)
- Memory: ~100-200 MB typical
- Latency: <25ms from RF reception to TCP output
- Throughput: 50-500 messages/second (depends on local traffic)

## Contributing

Contributions are welcome! See [CONTRIBUTING.md](CONTRIBUTING.md) for guidelines.

### Development Setup

1. Install .NET 9.0 SDK
2. Clone repository with RtlSdrManager submodule:
   ```bash
   git clone --recursive https://github.com/nandortoth/aeromux.git
   ```
3. Build: `dotnet build`
4. Run: `dotnet run --project src/Aeromux.CLI`

## License

GNU General Public License v3.0 - See [LICENSE.md](LICENSE.md) for details.

## Acknowledgments

- **readsb** - Reference implementation for Mode S demodulation techniques
- **pyModeS** - Comprehensive Mode S/ADS-B decoder and reference for algorithms
- **Mode S Made Easy** (mode-s.org) - Excellent technical documentation
- **RtlSdrManager** - Clean RTL-SDR device abstraction for .NET

## Contact

- **Author:** Nandor Toth
- **Email:** dev@nandortoth.com
- **Issues:** https://github.com/nandortoth/aeromux/issues

---

**Note:** Aeromux is currently in active development. Phase 5 (Message Parsing) is complete with 100% Mode S protocol coverage. Phase 6 (TCP Broadcasting) is next.
