#!/bin/bash

# Aeromux Build Script
# This script builds the project and compiles a single self-contained binary
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

# Build step tracking
CURRENT_STEP=""
TERMINAL_WIDTH=$(stty size 2>/dev/null | awk '{print $2}')
: "${TERMINAL_WIDTH:=80}"

# Configuration
PROJECT_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
ARTIFACTS_DIR="$PROJECT_ROOT/artifacts"
CONFIGURATION="Release"

# shellcheck source=platform.sh
source "$PROJECT_ROOT/platform.sh"

# ── Functions ────────────────────────────────────────────────────────────────

usage() {
    echo "Usage: ./build.sh [OPTIONS]"
    echo ""
    echo "Options:"
    echo "  --target TARGET  Target platform and architecture (default: auto-detect)"
    echo "  --with-database  Download aeromux-db after building"
    echo "  --silent         Suppress all output (only errors are shown)"
    echo ""
    echo "Supported targets:"
    echo "  auto           - Auto-detect current platform and architecture (default)"
    echo "  linux          - Auto-detect Linux architecture (x64 or arm64)"
    echo "  macos          - Auto-detect macOS architecture (x64 or arm64)"
    echo "  linux-x64      - Linux x64 (explicit, for cross-compilation)"
    echo "  linux-arm64    - Linux ARM64 (Raspberry Pi 4/5, explicit)"
    echo "  macos-x64      - macOS Intel (explicit, for cross-compilation)"
    echo "  macos-arm64    - macOS Apple Silicon (explicit, for cross-compilation)"
    echo "  all            - Build all supported targets sequentially"
    echo ""
    echo "Examples:"
    echo "  ./build.sh                              # Auto-detect current platform"
    echo "  ./build.sh --target linux               # Auto-detect Linux architecture"
    echo "  ./build.sh --target macos               # Auto-detect macOS architecture"
    echo "  ./build.sh --target linux-arm64         # Build for Raspberry Pi (cross-compile)"
    echo "  ./build.sh --target all                 # Build all platforms"
    echo "  ./build.sh --with-database              # Build and download database"
    echo "  ./build.sh --silent                     # Auto-detect, no output"
    echo "  ./build.sh --silent --target linux-arm64  # Both flags"
    exit 1
}

# Logging helper (suppressed in silent mode)
log() { [ "$SILENT" = true ] || echo "$@"; }

# Run a command quietly — suppress output on success, show on failure
run_quiet() {
    local output
    output=$("$@" 2>&1) || {
        [ -z "$CURRENT_STEP" ] || [ "$SILENT" = true ] || echo "✗ $CURRENT_STEP failed"
        echo ""
        echo "================================================"
        echo "BUILD FAILED"
        echo "================================================"
        if [ -n "$output" ]; then
            echo ""
            echo "An error occurred during the build. See the log below for details."
            echo "Paths relative to: $PROJECT_ROOT/"
            echo ""
            echo "$output" | sed "s|$PROJECT_ROOT/|./|g" | sed 's/^[[:space:]]*//' | fold -s -w $((TERMINAL_WIDTH - 3)) | sed 's/^/ | /'
        fi
        echo ""
        exit 1
    }
}

# Resolve a target name to a .NET runtime identifier (echoes the result)
resolve_runtime_id() {
    local target="$1"
    case "$target" in
        auto)
            detect_runtime_id
            ;;
        linux)
            if [ "$(detect_os)" != "linux" ]; then
                echo "ERROR: Cannot auto-detect Linux architecture on non-Linux system" >&2
                echo "Please specify explicit target: ./build.sh --target linux-x64 or ./build.sh --target linux-arm64" >&2
                exit 1
            fi
            echo "linux-$(detect_arch)"
            ;;
        macos)
            if [ "$(detect_os)" != "osx" ]; then
                echo "ERROR: Cannot auto-detect macOS architecture on non-macOS system" >&2
                echo "Please specify explicit target: ./build.sh --target macos-x64 or ./build.sh --target macos-arm64" >&2
                exit 1
            fi
            echo "osx-$(detect_arch)"
            ;;
        linux-x64)    echo "linux-x64" ;;
        linux-arm64)  echo "linux-arm64" ;;
        macos-x64)    echo "osx-x64" ;;
        macos-arm64)  echo "osx-arm64" ;;
        *)
            echo "ERROR: Unknown target: $target" >&2
            echo "" >&2
            usage
            ;;
    esac
}

# ── Argument parsing ─────────────────────────────────────────────────────────

SILENT=false
TARGET="auto"
WITH_DATABASE=false

while [[ $# -gt 0 ]]; do
    case "$1" in
        --silent)
            SILENT=true
            shift
            ;;
        --with-database)
            WITH_DATABASE=true
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

# Resolve targets into an array of runtime IDs
if [ "$TARGET" = "all" ]; then
    RUNTIME_IDS=("${SUPPORTED_RUNTIME_IDS[@]}")
else
    RUNTIME_IDS=("$(resolve_runtime_id "$TARGET")")
fi

# Clear screen and print header
[ "$SILENT" = true ] || clear

log "================================================"
log "Aeromux Build Script"
log "================================================"
log ""

if [ "${#RUNTIME_IDS[@]}" -gt 1 ]; then
    log "Building all targets..."
    ARCH_PREVIEW="${RUNTIME_IDS[0]}"
    for rid in "${RUNTIME_IDS[@]:1}"; do
        ARCH_PREVIEW="$ARCH_PREVIEW, $rid"
    done
    log "✓ Target architectures: $ARCH_PREVIEW"
else
    log "Determining target architecture..."
    log "✓ Target architecture: ${RUNTIME_IDS[0]}"
fi
log ""

# Clean artifact directories for targets being built
log "Cleaning artifacts directory..."
for rid in "${RUNTIME_IDS[@]}"; do
    BINARIES_DIR="$ARTIFACTS_DIR/binaries/$rid"
    if [ -d "$BINARIES_DIR" ]; then
        rm -rf "$BINARIES_DIR"
    fi
    mkdir -p "$BINARIES_DIR"
done
touch "$ARTIFACTS_DIR/.gitkeep"
log "✓ Artifacts directory cleaned"
log ""

# Restore dependencies (once for all targets)
CURRENT_STEP="Dependency restore"
log "Restoring dependencies..."
run_quiet dotnet restore "$PROJECT_ROOT/Aeromux.sln"
log "✓ Dependencies restored"
log ""

# Build each target
if [ "${#RUNTIME_IDS[@]}" -eq 1 ]; then
    log "Publishing self-contained executable..."
else
    log "Publishing self-contained executables..."
fi

for rid in "${RUNTIME_IDS[@]}"; do
    BINARIES_DIR="$ARTIFACTS_DIR/binaries/$rid"

    CURRENT_STEP="Publishing ($rid)"
    run_quiet dotnet publish "$PROJECT_ROOT/src/Aeromux.CLI/Aeromux.CLI.csproj" \
        --configuration "$CONFIGURATION" \
        --runtime "$rid" \
        --self-contained true \
        --output "$BINARIES_DIR" \
        -p:PublishSingleFile=true \
        -p:IncludeNativeLibrariesForSelfExtract=true \
        -p:EnableCompressionInSingleFile=true \
        -p:DebugType=embedded

    if [ -f "$BINARIES_DIR/Aeromux.CLI" ]; then
        mv "$BINARIES_DIR/Aeromux.CLI" "$BINARIES_DIR/aeromux"
    fi

    log "✓ Executable for $rid is published"
done
log ""

log "Finalizing..."
log "✓ Build finalized"
log ""

# Download database (optional, once)
if [ "$WITH_DATABASE" = true ]; then
    DB_DIR="$ARTIFACTS_DIR/db"
    mkdir -p "$DB_DIR"
    CURRENT_STEP="Database download"
    log "Downloading aeromux-db..."

    # Find a binary that can run on the current host
    HOST_RID=$(detect_runtime_id)
    DB_BINARY="$ARTIFACTS_DIR/binaries/$HOST_RID/aeromux"

    if [ -f "$DB_BINARY" ]; then
        run_quiet "$DB_BINARY" database update --database "$DB_DIR"
        log "✓ Database downloaded"
    else
        log "✗ Cannot download database: no binary for current platform ($HOST_RID)"
        log "  Build for this platform first, then run: ./artifacts/binaries/$HOST_RID/aeromux database update --database $DB_DIR"
    fi
    log ""
fi

# Summary — verify at least one binary exists
BUILT_COUNT=0
for rid in "${RUNTIME_IDS[@]}"; do
    [ -f "$ARTIFACTS_DIR/binaries/$rid/aeromux" ] && BUILT_COUNT=$((BUILT_COUNT + 1))
done

if [ "$BUILT_COUNT" -eq 0 ]; then
    echo ""
    echo "================================================"
    echo "BUILD FAILED"
    echo "================================================"
    echo ""
    echo "Binary not found after a successful build."
    echo ""
    exit 1
fi

TOTAL_FILES=0
for rid in "${RUNTIME_IDS[@]}"; do
    count=$(find "$ARTIFACTS_DIR/binaries/$rid" -type f | wc -l | tr -d ' ')
    TOTAL_FILES=$((TOTAL_FILES + count))
done
ARCH_LIST="${RUNTIME_IDS[0]}"
for rid in "${RUNTIME_IDS[@]:1}"; do
    ARCH_LIST="$ARCH_LIST, $rid"
done

log "================================================"
log "BUILD SUMMARY"
log "================================================"
log ""
log "Build completed successfully!"
log "Architecture: $ARCH_LIST"
log "Total files created: $TOTAL_FILES"
log "All artifacts are in: $ARTIFACTS_DIR"
log ""

if [ "${#RUNTIME_IDS[@]}" -eq 1 ]; then
    log "Executable:"
else
    log "Executables:"
fi
for rid in "${RUNTIME_IDS[@]}"; do
    if [ -f "$ARTIFACTS_DIR/binaries/$rid/aeromux" ]; then
        FILESIZE=$(ls -lh "$ARTIFACTS_DIR/binaries/$rid/aeromux" | awk '{print $5}')
        log "  - binaries/$rid/aeromux ($FILESIZE)"
    fi
done

if [ "$WITH_DATABASE" = true ] && [ -d "$ARTIFACTS_DIR/db" ]; then
    log ""
    log "Database:"
    for dbfile in "$ARTIFACTS_DIR/db/"*; do
        if [ -f "$dbfile" ]; then
            DBFILESIZE=$(ls -lh "$dbfile" | awk '{print $5}')
            log "  - db/$(basename "$dbfile") ($DBFILESIZE)"
        fi
    done
fi

log ""
log "Run on the target machine:"
for rid in "${RUNTIME_IDS[@]}"; do
    log "  - $rid: ./artifacts/binaries/$rid/aeromux"
done
log ""
