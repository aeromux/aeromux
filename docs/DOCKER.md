# Docker Distribution

Aeromux is distributed as a multi-arch Docker image for containerized deployments, targeting Raspberry Pi 4/5 (ARM64) as the primary platform and x86-64 as a secondary platform. The image runs aeromux as a headless daemon with RTL-SDR USB device passthrough support.

Images are published to GitHub Container Registry (GHCR) at `ghcr.io/aeromux/aeromux` and saved as `.tar` files for offline distribution.

## Image Metadata

| Field           | Value                                                 |
|-----------------|-------------------------------------------------------|
| Image Name      | `ghcr.io/aeromux/aeromux`                          |
| Base Image      | `debian:bookworm-slim`                                |
| Version         | From `src/Directory.Build.props` (e.g., `0.6.1`)      |
| License         | GPL-3.0-or-later                                      |
| Maintainer      | `Nandor Toth <dev@nandortoth.com>`                    |
| Homepage        | `https://github.com/aeromux/aeromux`               |

### Runtime Dependencies

The following packages are installed in the image via `apt-get`:

| Dependency        | Reason                                              |
|-------------------|-----------------------------------------------------|
| `librtlsdr0`      | RTL-SDR shared library (required by RtlSdrManager)  |
| `libicu72`        | ICU for .NET globalization                          |
| `ca-certificates` | HTTPS support (database downloads, etc.)            |
| `gosu`            | Lightweight privilege drop (root to aeromux user)   |

### Image Tags

| Tag       | Description                                             |
|-----------|---------------------------------------------------------|
| `0.6.1`   | Version-specific, multi-arch manifest (arm64 + amd64)   |
| `latest`  | Points to the most recent version                       |

Multi-arch manifests are used so that a single tag (e.g., `0.6.1`) automatically resolves to the correct architecture when pulled.

## Filesystem Layout

| Path                               | Type        | Description                          |
|------------------------------------|-------------|--------------------------------------|
| `/usr/bin/aeromux`                 | Binary      | Self-contained executable            |
| `/etc/aeromux/aeromux.yaml`        | Config      | Default configuration                |
| `/var/lib/aeromux/`                | Volume      | Database storage (persistent)        |
| `/var/log/aeromux/`                | Volume      | Log files (persistent)               |
| `/docker-entrypoint.sh`            | Script      | Entrypoint script                    |

The container starts as root for volume permission setup, then drops to a dedicated non-root user (`aeromux`) via `gosu` for security.

## Configuration

The image ships a default `aeromux.yaml` at `/etc/aeromux/aeromux.yaml` with Docker-appropriate paths. The default configuration is generated at image build time from `aeromux.example.yaml` with the following transformations:

| Setting                    | Example Config Value           | Docker Config Value                     |
|----------------------------|--------------------------------|-----------------------------------------|
| `logging.level`            | `debug`                        | `information`                           |
| `logging.console.enabled`  | `false`                        | `true`                                  |
| `logging.file.path`        | `"logs/aeromux-.log"`          | `"/var/log/aeromux/aeromux-.log"`       |
| `database.enabled`         | `false`                        | `true`                                  |
| `database.path`            | `"artifacts/db/"`              | `"/var/lib/aeromux/"`                   |

Console logging is enabled so that `docker logs` captures output. The database is enabled because the entrypoint auto-downloads it on first run.

### Custom Configuration

The image works out of the box with the default configuration. To customize, extract the default config, edit it, and mount it as a volume:

```bash
# Extract the default config from the image
docker run --rm ghcr.io/aeromux/aeromux:latest cat /etc/aeromux/aeromux.yaml > aeromux.yaml

# Edit the config
nano aeromux.yaml

# Uncomment the config volume mount in docker-compose.yaml, then restart
docker compose up -d
```

The `docker-compose.yaml` template ships with the config mount commented out:

```yaml
volumes:
  - aeromux-data:/var/lib/aeromux
  - aeromux-logs:/var/log/aeromux
  # Uncomment to use a custom configuration file:
  # - ./aeromux.yaml:/etc/aeromux/aeromux.yaml
```

When pulling a new image version, the user's mounted configuration file is unaffected — it lives on the host filesystem.

## Aircraft Database

### Automatic First-Run Download

The entrypoint script checks if `/var/lib/aeromux/` is empty on container start. If no database files are found and the daemon command is being run, it automatically runs `aeromux database update`. The database persists in the `aeromux-data` named volume across restarts and upgrades.

### Manual Update

```bash
# Running container
docker exec aeromux aeromux database update --config /etc/aeromux/aeromux.yaml

# Stopped container
docker compose run --rm aeromux aeromux database update --config /etc/aeromux/aeromux.yaml
```

## Entrypoint Script

The `docker-entrypoint.sh` script (`docker/entrypoint.sh`) is the container's entrypoint. Docker always runs the entrypoint — only the CMD portion is replaced when the user overrides the command.

1. **Fix volume ownership** — runs `chown aeromux:aeromux` on `/var/lib/aeromux` and `/var/log/aeromux` to ensure the non-root user can write to bind-mounted volumes (e.g., Synology DSM). This is a no-op when ownership already matches.
2. **Check if running the daemon** — inspects `$1` and `$2` to determine if the default `aeromux daemon` command is being run.
3. **Auto-download database (daemon only)** — if running the daemon and `/var/lib/aeromux/` is empty, downloads the database via `gosu aeromux`. Skipped for non-daemon commands.
4. **Drop privileges and exec** — `exec gosu aeromux "$@"` drops from root to the `aeromux` user, replaces the shell process with the actual command, so aeromux runs as PID 1 and receives signals (SIGTERM, etc.) directly.

| Command                                                   | Entrypoint Behavior                                                                   |
|-----------------------------------------------------------|---------------------------------------------------------------------------------------|
| `docker compose up` (default CMD)                         | Fixes permissions, auto-downloads database if needed, drops to aeromux, starts daemon |
| `docker exec aeromux aeromux database update`             | Bypasses entrypoint entirely (exec into running container)                            |
| `docker compose run --rm aeromux aeromux database update` | Fixes permissions, skips auto-download, drops to aeromux, execs the override command  |

## USB Device Access

RTL-SDR devices are USB peripherals that must be passed through to the container using a cgroup rule and a volume mount:

```yaml
device_cgroup_rules:
  - 'c 189:* rwm'
volumes:
  - /dev/bus/usb:/dev/bus/usb
```

The cgroup rule grants access to all USB devices (major number 189), and the volume mount makes the device nodes visible inside the container. This approach survives USB hotplug — if an RTL-SDR disconnects and reconnects, the new device node is automatically accessible without restarting the container.

| Platform               | USB Passthrough                                                            |
|------------------------|----------------------------------------------------------------------------|
| Linux (native Docker)  | Works with cgroup rule + volume mount in Compose                           |
| Raspberry Pi           | Same as Linux — works natively                                             |
| Synology DSM           | Container Manager GUI has limited device support; use SSH CLI or Portainer |
| OrbStack (macOS)       | USB passthrough not supported — use Beast TCP input instead                |
| Docker Desktop (macOS) | USB passthrough not supported — use Beast TCP input instead                |
| Docker Desktop (Win)   | USB passthrough not supported — use Beast TCP input instead                |

For platforms without USB passthrough, Aeromux can receive data via Beast TCP input sources (`beastSources` in the config) from an external receiver.

## Network Ports

| Port    | Protocol | Description                              | Default   |
|---------|----------|------------------------------------------|-----------|
| `30005` | TCP      | Beast binary protocol output             | Enabled   |
| `30006` | TCP      | JSON streaming output                    | Disabled  |
| `30003` | TCP      | SBS BaseStation text protocol output     | Disabled  |
| `8080`  | TCP      | REST API                                 | Enabled   |
| `30104` | TCP      | MLAT input (from mlat-client)            | Enabled   |

The `docker-compose.yaml` template maps the default-enabled ports and includes commented entries for the optional ports.

## Synology DSM

Synology's Container Manager GUI has limitations compared to the Docker CLI:

| Feature                | Limitation                                                                                         |
|------------------------|----------------------------------------------------------------------------------------------------|
| Volume mappings        | Not auto-populated from the image — manually add `/var/lib/aeromux` and `/var/log/aeromux` mounts |
| USB device passthrough | `--device` flag not available in the GUI — use SSH CLI or Portainer                               |

For volumes, click **Add Folder** in the Volume Settings panel and create the following mappings:

| Host folder (example)             | Container path                  | Required |
|-----------------------------------|---------------------------------|----------|
| `docker/aeromux/data`             | `/var/lib/aeromux`              | Yes      |
| `docker/aeromux/logs`             | `/var/log/aeromux`              | Yes      |
| `docker/aeromux/aeromux.yaml`     | `/etc/aeromux/aeromux.yaml`     | No       |

Without explicit mappings for data and logs, Docker uses anonymous volumes that may be lost on container recreation. The config file mount is only needed when using a custom configuration (see [Custom Configuration](#custom-configuration)).

If not using a local RTL-SDR device, configure Beast TCP input sources (`beastSources` in the config) to receive data from an external receiver.

## Docker Compose Template

A `docker-compose.yaml` template is provided in `docker/` with pre-configured device mapping, port mappings, and volume mounts. See `docker/docker-compose.yaml` for the full template with inline comments.

Quick start:

```bash
cd docker/
docker compose up -d
```

## Build Script

### Usage

```
./docker/build-image.sh [OPTIONS]

Options:
  --push             Push to GHCR after building (requires authentication)
  --rebuild          Force rebuild of binaries even if they exist and are recent
  --silent           Suppress all output (only errors are shown)
```

The script always builds both architectures (`linux/arm64` and `linux/amd64`).

### Examples

```bash
./docker/build-image.sh                   # Build images (both architectures)
./docker/build-image.sh --push            # Build and push to GHCR
./docker/build-image.sh --push --rebuild  # Rebuild binaries, then build and push
```

### Workflow

1. **Check tools** — verifies `docker` and `docker buildx` are available, and checks GHCR authentication if `--push` is specified.
2. **Check binaries** — verifies `artifacts/binaries/linux-arm64/aeromux` and `artifacts/binaries/linux-x64/aeromux` exist and are recent. With `--rebuild`, runs `build.sh` for both targets.
3. **Read version** — extracts the version from `src/Directory.Build.props`.
4. **Prepare build context** — creates a temporary directory with the Dockerfile, entrypoint, generated config, and architecture-specific binaries.
5. **Build and save** — builds each architecture with `docker buildx build --output type=docker,dest=<file>`, saving `.tar` files to `artifacts/docker/`.
6. **Push (optional)** — with `--push`, builds and pushes a multi-arch manifest to GHCR using `docker buildx build --platform linux/arm64,linux/amd64 --push`.

### Build Context Layout

The build script assembles a temporary build context directory:

```
build-context/
  Dockerfile
  entrypoint.sh
  aeromux.yaml                        # Generated default config
  binaries/
    arm64/aeromux                     # From artifacts/binaries/linux-arm64/aeromux
    amd64/aeromux                     # From artifacts/binaries/linux-x64/aeromux
```

The Dockerfile's `COPY binaries/${TARGETARCH}/aeromux` instruction uses the `TARGETARCH` variable (automatically set by buildx to `arm64` or `amd64`) to select the correct binary.

### GHCR Authentication

Authenticate before using `--push`:

```bash
echo $GITHUB_TOKEN | docker login ghcr.io -u USERNAME --password-stdin
```

### Image Output

Saved `.tar` files follow the naming convention `aeromux_<version>_linux_<arch>.tar`:

```
artifacts/docker/
  aeromux_0.6.1_linux_arm64.tar
  aeromux_0.6.1_linux_amd64.tar
```

Load a saved image with:

```bash
docker load -i artifacts/docker/aeromux_0.6.1_linux_arm64.tar
```

## Build Environment

### Requirements

- **Docker** with BuildKit enabled (Docker 19.03+).
- **Docker Buildx** — for multi-arch builds. Included with Docker Desktop; on Linux, install as a CLI plugin.
- **`dotnet`** — .NET SDK for building the binary (already required by `build.sh`).

The script works on both macOS and Linux. Docker Buildx handles cross-architecture builds transparently.

## Static Files

All Docker distribution files live in `docker/`:

| File                   | Description                                           |
|------------------------|-------------------------------------------------------|
| `build-image.sh`       | Docker image build and push script                    |
| `Dockerfile`           | Multi-arch Dockerfile                                 |
| `entrypoint.sh`        | Container entrypoint (database check + daemon start)  |
| `docker-compose.yaml`  | Compose template for users                            |

