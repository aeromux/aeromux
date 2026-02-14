# Contributing to Aeromux

Thank you for your interest in contributing to Aeromux! This document provides guidelines and instructions for contributing to the project.

## Table of Contents

- [Code of Conduct](#code-of-conduct)
- [Getting Started](#getting-started)
- [Development Setup](#development-setup)
- [Architecture Overview](#architecture-overview)
- [How to Contribute](#how-to-contribute)
- [Coding Standards](#coding-standards)
- [Testing](#testing)
- [Pull Request Process](#pull-request-process)
- [Reporting Bugs](#reporting-bugs)
- [Suggesting Features](#suggesting-features)

## Code of Conduct

This project adheres to the [Contributor Covenant Code of Conduct](CODE_OF_CONDUCT.md). By participating, you are expected to uphold this code. Please report unacceptable behavior to the project maintainers.

## Getting Started

### Prerequisites

Before you begin, ensure you have the following installed:

- **.NET 10.0 SDK or later** - [Download here](https://dotnet.microsoft.com/download)
- **Git** - Version control system
- **librtlsdr** - Native RTL-SDR library for your platform:
  - **macOS:** `brew install librtlsdr`
  - **Linux (Debian/Ubuntu):** `sudo apt-get install librtlsdr-dev`
  - **Windows:** `choco install rtl-sdr` or download from [osmocom releases](https://github.com/osmocom/rtl-sdr/releases)
- **IDE** (recommended):
  - JetBrains Rider
  - Visual Studio Code with the C# Dev Kit extension

### Fork and Clone

1. Fork the repository on GitHub.
2. Clone your fork locally:
   ```bash
   git clone https://github.com/YOUR-USERNAME/aeromux.git
   cd aeromux
   ```
3. Add the upstream repository:
   ```bash
   git remote add upstream https://github.com/nandortoth/aeromux.git
   ```
4. Keep your fork up to date:
   ```bash
   git fetch upstream
   git checkout main
   git merge upstream/main
   ```

## Development Setup

### Building the Project

```bash
# Restore dependencies and build
dotnet build

# Or use the convenience script (auto-detects platform and architecture)
./build.sh

# Cross-compile for a specific target
./build.sh --target linux-arm64

# Silent mode (for automation)
./build.sh --silent
```

The build script produces a self-contained single-file executable in `artifacts/binaries/{runtime_id}/aeromux`. Supported runtime identifiers: `osx-x64`, `osx-arm64`, `linux-x64`, `linux-arm64`.

### Running Aeromux

```bash
# Interactive mode with menu (builds automatically)
./run.sh

# Skip rebuild
./run.sh --no-build

# Or run directly via dotnet
dotnet run --project src/Aeromux.CLI -- daemon --config aeromux.example.yaml
dotnet run --project src/Aeromux.CLI -- live --standalone --config aeromux.example.yaml
dotnet run --project src/Aeromux.CLI -- version
```

### Verify Code Style

The project enforces code style through `.editorconfig`. Most IDEs apply these rules automatically.

```bash
# Format code according to .editorconfig
dotnet format

# Check formatting without making changes
dotnet format --verify-no-changes
```

## Architecture Overview

Aeromux follows a clean architecture with three layers:

```
src/
├── Aeromux.Core/              # Domain logic (no external dependencies)
│   ├── Configuration/         # Application configuration models
│   ├── ModeS/                 # Mode S protocol implementation
│   │   ├── Enums/             # Protocol enumerations
│   │   ├── Messages/          # Message types
│   │   └── ValueObjects/      # Domain value objects
│   ├── Services/              # Core business logic
│   ├── SignalProcessing/      # IQ demodulation, preamble detection
│   ├── Timing/                # High-precision timing
│   └── Tracking/              # Aircraft state tracking
├── Aeromux.Infrastructure/    # I/O, networking, SDR device management
│   ├── Configuration/         # Config file loading and validation
│   ├── Network/               # TCP servers and protocol output
│   │   └── Protocols/         # Beast, SBS, JSON implementations
│   ├── Sdr/                   # SDR device coordination
│   ├── Streaming/             # Data streaming
│   ├── Aggregation/           # Multi-device data aggregation
│   └── Mlat/                  # MLAT input support
└── Aeromux.CLI/               # CLI executable and user interface
    ├── Commands/              # daemon, live, version
    └── Configuration/         # CLI config validation
```

**Key dependency rule:** Core has no dependency on Infrastructure or CLI. Infrastructure depends on Core. CLI depends on both.

When contributing, place your code in the appropriate layer:

- **Protocol parsing, domain models, signal processing** go in `Aeromux.Core`.
- **Device I/O, file access, networking, serialization** go in `Aeromux.Infrastructure`.
- **CLI commands, display, user interaction** go in `Aeromux.CLI`.

## How to Contribute

### Types of Contributions

We welcome various types of contributions:

- **Bug fixes** - Fix issues and improve stability
- **New features** - Add new functionality (protocol support, output formats, etc.)
- **Documentation** - Improve or add documentation
- **Tests** - Add or improve test coverage
- **Code quality** - Refactoring and improvements
- **Tooling** - Improve build scripts, CI/CD, packaging

### Contribution Workflow

1. **Check existing issues** - Look for existing issues or create a new one.
2. **Discuss major changes** - For significant changes, open an issue first to discuss the approach.
3. **Create a branch** - Use a descriptive branch name (see below).
4. **Make your changes** - Follow the coding standards.
5. **Write tests** - Add tests for new functionality.
6. **Ensure the build is clean** - No warnings from `dotnet build`.
7. **Run tests** - All tests must pass.
8. **Update documentation** - Update relevant docs and XML comments.
9. **Commit your changes** - Use clear commit messages.
10. **Open a Pull Request** - Submit your PR with a clear description.

### Branch Naming Convention

Use descriptive branch names following this pattern:

```
feature/description       # New features
bugfix/description        # Bug fixes
docs/description          # Documentation updates
refactor/description      # Code refactoring
test/description          # Test additions/improvements
```

Examples:

```bash
git checkout -b feature/add-websocket-output
git checkout -b bugfix/fix-cpr-decoding-edge-case
git checkout -b docs/add-architecture-guide
```

## Coding Standards

### Code Style

This project enforces code style through `.editorconfig`. The key rules are summarized below; your IDE should handle most of these automatically.

#### Formatting

- **Indentation:** 4 spaces for C#, 2 spaces for YAML/JSON/XML/shell scripts
- **Line endings:** LF (Unix-style) for all source files
- **Braces:** Allman style (opening brace on its own line)
- **Namespaces:** File-scoped (required)

```csharp
// Correct - file-scoped namespace
namespace Aeromux.Core.ModeS;

public class MessageParser
{
    public void Parse()
    {
        // ...
    }
}
```

#### Naming Conventions

| Element | Convention | Example |
|---|---|---|
| Classes, Methods, Properties | `PascalCase` | `MessageParser`, `DecodeAltitude()` |
| Private fields | `_camelCase` | `_deviceCount`, `_icaoAddress` |
| Parameters, Local variables | `camelCase` | `rawMessage`, `signalLevel` |
| Constants | `PascalCase` | `MaxDevices`, `PreambleLength` |
| Static readonly fields | `PascalCase` | `DefaultFrequency` |
| Interfaces | `IPascalCase` | `IDeviceManager`, `IMessageDecoder` |

#### `var` Usage

- **Do not use** `var` for built-in types (`int`, `string`, `bool`, etc.)
- **Use** `var` when the type is apparent from the right-hand side (`new`, cast)
- **Do not use** `var` when the type is unclear (method return values)

```csharp
// Correct
int count = 5;
string icao = "A1B2C3";
var parser = new MessageParser();
var frequency = new Frequency(1090000000);

// Incorrect
var count = 5;
var result = GetSomething();
```

#### Nullable Reference Types

Nullable reference types are enabled project-wide. Respect nullability annotations and avoid suppressing nullable warnings (`!`) without justification.

### XML Documentation

All public APIs must have XML documentation comments:

```csharp
/// <summary>
/// Decodes the altitude from a Mode S message.
/// </summary>
/// <param name="rawMessage">The raw Mode S message bytes.</param>
/// <returns>The decoded altitude in feet, or null if the altitude is unavailable.</returns>
/// <exception cref="ArgumentException">Thrown when the message format is invalid.</exception>
public int? DecodeAltitude(byte[] rawMessage)
{
    // ...
}
```

### Exception Handling

Use specific exception types:

```csharp
// Correct
if (friendlyName is null)
    throw new ArgumentNullException(nameof(friendlyName));

if (string.IsNullOrWhiteSpace(friendlyName))
    throw new ArgumentException("Friendly name cannot be empty.", nameof(friendlyName));

// Incorrect - too generic
throw new Exception("Something went wrong");
```

### Async/Await

```csharp
// Correct - accept CancellationToken, use Async suffix
public async Task<IQData> ReadDataAsync(CancellationToken cancellationToken = default)
{
    await Task.Delay(100, cancellationToken);
    return new IQData();
}

// Incorrect - async void (only acceptable for event handlers)
public async void ProcessData() { }
```

### Dispose Pattern

For classes managing unmanaged resources (SDR devices, TCP connections, etc.), implement `IDisposable` correctly:

```csharp
public class DeviceConnection : IDisposable
{
    private bool _disposed;

    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }

    private void Dispose(bool disposing)
    {
        if (_disposed)
            return;

        if (disposing)
        {
            // Dispose managed resources
        }

        // Release unmanaged resources
        _disposed = true;
    }

    ~DeviceConnection()
    {
        Dispose(disposing: false);
    }
}
```

## Testing

### Test Framework

The project uses **xUnit** with **FluentAssertions** and **Moq**:

```bash
# Run all tests
dotnet test

# Run tests with detailed output
dotnet test --verbosity detailed

# Run tests with coverage
dotnet test --collect:"XPlat Code Coverage"

# Run a specific test project
dotnet test tests/Aeromux.Core.Tests
dotnet test tests/Aeromux.Infrastructure.Tests
```

### Test Organization

Tests mirror the source structure:

```
tests/
├── Aeromux.Core.Tests/              # Unit tests for domain logic
└── Aeromux.Infrastructure.Tests/    # Tests for I/O and networking
```

### Writing Tests

- Place tests in the corresponding test project for the layer you are testing.
- Use descriptive test method names that describe the scenario and expected behavior.
- Use FluentAssertions for readable assertions.
- Use Moq for mocking dependencies at layer boundaries.

```csharp
public class FrequencyTests
{
    [Fact]
    public void Constructor_WithValidMhz_SetsCorrectHzValue()
    {
        // Arrange & Act
        var frequency = Frequency.FromMhz(1090);

        // Assert
        frequency.Hz.Should().Be(1_090_000_000);
    }
}
```

## Pull Request Process

### Before Submitting

Ensure your PR meets these requirements:

- [ ] Code follows the project's style guidelines (enforced by `.editorconfig`)
- [ ] Code builds without warnings: `dotnet build`
- [ ] Code formatting is correct: `dotnet format --verify-no-changes`
- [ ] All existing tests pass: `dotnet test`
- [ ] New code has appropriate test coverage
- [ ] Public APIs have XML documentation comments
- [ ] CHANGELOG.md is updated with your changes
- [ ] Commit messages are clear and descriptive

### PR Title Format

Use a clear, descriptive title following conventional commits:

```
feat: Add WebSocket output protocol
fix: Correct CPR position decoding near anti-meridian
docs: Add architecture decision record for logging
refactor: Simplify preamble detection logic
test: Add unit tests for Gillham altitude decoding
```

### PR Description

Include in your PR description:

- **What** the PR does and **why**.
- How you **tested** the changes.
- Any **breaking changes** or **migration steps** required.

### Review Process

1. A maintainer will review your PR.
2. Feedback may be provided - please address review comments.
3. Once approved, a maintainer will merge your PR.
4. Your contribution will be included in the next release.

## Reporting Bugs

### Before Reporting

- Check if the bug has already been reported in [Issues](https://github.com/nandortoth/aeromux/issues).
- Ensure you are using the latest version.
- Verify the issue is reproducible.

### Bug Report Contents

When reporting bugs, include:

- **Description** - Clear description of the bug.
- **Steps to reproduce** - Minimal steps to trigger the issue.
- **Expected vs. actual behavior** - What you expected and what happened.
- **Environment** - OS, .NET version, Aeromux version, RTL-SDR device model, librtlsdr version.
- **Logs** - Relevant log output (Aeromux logs to `logs/` by default).
- **Configuration** - The YAML configuration used (with sensitive data redacted).

## Suggesting Features

When suggesting features:

1. **Check existing issues** - See if it has already been suggested.
2. **Describe the use case** - Why is this feature needed? What problem does it solve?
3. **Provide examples** - How would the API or CLI usage look?
4. **Consider alternatives** - Are there other approaches?

Feature ideas that align well with the project roadmap (multi-device support, aircraft tracking, output protocols, web interface) are especially welcome.

## License

By contributing to Aeromux, you agree that your contributions will be licensed under the [GNU General Public License v3.0 or later](LICENSE.md).

---

Thank you for contributing to Aeromux! Your efforts help make this project better for the aviation tracking community.
