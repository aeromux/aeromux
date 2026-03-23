#!/bin/bash

# Aeromux macOS Packaging Script
# This script creates .pkg packages from built binaries
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
PKG_DIR="$SCRIPT_DIR/pkg"
ARTIFACTS_DIR="$PROJECT_ROOT/artifacts"
PACKAGES_DIR="$ARTIFACTS_DIR/packages"
PROPS_FILE="$PROJECT_ROOT/src/Directory.Build.props"
EXAMPLE_CONFIG="$PROJECT_ROOT/aeromux.example.yaml"
NOTARY_PROFILE="aeromux-notary"

# ── Functions ────────────────────────────────────────────────────────────────

usage() {
    echo "Usage: ./packaging/package-pkg.sh [OPTIONS]"
    echo ""
    echo "Options:"
    echo "  --target TARGET    Target platform: osx-arm64, osx-x64, or all (required)"
    echo "  --sign             Sign the binary and package with Developer ID certificates"
    echo "  --notarize         Notarize and staple the package (requires --sign)"
    echo "  --rebuild          Force rebuild even if binaries exist and are recent"
    echo "  --silent           Suppress all output (only errors are shown)"
    echo ""
    echo "Examples:"
    echo "  ./packaging/package-pkg.sh --target osx-arm64"
    echo "  ./packaging/package-pkg.sh --target osx-arm64 --sign"
    echo "  ./packaging/package-pkg.sh --target all --sign --notarize --rebuild"
    exit 1
}

# Logging helper (suppressed in silent mode)
log() { [ "$SILENT" = true ] || echo "$@"; }

# Run a command quietly — suppress output on success, show on failure
run_quiet() {
    local output
    output=$("$@" 2>&1) || {
        echo "" >&2
        echo "================================================" >&2
        echo "PACKAGING FAILED" >&2
        echo "================================================" >&2
        if [ -n "$output" ]; then
            echo "" >&2
            echo "$output" >&2
        fi
        echo "" >&2
        exit 1
    }
}

# Resolve a target to pkg arch, .NET runtime ID, and build target
# (echoes "PKG_ARCH RUNTIME_ID BUILD_TARGET")
resolve_pkg_target() {
    case "$1" in
        osx-arm64) echo "arm64 osx-arm64 macos-arm64" ;;
        osx-x64)   echo "x86_64 osx-x64 macos-x64" ;;
        *)
            echo "ERROR: Invalid target for .pkg packaging: $1" >&2
            echo "" >&2
            echo "Valid targets: osx-arm64, osx-x64, all" >&2
            exit 1
            ;;
    esac
}

# Build a .pkg package for a single target
build_package() {
    local pkg_arch="$1"
    local runtime_id="$2"
    local binary="$ARTIFACTS_DIR/binaries/$runtime_id/aeromux"

    local staging
    staging=$(mktemp -d)

    # Create directory structure
    mkdir -p "$staging/payload/bin"
    mkdir -p "$staging/payload/share/man/man1"
    mkdir -p "$staging/payload/share"
    mkdir -p "$staging/scripts"
    mkdir -p "$staging/resources"

    # Install binary and uninstall script
    install -m 0755 "$binary" "$staging/payload/bin/aeromux"
    install -m 0755 "$PKG_DIR/aeromux-uninstall" "$staging/payload/bin/aeromux-uninstall"

    # Sign the binary with Developer ID Application + hardened runtime
    if [ "$SIGN" = true ]; then
        run_quiet codesign --sign "Developer ID Application" --options runtime --entitlements "$PKG_DIR/entitlements.plist" --force "$staging/payload/bin/aeromux"
        if ! codesign --verify "$staging/payload/bin/aeromux" > /dev/null 2>&1; then
            echo "ERROR: Binary signature verification failed." >&2
            rm -rf "$staging"
            exit 1
        fi
        log "  ✓ Binary signed: $runtime_id" >&2
    fi

    # Compress man page
    gzip -9 -c "$PKG_DIR/aeromux.1" > "$staging/payload/share/man/man1/aeromux.1.gz"

    # Generate config template with macOS path transformations
    # Use $HOME as a placeholder — postinstall expands it to the actual user home
    sed -e 's|level: debug                            |level: information                      |' \
        -e 's|path: "logs/aeromux-.log"             # Log file path with date placeholder|path: "$HOME/Library/Logs/aeromux/aeromux-.log" # Log file path with date placeholder|' \
        -e 's|path: "artifacts/db/"                   # Directory for database storage (relative or absolute path)|path: "$HOME/Library/Application Support/aeromux/" # Directory for database storage (relative or absolute path)|' \
        "$EXAMPLE_CONFIG" > "$staging/payload/share/aeromux.example.yaml"

    # Copy the same config to the scripts directory for postinstall to place
    cp "$staging/payload/share/aeromux.example.yaml" "$staging/scripts/aeromux.yaml"

    # Copy postinstall script
    install -m 0755 "$PKG_DIR/postinstall" "$staging/scripts/postinstall"

    # Prepare resources with version substitution
    sed "s/VERSION_PLACEHOLDER/$VERSION/" "$PKG_DIR/resources/welcome.html" > "$staging/resources/welcome.html"
    cp "$PKG_DIR/resources/license.html" "$staging/resources/license.html"
    cp "$PKG_DIR/resources/conclusion.html" "$staging/resources/conclusion.html"

    # Generate distribution.xml from template
    sed -e "s/{{PKG_ARCH}}/$pkg_arch/" \
        -e "s/{{VERSION}}/$VERSION/" \
        "$PKG_DIR/distribution.xml" > "$staging/distribution.xml"

    # Build component package
    run_quiet pkgbuild \
        --root "$staging/payload" \
        --identifier com.aeromux \
        --version "$VERSION" \
        --install-location /opt/aeromux \
        --scripts "$staging/scripts" \
        "$staging/aeromux-component.pkg"

    # Build product archive
    mkdir -p "$PACKAGES_DIR"
    local pkg_filename="aeromux_${VERSION}_macos_${pkg_arch}.pkg"
    local output_path="$PACKAGES_DIR/$pkg_filename"

    if [ "$SIGN" = true ]; then
        run_quiet productbuild \
            --distribution "$staging/distribution.xml" \
            --resources "$staging/resources" \
            --package-path "$staging" \
            --sign "Developer ID Installer: Nándor Tóth" \
            "$output_path"
    else
        run_quiet productbuild \
            --distribution "$staging/distribution.xml" \
            --resources "$staging/resources" \
            --package-path "$staging" \
            "$output_path"
    fi

    # Verify signature if signed
    if [ "$SIGN" = true ]; then
        if ! pkgutil --check-signature "$output_path" > /dev/null 2>&1; then
            echo "ERROR: Package signature verification failed." >&2
            rm -f "$output_path"
            rm -rf "$staging"
            exit 1
        fi
        log "  ✓ Package signed: $pkg_filename" >&2
    fi

    # Cleanup staging
    rm -rf "$staging"

    echo "$pkg_filename"
}

# Notarize and staple a .pkg package
notarize_package() {
    local pkg_filename="$1"
    local output_path="$PACKAGES_DIR/$pkg_filename"

    xcrun notarytool submit "$output_path" \
        --keychain-profile "$NOTARY_PROFILE" --wait > /dev/null 2>&1 || {
        echo "ERROR: Notarization failed for $pkg_filename." >&2
        return 1
    }

    xcrun stapler staple "$output_path" > /dev/null 2>&1 || {
        echo "ERROR: Stapling failed for $pkg_filename." >&2
        return 1
    }

    if ! xcrun stapler validate "$output_path" > /dev/null 2>&1; then
        echo "ERROR: Notarization validation failed for $pkg_filename." >&2
        return 1
    fi

    return 0
}

# ── Argument parsing ─────────────────────────────────────────────────────────

SILENT=false
TARGET=""
REBUILD=false
SIGN=false
NOTARIZE=false

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
        --sign)
            SIGN=true
            shift
            ;;
        --notarize)
            NOTARIZE=true
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
    echo "Usage: ./packaging/package-pkg.sh --target <osx-arm64|osx-x64|all>"
    exit 1
fi

# Validate --notarize requires --sign
if [ "$NOTARIZE" = true ] && [ "$SIGN" != true ]; then
    echo "ERROR: --notarize requires --sign"
    exit 1
fi

# Resolve targets into arrays
if [ "$TARGET" = "all" ]; then
    TARGETS=(osx-arm64 osx-x64)
else
    TARGETS=("$TARGET")
fi

PKG_ARCHS=()
RUNTIME_IDS=()
BUILD_TARGETS=()
for t in "${TARGETS[@]}"; do
    resolved=$(resolve_pkg_target "$t")
    PKG_ARCHS+=("$(echo "$resolved" | awk '{print $1}')")
    RUNTIME_IDS+=("$(echo "$resolved" | awk '{print $2}')")
    BUILD_TARGETS+=("$(echo "$resolved" | awk '{print $3}')")
done

# Clear screen and print header
[ "$SILENT" = true ] || clear

log "================================================"
log "Aeromux macOS Packaging"
log "================================================"
log ""

if [ "${#TARGETS[@]}" -gt 1 ]; then
    log "Packaging all targets..."
    log ""
fi

# Check pkgbuild and productbuild
log "Checking build tools..."
if ! command -v pkgbuild > /dev/null 2>&1 || ! command -v productbuild > /dev/null 2>&1; then
    echo "ERROR: pkgbuild/productbuild not found."
    echo "Install Xcode Command Line Tools: xcode-select --install"
    exit 1
fi
log "✓ pkgbuild and productbuild found"

# Check signing certificates if --sign
if [ "$SIGN" = true ]; then
    if ! security find-identity -v -p basic 2>/dev/null | grep -q "Developer ID Application"; then
        echo "ERROR: Developer ID Application certificate not found in Keychain."
        echo "Install the certificate from the Apple Developer portal."
        exit 1
    fi
    log "✓ Developer ID Application certificate found"

    if ! security find-identity -v -p basic 2>/dev/null | grep -q "Developer ID Installer"; then
        echo "ERROR: Developer ID Installer certificate not found in Keychain."
        echo "Install the certificate from the Apple Developer portal."
        exit 1
    fi
    log "✓ Developer ID Installer certificate found"
fi

# Check notarization prerequisites if --notarize
if [ "$NOTARIZE" = true ]; then
    if ! xcrun notarytool --version > /dev/null 2>&1; then
        echo "ERROR: notarytool not found."
        echo "Install Xcode Command Line Tools: xcode-select --install"
        exit 1
    fi
    if ! xcrun notarytool history --keychain-profile "$NOTARY_PROFILE" > /dev/null 2>&1; then
        echo "ERROR: Notary credentials not found for profile '$NOTARY_PROFILE'."
        echo "Run: xcrun notarytool store-credentials \"$NOTARY_PROFILE\" --apple-id <APPLE_ID> --team-id <TEAM_ID> --password <APP_SPECIFIC_PASSWORD>"
        exit 1
    fi
    log "✓ Notary credentials found ($NOTARY_PROFILE)"
fi
log ""

# Check/rebuild binaries (per target)
if [ "${#TARGETS[@]}" -eq 1 ]; then
    log "Checking binary..."
else
    log "Checking binaries..."
fi

for i in "${!TARGETS[@]}"; do
    rid="${RUNTIME_IDS[$i]}"
    build_target="${BUILD_TARGETS[$i]}"
    binary="$ARTIFACTS_DIR/binaries/$rid/aeromux"

    if [ "$REBUILD" = true ]; then
        run_quiet "$PROJECT_ROOT/build.sh" --target "$build_target" --silent
        log "✓ Binary rebuilt: $rid"
    elif [ ! -f "$binary" ]; then
        echo "ERROR: Binary not found at: $binary"
        echo "Run './build.sh --target $build_target' first."
        exit 1
    elif [ -n "$(find "$binary" -mmin +60 2>/dev/null)" ]; then
        echo "ERROR: Binary is older than 1 hour: $binary"
        echo "Run './build.sh --target $build_target' to rebuild, or use --rebuild."
        exit 1
    else
        log "✓ Binary found: $rid"
    fi
done
log ""

# Read version (once)
VERSION=$(sed -n 's/.*<Version>\(.*\)<\/Version>.*/\1/p' "$PROPS_FILE")
log "Package version: $VERSION"
log ""

# Package each target
if [ "${#TARGETS[@]}" -eq 1 ]; then
    log "Preparing package..."
else
    log "Preparing packages..."
fi

PKG_FILENAMES=()
for i in "${!TARGETS[@]}"; do
    pkg_arch="${PKG_ARCHS[$i]}"
    rid="${RUNTIME_IDS[$i]}"

    pkg_filename=$(build_package "$pkg_arch" "$rid") || exit 1
    PKG_FILENAMES+=("$pkg_filename")

    log "  ✓ $pkg_filename packaged"
done
log ""

# Notarize packages (parallel when multiple targets)
if [ "$NOTARIZE" = true ]; then
    if [ "${#PKG_FILENAMES[@]}" -eq 1 ]; then
        log "Notarizing package (this may take up to 15 minutes)..."
    else
        log "Notarizing packages in parallel (this may take up to 15 minutes)..."
    fi

    NOTARY_PIDS=()
    NOTARY_LOGS=()
    for pkg in "${PKG_FILENAMES[@]}"; do
        notary_log=$(mktemp)
        NOTARY_LOGS+=("$notary_log")
        notarize_package "$pkg" > "$notary_log" 2>&1 &
        NOTARY_PIDS+=($!)
        log "  → Submitted: $pkg"
    done

    # Wait for all notarizations and check results
    NOTARY_FAILED=false
    for i in "${!NOTARY_PIDS[@]}"; do
        pid="${NOTARY_PIDS[$i]}"
        pkg="${PKG_FILENAMES[$i]}"
        notary_log="${NOTARY_LOGS[$i]}"

        if wait "$pid"; then
            log "  ✓ $pkg notarized and stapled"
        else
            NOTARY_FAILED=true
            echo "ERROR: Notarization failed for $pkg" >&2
            if [ -s "$notary_log" ]; then
                cat "$notary_log" >&2
            fi
        fi
        rm -f "$notary_log"
    done

    if [ "$NOTARY_FAILED" = true ]; then
        echo "" >&2
        echo "One or more packages failed notarization." >&2
        exit 1
    fi
    log ""
fi

# Summary
log "================================================"
log "PACKAGING SUMMARY"
log "================================================"
log ""

if [ "${#PKG_FILENAMES[@]}" -eq 1 ]; then
    local_arch="${PKG_ARCHS[0]}"
    local_pkg="${PKG_FILENAMES[0]}"
    local_path="$PACKAGES_DIR/$local_pkg"
    local_size=""
    if [ -f "$local_path" ]; then
        local_size=$(ls -lh "$local_path" | awk '{print $5}')
    fi

    log "Package created successfully!"
    log "Package:      $local_pkg"
    log "Architecture: $local_arch"
    log "Version:      $VERSION"
    if [ "$SIGN" = true ]; then
        log "Signed:       Yes"
    else
        log "Signed:       No"
    fi
    if [ "$NOTARIZE" = true ]; then
        log "Notarized:    Yes"
    else
        log "Notarized:    No"
    fi
    if [ -n "$local_size" ]; then
        log "Size:         $local_size"
    fi
    log "Output:       artifacts/packages/$local_pkg"
    log ""
    log "Install with: sudo installer -pkg artifacts/packages/$local_pkg -target /"
    log "         or: Double-click the .pkg file in Finder"
else
    log "All packages created successfully!"
    log "Version:      $VERSION"
    log ""
    log "Packages:"
    for pkg in "${PKG_FILENAMES[@]}"; do
        pkg_path="$PACKAGES_DIR/$pkg"
        if [ -f "$pkg_path" ]; then
            pkg_size=$(ls -lh "$pkg_path" | awk '{print $5}')
            log "  - artifacts/packages/$pkg ($pkg_size)"
        fi
    done
fi

log ""
