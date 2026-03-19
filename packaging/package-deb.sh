#!/bin/bash

# Aeromux Debian Packaging Script
# This script creates .deb packages from built binaries
#
# Copyright (C) 2025-2026 Nandor Toth <dev@nandortoth.com>
#
# This program is free software: you can redistribute it and/or modify
# it under the terms of the GNU General Public License as published by
# the Free Software Foundation, either version 3 of the License, or
# (at your option) any later version.
#
# This program is distributed in the hope that it will be useful,
# but WITHOUT ANY WARRANTY; without even the implied warranty of
# MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
# GNU General Public License for more details.
#
# You should have received a copy of the GNU General Public License
# along with this program. If not, see <https://www.gnu.org/licenses/>.

set -e  # Exit on error

# Configuration
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"
DEB_DIR="$SCRIPT_DIR/deb"
ARTIFACTS_DIR="$PROJECT_ROOT/artifacts"
PACKAGES_DIR="$ARTIFACTS_DIR/packages"
PROPS_FILE="$PROJECT_ROOT/src/Directory.Build.props"
EXAMPLE_CONFIG="$PROJECT_ROOT/aeromux.example.yaml"

# ── Functions ────────────────────────────────────────────────────────────────

usage() {
    echo "Usage: ./packaging/package-deb.sh [OPTIONS]"
    echo ""
    echo "Options:"
    echo "  --target TARGET    Target platform: linux-arm64, linux-x64, or all (required)"
    echo "  --rebuild          Force rebuild even if binaries exist and are recent"
    echo "  --silent           Suppress all output (only errors are shown)"
    echo ""
    echo "Examples:"
    echo "  ./packaging/package-deb.sh --target linux-arm64"
    echo "  ./packaging/package-deb.sh --target linux-x64"
    echo "  ./packaging/package-deb.sh --target all"
    echo "  ./packaging/package-deb.sh --target all --rebuild"
    exit 1
}

# Logging helper (suppressed in silent mode)
log() { [ "$SILENT" = true ] || echo "$@"; }

# Run a command quietly — suppress output on success, show on failure
run_quiet() {
    local output
    output=$("$@" 2>&1) || {
        echo ""
        echo "================================================"
        echo "PACKAGING FAILED"
        echo "================================================"
        if [ -n "$output" ]; then
            echo ""
            echo "$output"
        fi
        echo ""
        exit 1
    }
}

# Resolve a target to Debian arch and .NET runtime ID (echoes "DEB_ARCH RUNTIME_ID")
resolve_deb_target() {
    case "$1" in
        linux-arm64) echo "arm64 linux-arm64" ;;
        linux-x64)   echo "amd64 linux-x64" ;;
        *)
            echo "ERROR: Invalid target for .deb packaging: $1" >&2
            echo "" >&2
            echo "Valid targets: linux-arm64, linux-x64, all" >&2
            exit 1
            ;;
    esac
}

# Build a .deb package for a single target (uses DEB_ARCH, RUNTIME_ID, DEB_VERSION)
build_package() {
    local deb_arch="$1"
    local runtime_id="$2"
    local binary="$ARTIFACTS_DIR/binaries/$runtime_id/aeromux"

    local staging
    staging=$(mktemp -d)

    # Create directory tree
    mkdir -p "$staging/DEBIAN"
    mkdir -p "$staging/usr/bin"
    mkdir -p "$staging/usr/lib/systemd/system"
    mkdir -p "$staging/usr/share/man/man1"
    mkdir -p "$staging/usr/share/doc/aeromux"
    mkdir -p "$staging/etc/aeromux"
    mkdir -p "$staging/etc/udev/rules.d"

    # Populate staging
    install -m 0755 "$binary" "$staging/usr/bin/aeromux"

    cp "$DEB_DIR/aeromux.service" "$staging/usr/lib/systemd/system/aeromux.service"
    cp "$DEB_DIR/99-aeromux-rtlsdr.rules" "$staging/etc/udev/rules.d/99-aeromux-rtlsdr.rules"
    cp "$DEB_DIR/copyright" "$staging/usr/share/doc/aeromux/copyright"
    cp "$DEB_DIR/conffiles" "$staging/DEBIAN/conffiles"
    cp "$DEB_DIR/postinst" "$staging/DEBIAN/postinst"
    cp "$DEB_DIR/prerm" "$staging/DEBIAN/prerm"
    cp "$DEB_DIR/postrm" "$staging/DEBIAN/postrm"

    chmod 0755 "$staging/DEBIAN/postinst"
    chmod 0755 "$staging/DEBIAN/prerm"
    chmod 0755 "$staging/DEBIAN/postrm"

    # Compress man page
    gzip -9 -c "$DEB_DIR/aeromux.1" > "$staging/usr/share/man/man1/aeromux.1.gz"

    # Generate aeromux.yaml from example config with path transformations
    sed -e 's|level: debug                            |level: information                      |' \
        -e '/^  console:/,/^  [a-z]/ s|enabled: false|enabled: true |' \
        -e 's|path: "logs/aeromux-.log"             |path: "/var/log/aeromux/aeromux-.log" |' \
        -e 's|path: "artifacts/db/"                   |path: "/var/lib/aeromux/"               |' \
        "$EXAMPLE_CONFIG" > "$staging/etc/aeromux/aeromux.yaml"

    # Generate control file
    local installed_size
    installed_size=$(du -sk "$staging" | awk '{print $1}')

    cat > "$staging/DEBIAN/control" << EOF
Package: aeromux
Version: $DEB_VERSION
Section: misc
Priority: optional
Architecture: $deb_arch
Depends: librtlsdr0
Installed-Size: $installed_size
Maintainer: Nandor Toth <dev@nandortoth.com>
Homepage: https://github.com/nandortoth/aeromux
Description: Multi-SDR Mode S and ADS-B demodulator and decoder
 Multi-SDR Mode S and ADS-B demodulator and decoder for .NET.
 Aeromux receives 1090 MHz ADS-B and Mode S signals from one or more
 RTL-SDR devices, decodes aircraft transponder messages, and provides
 real-time aircraft tracking via a terminal UI, REST API, and network
 streaming protocols (Beast, SBS BaseStation, JSON).
EOF

    # Build package
    mkdir -p "$PACKAGES_DIR"
    local pkg_filename="aeromux_${DEB_VERSION}_${deb_arch}.deb"
    local output_path="$PACKAGES_DIR/$pkg_filename"

    run_quiet dpkg-deb --build --root-owner-group "$staging" "$output_path"

    # Cleanup staging
    rm -rf "$staging"

    echo "$pkg_filename"
}

# ── Argument parsing ─────────────────────────────────────────────────────────

SILENT=false
TARGET=""
REBUILD=false

while [[ $# -gt 0 ]]; do
    case "$1" in
        --silent)
            SILENT=true
            shift
            ;;
        --rebuild)
            REBUILD=true
            shift
            ;;
        --target)
            if [[ -z "$2" || "$2" == --* ]]; then
                echo "ERROR: --target requires a value"
                exit 1
            fi
            TARGET="$2"
            shift 2
            ;;
        *)
            echo "ERROR: Unknown option: $1"
            echo ""
            usage
            ;;
    esac
done

# ── Main ─────────────────────────────────────────────────────────────────────

# Validate target is specified
if [ -z "$TARGET" ]; then
    echo "ERROR: --target is required"
    echo ""
    echo "Usage: ./packaging/package-deb.sh --target <linux-arm64|linux-x64|all>"
    exit 1
fi

# Resolve targets into arrays
if [ "$TARGET" = "all" ]; then
    TARGETS=(linux-arm64 linux-x64)
else
    TARGETS=("$TARGET")
fi

DEB_ARCHS=()
RUNTIME_IDS=()
for t in "${TARGETS[@]}"; do
    resolved=$(resolve_deb_target "$t")
    DEB_ARCHS+=("$(echo "$resolved" | awk '{print $1}')")
    RUNTIME_IDS+=("$(echo "$resolved" | awk '{print $2}')")
done

# Clear screen and print header
[ "$SILENT" = true ] || clear

log "================================================"
log "Aeromux Debian Packaging"
log "================================================"
log ""

if [ "${#TARGETS[@]}" -gt 1 ]; then
    log "Packaging all targets..."
    log ""
fi

# Check dpkg-deb (once)
log "Checking build tools..."
if ! command -v dpkg-deb > /dev/null 2>&1; then
    echo "ERROR: dpkg-deb not found."
    if [[ "$OSTYPE" == "darwin"* ]]; then
        echo "Install with: brew install dpkg"
    else
        echo "Install with: sudo apt install dpkg"
    fi
    exit 1
fi
log "✓ dpkg-deb found"
log ""

# Check/rebuild binaries (per target)
if [ "${#TARGETS[@]}" -eq 1 ]; then
    log "Checking binary..."
else
    log "Checking binaries..."
fi

for i in "${!TARGETS[@]}"; do
    rid="${RUNTIME_IDS[$i]}"
    target="${TARGETS[$i]}"
    binary="$ARTIFACTS_DIR/binaries/$rid/aeromux"

    if [ "$REBUILD" = true ]; then
        run_quiet "$PROJECT_ROOT/build.sh" --target "$target" --silent
        log "✓ Binary rebuilt: $rid"
    elif [ ! -f "$binary" ]; then
        echo "ERROR: Binary not found at: $binary"
        echo "Run './build.sh --target $target' first."
        exit 1
    elif [ -n "$(find "$binary" -mmin +60 2>/dev/null)" ]; then
        echo "ERROR: Binary is older than 1 hour: $binary"
        echo "Run './build.sh --target $target' to rebuild, or use --rebuild."
        exit 1
    else
        log "✓ Binary found: $rid"
    fi
done
log ""

# Read version (once)
VERSION=$(sed -n 's/.*<Version>\(.*\)<\/Version>.*/\1/p' "$PROPS_FILE")
DEB_VERSION="${VERSION}-1"
log "Package version: $DEB_VERSION"
log ""

# Package each target
if [ "${#TARGETS[@]}" -eq 1 ]; then
    log "Preparing package..."
else
    log "Preparing packages..."
fi

PKG_FILENAMES=()
for i in "${!TARGETS[@]}"; do
    deb_arch="${DEB_ARCHS[$i]}"
    rid="${RUNTIME_IDS[$i]}"

    pkg_filename=$(build_package "$deb_arch" "$rid")
    PKG_FILENAMES+=("$pkg_filename")

    log "✓ $pkg_filename packaged"
done
log ""

# Summary
ARCH_LIST="${DEB_ARCHS[0]}"
for arch in "${DEB_ARCHS[@]:1}"; do
    ARCH_LIST="$ARCH_LIST, $arch"
done

log "================================================"
log "PACKAGING SUMMARY"
log "================================================"
log ""
log "Package created successfully!"
log "Architecture: $ARCH_LIST"
log "Version:      $DEB_VERSION"
log "Output:       artifacts/packages/"
log ""

if [ "${#PKG_FILENAMES[@]}" -eq 1 ]; then
    log "Package:"
else
    log "Packages:"
fi
for pkg in "${PKG_FILENAMES[@]}"; do
    pkg_path="$PACKAGES_DIR/$pkg"
    if [ -f "$pkg_path" ]; then
        pkg_size=$(ls -lh "$pkg_path" | awk '{print $5}')
        log "  - artifacts/packages/$pkg ($pkg_size)"
    fi
done

log ""
log "Install on the target machine:"
for i in "${!PKG_FILENAMES[@]}"; do
    log "  - ${TARGETS[$i]}: sudo dpkg -i artifacts/packages/${PKG_FILENAMES[$i]}"
done
log ""
