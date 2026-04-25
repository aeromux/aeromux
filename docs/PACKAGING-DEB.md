# Debian Packaging

Aeromux is distributed as a `.deb` package for Debian-based Linux systems. The primary target is Raspberry Pi 4/5 (ARM64), with x86-64 as a secondary platform. The package installs Aeromux as a systemd service with a dedicated system user, proper filesystem layout, and udev rules for RTL-SDR USB device access.

Packages are built using `dpkg-deb` and can be cross-compiled from any platform, including macOS.

## Package Metadata

| Field         | Value                                                 |
|---------------|-------------------------------------------------------|
| Package       | `aeromux`                                             |
| Version       | From `src/Directory.Build.props` (e.g., `0.6.2-1`)    |
| Section       | `misc`                                                |
| Priority      | `optional`                                            |
| Architecture  | `arm64` or `amd64`                                    |
| Depends       | `librtlsdr0`                                          |
| Maintainer    | `Nandor Toth <dev@nandortoth.com>`                    |
| Homepage      | `https://github.com/aeromux/aeromux`               |
| License       | GPL-3.0-or-later                                      |

The version string follows Debian conventions: `<upstream-version>-<debian-revision>` (e.g., `0.6.2-1`). The upstream version is read from `src/Directory.Build.props` at packaging time.

## Filesystem Layout

### Installed Files

| Path                                          | Description                          | Mode   |
|-----------------------------------------------|--------------------------------------|--------|
| `/usr/bin/aeromux`                            | Self-contained executable            | `0755` |
| `/etc/aeromux/aeromux.yaml`                   | Default configuration (conffile)     | `0644` |
| `/usr/lib/systemd/system/aeromux.service`     | Systemd unit file                    | `0644` |
| `/etc/udev/rules.d/99-aeromux-rtlsdr.rules`   | RTL-SDR USB device access            | `0644` |
| `/usr/share/man/man1/aeromux.1.gz`            | Manual page                          | `0644` |
| `/usr/share/doc/aeromux/copyright`            | GPLv3 copyright file                 | `0644` |

### Created Directories

The following directories are created by the `postinst` script with `aeromux:aeromux` ownership:

| Path                  | Description                     |
|-----------------------|---------------------------------|
| `/var/lib/aeromux/`   | Aircraft metadata database      |
| `/var/log/aeromux/`   | Log files                       |

## Configuration

The package ships a default configuration file at `/etc/aeromux/aeromux.yaml`, generated at packaging time from the project's `aeromux.example.yaml` with the following transformations applied via `sed`. The replacement patterns include trailing whitespace to preserve the inline comment alignment of the original file.

| Setting                   | Example Config Value         | Package Config Value                 |
|---------------------------|------------------------------|--------------------------------------|
| `logging.level`           | `debug`                      | `information`                        |
| `logging.file.path`       | `"logs/aeromux-.log"`        | `"/var/log/aeromux/aeromux-.log"`    |
| `database.path`           | `"artifacts/db/"`            | `"/var/lib/aeromux/"`                |

There is no separate configuration file to maintain — the packaging script reads the current `aeromux.example.yaml` and applies the substitutions listed above.

### Conffile Behavior

The configuration file is marked as a Debian **conffile**, which means `dpkg` tracks user modifications across upgrades:

- **Install:** the file is created.
- **Upgrade:** if the user has modified the file, `dpkg` prompts whether to keep the user's version or install the new package version.
- **Remove (`dpkg -r`):** the file is kept.
- **Purge (`dpkg -P`):** the file is removed.

## Systemd Service

The unit file installs Aeromux as a `simple` service running under a dedicated `aeromux` user with security hardening (`ProtectSystem=strict`, `NoNewPrivileges=true`, `PrivateTmp=true`). The service reads its configuration from `/etc/aeromux/aeromux.yaml` and has write access only to `/var/lib/aeromux` and `/var/log/aeromux`.

The service is **installed but not enabled or started** by default. The user must configure their SDR device and receiver location before starting the service.

Enable and start:

```bash
sudo systemctl enable --now aeromux
```

View logs:

```bash
journalctl -u aeromux -f
```

## System User and Permissions

The `postinst` script creates a dedicated `aeromux` system user and group:

```bash
adduser --system --group --home /var/lib/aeromux --no-create-home aeromux
```

The data and log directories (`/var/lib/aeromux`, `/var/log/aeromux`) are created before the user to avoid the `adduser` warning about a missing home directory. The user is then added to the `plugdev` group for USB device access. A udev rule grants the `aeromux` group access to RTL-SDR USB devices (vendor `0bda`, products `2832` and `2838`).

## Man Page

The package includes a man page at `/usr/share/man/man1/aeromux.1.gz` covering commands, global options, file paths, and systemd usage. The source is stored as `packaging/deb/aeromux.1` in troff format and compressed with `gzip -9` during packaging.

## Building Packages

The `.deb` packaging script builds packages for one or both Linux targets in a single pipeline:

```bash
./packaging/package-deb.sh --target linux-arm64
./packaging/package-deb.sh --target linux-x64
./packaging/package-deb.sh --target all
./packaging/package-deb.sh --target all --rebuild
```

The `--target all` option packages both Linux architectures (`arm64` and `amd64`) sequentially in one run, with per-target progress output.

### Options

| Option      | Description                                                        |
|-------------|--------------------------------------------------------------------|
| `--target`  | Target platform: `linux-arm64`, `linux-x64`, or `all` (required)   |
| `--rebuild` | Force rebuild of the binary even if it exists and is recent         |
| `--silent`  | Suppress all output (only errors are shown)                        |

### Workflow

1. Parse and validate the target architecture.
2. Check that `dpkg-deb` is available.
3. Check the binary at `artifacts/binaries/<runtime-id>/aeromux` — it must exist and be less than 1 hour old, or use `--rebuild` to trigger a fresh build.
4. Read the version from `src/Directory.Build.props`.
5. For each target, create a temporary staging directory with the full `.deb` directory tree.
6. Populate staging with the binary, static files from `packaging/deb/`, and the generated configuration.
7. Generate the `DEBIAN/control` file with computed `Installed-Size`.
8. Build the package with `dpkg-deb --build --root-owner-group`.
9. Output the `.deb` file to `artifacts/packages/`.

### Build Requirements

- **`dpkg-deb`** — for building the `.deb` package.
  - macOS: `brew install dpkg`
  - Linux: pre-installed on Debian-based systems, or `sudo apt install dpkg`
- **`dotnet`** — .NET SDK for building the binary (already required by `build.sh`).

## Cross-Compilation

The packaging script supports building packages for any Linux target from any host platform. The `build.sh` script handles cross-compilation of the .NET binary, and `dpkg-deb` works on any platform.

```bash
# Build the binary first, then package
./build.sh --target linux-arm64
./packaging/package-deb.sh --target linux-arm64

# Build and package both Linux targets
./build.sh --target all
./packaging/package-deb.sh --target all

# Or rebuild as part of packaging (--rebuild calls build.sh automatically)
./packaging/package-deb.sh --target all --rebuild
```

## Install, Upgrade, Remove, and Purge

### Install

```bash
sudo dpkg -i aeromux_0.6.2-1_arm64.deb
```

Creates the data and log directories, creates the `aeromux` system user and group, reloads udev rules and systemd, and displays a post-install message with next steps.

### Upgrade

```bash
sudo dpkg -i aeromux_0.6.2-1_arm64.deb
```

Stops the running service, installs new files, and restarts the service. If the configuration file has been modified by the user, `dpkg` prompts whether to keep the user's version or install the new package version.

### Remove

```bash
sudo dpkg -r aeromux
```

Stops and disables the service. Removes the binary, service file, udev rule, and man page. Keeps the configuration (`/etc/aeromux/`), database (`/var/lib/aeromux/`), logs (`/var/log/aeromux/`), and the `aeromux` user.

### Purge

```bash
sudo dpkg -P aeromux
```

Everything from remove, plus removes `/etc/aeromux/`, `/var/lib/aeromux/`, `/var/log/aeromux/`, and the `aeromux` user and group.

## Static Files

The following files are stored in `packaging/deb/` and copied into the package by the packaging script:

| File                       | Description                             |
|----------------------------|-----------------------------------------|
| `aeromux.service`          | Systemd unit file                       |
| `99-aeromux-rtlsdr.rules`  | Udev rule for RTL-SDR access            |
| `aeromux.1`                | Man page source (troff)                 |
| `copyright`                | GPLv3 copyright file (Debian format)    |
| `postinst`                 | Post-installation script                |
| `prerm`                    | Pre-removal script                      |
| `postrm`                   | Post-removal script                     |
| `conffiles`                | List of configuration files             |

Two additional files are generated at packaging time:

| File              | Source                                                                 |
|-------------------|------------------------------------------------------------------------|
| `DEBIAN/control`  | Generated from package metadata + version from `Directory.Build.props` |
| `aeromux.yaml`    | Generated from `aeromux.example.yaml` with path transformations        |
