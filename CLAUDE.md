# Aeromux

Multi-SDR Mode S and ADS-B Demodulator and Decoder for .NET.
License: GPLv3, Copyright 2025-2026 Nandor Toth.

## Build and Test

```bash
dotnet build                                   # Build entire solution
dotnet test                                    # Run all tests
dotnet test tests/Aeromux.Core.Tests           # Core tests only
dotnet test tests/Aeromux.Infrastructure.Tests # Infrastructure tests only
dotnet test tests/Aeromux.CLI.Tests            # CLI tests only
dotnet run --project src/Aeromux.CLI           # Run the application
```

### Release Build

```bash
./build.sh                          # Auto-detect platform, self-contained single-file binary
./build.sh --target linux-arm64     # Cross-compile for Raspberry Pi
./build.sh --target all             # Build all supported platforms
./build.sh --with-database          # Build and download aeromux-db
./build.sh --silent                 # Suppress output
```

Output goes to `artifacts/binaries/{runtime-id}/aeromux`. Supported targets: `linux-x64`, `linux-arm64`, `macos-x64`, `macos-arm64`.

`run.sh` is an interactive quick-start helper for building and launching during development.

## Tech Stack

- **.NET 10**, C# with nullable reference types and implicit usings
- **Spectre.Console** — TUI rendering (Live display, tables)
- **Serilog** — structured logging (file, console, async sinks)
- **RtlSdrManager** — RTL-SDR device access via P/Invoke
- **Microsoft.Data.Sqlite** — local database
- **YamlDotNet** — configuration file parsing
- **ASP.NET Core** — streaming/web infrastructure
- **xUnit** + **FluentAssertions** + **Moq** — testing
- **Coverlet** — code coverage

## Project Structure

```
src/
  Aeromux.CLI/            CLI application (Spectre.Console TUI, commands)
  Aeromux.Core/           Core domain (Mode S decoding, tracking, value objects)
  Aeromux.Infrastructure/ Infrastructure (database, streaming, config)
tests/
  Aeromux.Core.Tests/
  Aeromux.Infrastructure.Tests/
  Aeromux.CLI.Tests/
docs/                     Requirements and documentation
packaging/                Platform packaging scripts (deb, pkg, etc.)
docker/                   Docker build and compose files
```

Dependency direction: CLI -> Infrastructure -> Core. CLI also references Core directly.

## Git Rules

**Do not perform any git commands.** The user handles all git operations (commit, branch, push, stash, etc.) manually. Only read commands (`git diff`, `git status`, `git log`) are allowed when explicitly needed by a command or skill.

## Workflow

Always present a plan before modifying code, even for small changes. Get user approval before editing files.

## Domain Terminology

| Term | Meaning |
|------|---------|
| DF | Downlink Format — Mode S message type (0-24) |
| TC | Type Code — Extended Squitter subtype (1-31) within DF 17/18 |
| BDS | Binary Data Selector — Comm-B register identifier (e.g., BDS 6,0) |
| CPR | Compact Position Reporting — position encoding using even/odd frames |
| ICAO | 24-bit aircraft address assigned by registration country |
| NIC | Navigation Integrity Category |
| NACp | Navigation Accuracy Category - Position |
| SIL | Surveillance Integrity Level |
| MLAT | Multilateration — position via time-difference-of-arrival |
| SDR | Software Defined Radio |
| TUI | Terminal User Interface (Spectre.Console Live display) |
