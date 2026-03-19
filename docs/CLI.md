# CLI Reference

Aeromux is operated entirely through a command-line interface that provides five commands: `daemon` for unattended background operation, `live` for an interactive terminal display, `database` for managing the aircraft metadata database, `device` for discovering RTL-SDR hardware, and `version` for displaying build and runtime information. All commands share a common set of global options and follow the same configuration priority model, where CLI parameters take precedence over YAML settings, which in turn take precedence over built-in defaults.

## Global Options

The following options are available on all commands. They control how Aeromux locates its configuration file, sets its logging verbosity, and finds the aircraft metadata database:

| Option                | Description                                                                                                     | Default                     |
|-----------------------|-----------------------------------------------------------------------------------------------------------------|-----------------------------|
| `--config <path>`     | Path to the YAML configuration file                                                                             | `aeromux.yaml`              |
| `--log-level <level>` | Logging verbosity: `Verbose`, `Debug`, `Information`, `Warning`, `Error`, or `Fatal`                            | From YAML, or `Information` |
| `--database <path>`   | Path to the aircraft metadata database directory. Specifying this option implicitly enables database enrichment | From YAML                   |

## Configuration Priority

Every individual setting follows the same resolution order: **CLI > YAML > Default**. This priority is applied per-setting rather than globally, which means you can rely on the YAML file for most of your configuration while selectively overriding specific values on the command line. For example, you can use SDR sources defined in the YAML file while overriding the Beast output port via a CLI parameter:

| Priority    | Source                  | Example                          |
|-------------|-------------------------|----------------------------------|
| 1 (highest) | CLI parameter           | `--beast-output-port 31005`      |
| 2           | YAML configuration file | `network.beastOutputPort: 30005` |
| 3 (lowest)  | Built-in default        | `30005`                          |

## Input Sources

Both the `daemon` and `live` commands support the same unified input model. Aeromux can receive data from local RTL-SDR devices, from remote Beast TCP sources, or from both simultaneously. Frames from all active sources are automatically aggregated and deduplicated by the tracker, regardless of whether they originate from an SDR device or a network source.

### Source Resolution

The way input sources are resolved depends on which CLI flags are present and what the YAML configuration file contains. The following table describes how Aeromux determines which sources to activate:

| Scenario                                | Behavior                                                                                                                                       |
|-----------------------------------------|------------------------------------------------------------------------------------------------------------------------------------------------|
| No CLI flags, YAML has `sdrSources`     | SDR sources from the YAML file are used                                                                                                        |
| No CLI flags, YAML has `beastSources`   | Beast sources from the YAML file are used                                                                                                      |
| No CLI flags, YAML has both             | Both SDR and Beast sources from the YAML file are used                                                                                         |
| `--beast-source host:port`              | The Beast source from the CLI is used, overriding any `beastSources` in YAML. SDR sources are not used unless `--sdr-source` is also specified |
| `--sdr-source`                          | Explicitly enables SDR sources from the YAML `sdrSources` section                                                                              |
| `--sdr-source --beast-source host:port` | Both SDR sources (from YAML) and the Beast source (from CLI) are used together                                                                 |

### Beast Connection String Format

Beast sources are specified as `HOST:PORT` pairs. If the port is omitted, it defaults to 30005:

```
192.168.1.100:30005    # Full format with explicit port
192.168.1.100          # Defaults to port 30005
localhost              # Defaults to port 30005
```

Multiple Beast sources can be specified by repeating the `--beast-source` option on the command line:

```bash
aeromux daemon --beast-source 192.168.1.100:30005 --beast-source 192.168.1.101:30005
```

Beast sources can also be configured in the YAML file under the `beastSources` section. CLI Beast sources override YAML Beast sources when both are specified:

```yaml
beastSources:
  - host: "192.168.1.100"
    port: 30005
  - host: "192.168.1.101"
    port: 30005
```

## Commands

### `daemon`

The daemon command runs Aeromux as a background service for continuous, unattended operation. It decodes signals from the configured input sources and makes the decoded aircraft data available over the network via TCP protocols (Beast, SBS, JSON) and a REST API. This is the primary operating mode for production deployments and headless systems such as Raspberry Pi.

#### Input Options

| Option                       | Description                                                                         | Default                                      |
|------------------------------|-------------------------------------------------------------------------------------|----------------------------------------------|
| `--sdr-source`               | Enable RTL-SDR device input using the `sdrSources` defined in the YAML file         | Implied when no Beast sources are configured |
| `--beast-source <HOST:PORT>` | Connect to a Beast TCP source. Can be specified multiple times for multiple sources | From YAML, or none                           |

#### Network Output Options

The daemon serves decoded data over TCP in up to three standard formats, each independently configurable. It also exposes a read-only REST API for web interfaces and third-party integrations. See the [Broadcast Guide](BROADCAST.md) for protocol details and the [API Guide](API.md) for the REST API documentation.

| Option                          | Description                                       | Default   |
|---------------------------------|---------------------------------------------------|-----------|
| `--beast-output-port <port>`    | TCP port for Beast binary protocol output         | `30005`   |
| `--beast-output-enabled <bool>` | Enable or disable Beast output                    | `true`    |
| `--sbs-output-port <port>`      | TCP port for SBS/BaseStation text protocol output | `30003`   |
| `--sbs-output-enabled <bool>`   | Enable or disable SBS output                      | `false`   |
| `--json-output-port <port>`     | TCP port for streaming JSON output                | `30006`   |
| `--json-output-enabled <bool>`  | Enable or disable JSON output                     | `false`   |
| `--api-port <port>`             | TCP port for the REST API                         | `8080`    |
| `--api-enabled <bool>`          | Enable or disable the REST API                    | `true`    |
| `--bind-address <ip>`           | Network interface to bind all listeners to        | `0.0.0.0` |

#### MLAT Options

When MLAT is enabled, Aeromux can receive multilateration position data from mlat-client for aircraft that do not broadcast ADS-B positions. The receiver UUID is required for MLAT triangulation and must be a valid RFC 4122 UUID.

| Option                     | Description                                   | Default            |
|----------------------------|-----------------------------------------------|--------------------|
| `--mlat-enabled <bool>`    | Enable or disable MLAT input from mlat-client | `true`             |
| `--mlat-input-port <port>` | TCP port for receiving MLAT Beast data        | `30104`            |
| `--receiver-uuid <uuid>`   | Receiver UUID for MLAT triangulation          | From YAML, or none |

#### Usage Examples

```bash
# SDR-only — uses sdrSources from the YAML configuration
aeromux daemon --config aeromux.yaml

# Beast-only — connects to an external Beast source over the network
aeromux daemon --beast-source 192.168.1.100:30005 --config aeromux.yaml

# Combined SDR + Beast — aggregates frames from both source types
aeromux daemon --sdr-source --beast-source 192.168.1.100:30005 --config aeromux.yaml

# Multiple Beast sources with custom output ports and SBS enabled
aeromux daemon \
  --beast-source 192.168.1.100:30005 \
  --beast-source 192.168.1.101:30005 \
  --beast-output-port 31005 \
  --sbs-output-enabled true \
  --config aeromux.yaml

# Beast-only with MLAT disabled
aeromux daemon --beast-source host:30005 --mlat-enabled false --config aeromux.yaml
```

### `live`

The live command launches an interactive terminal interface that displays tracked aircraft in real time. It provides a sortable, searchable aircraft list and a detail view showing comprehensive information for a single selected aircraft. The live command supports the same input sources as the daemon command — SDR devices, Beast TCP sources, or both simultaneously.

For the full keyboard reference, sorting, search, and detail view documentation, see the [TUI Guide](TUI.md).

#### Input Options

| Option                       | Description                                                                         | Default                                      |
|------------------------------|-------------------------------------------------------------------------------------|----------------------------------------------|
| `--sdr-source`               | Enable RTL-SDR device input using the `sdrSources` defined in the YAML file         | Implied when no Beast sources are configured |
| `--beast-source <HOST:PORT>` | Connect to a Beast TCP source. Can be specified multiple times for multiple sources | From YAML, or none                           |

#### Usage Examples

```bash
# SDR-only — reads directly from your RTL-SDR device(s) defined in the config
aeromux live --config aeromux.yaml

# Beast-only — connects to an existing Beast data source over the network
aeromux live --beast-source 192.168.1.100:30005 --config aeromux.yaml

# Combined SDR + Beast — SDR devices and Beast sources together
aeromux live --sdr-source --beast-source 192.168.1.100:30005 --config aeromux.yaml

# Multiple Beast sources
aeromux live --beast-source 192.168.1.100:30005 --beast-source 192.168.1.101:30005 --config aeromux.yaml
```

### `database`

The database command manages the aircraft metadata database that Aeromux uses to enrich tracked aircraft with registration, operator, manufacturer, and type information. The database is downloaded from the [aeromux-db](https://github.com/nandortoth/aeromux-db) GitHub releases and stored locally. Integrity verification is performed automatically during download and update operations.

#### Subcommands

| Subcommand  | Description                                                                                                                      |
|-------------|----------------------------------------------------------------------------------------------------------------------------------|
| `update`    | Download the latest version of the aircraft metadata database, or update an existing copy if a newer version is available        |
| `info`      | Display the currently installed database version and statistics, including the number of records and the date of the last update |

#### Usage Examples

```bash
# Download or update the database to the specified directory
aeromux database update --database artifacts/db/

# Display information about the currently installed database
aeromux database info --database artifacts/db/
```

### `device`

The device command lists all RTL-SDR USB receivers detected on the system. This is useful for identifying device indices and confirming that your hardware is recognized before configuring the `sdrSources` section of the YAML file. With the `--verbose` flag, it also displays detailed tuner parameters including supported gains and frequency ranges, which can help when fine-tuning the gain settings in your configuration.

#### Options

| Option      | Description                                                                                           |
|-------------|-------------------------------------------------------------------------------------------------------|
| `--verbose` | Show detailed tuner parameters including supported gains and frequency range for each detected device |

#### Usage Examples

```bash
# List all detected RTL-SDR devices with basic information
aeromux device

# List devices with detailed tuner parameters
aeromux device --verbose
```

### `version`

The version command displays the Aeromux version number and runtime information, including the .NET runtime version and the operating system platform. This information is useful for bug reports and verifying that the correct build is running.

#### Options

| Option      | Description                                                                                                  |
|-------------|--------------------------------------------------------------------------------------------------------------|
| `--verbose` | Display verbose version information including commit hash, .NET runtime version, license, and repository URL |

#### Usage Examples

```bash
# Display the version number
aeromux version

# Display verbose version information
aeromux version --verbose
```

## YAML Configuration Reference

The YAML configuration file is organized into the following top-level sections. Each section controls a specific aspect of Aeromux's behavior. See [`aeromux.example.yaml`](../aeromux.example.yaml) for a fully commented template with detailed explanations of every option.

| Section        | Description                                                                                                  |
|----------------|--------------------------------------------------------------------------------------------------------------|
| `sdrSources`   | RTL-SDR device configurations, including friendly name, device index, gain, and PPM frequency correction     |
| `beastSources` | Beast TCP input sources, each specified as a host and port pair                                              |
| `network`      | Output protocol ports and enable/disable flags for Beast, SBS, JSON, and the REST API, plus the bind address |
| `tracking`     | Confidence level for ICAO filtering, aircraft and ICAO timeout durations, and history buffer settings        |
| `receiver`     | Station geographic location (latitude, longitude, altitude) and receiver UUID for MLAT triangulation         |
| `mlat`         | MLAT input enable/disable flag and the port for receiving positions from mlat-client                         |
| `database`     | Aircraft metadata database directory path and enable/disable flag                                            |
| `logging`      | Log level, console and file output settings, log rotation, and retention                                     |
