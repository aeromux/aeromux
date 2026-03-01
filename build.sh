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

# Parse named parameters
SILENT=false
TARGET="auto"

while [[ $# -gt 0 ]]; do
    case "$1" in
        --silent)
            SILENT=true
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
            echo "Usage: ./build.sh [OPTIONS]"
            echo ""
            echo "Options:"
            echo "  --target TARGET  Target platform and architecture (default: auto-detect)"
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
            echo ""
            echo "Examples:"
            echo "  ./build.sh                              # Auto-detect current platform"
            echo "  ./build.sh --target linux               # Auto-detect Linux architecture"
            echo "  ./build.sh --target macos               # Auto-detect macOS architecture"
            echo "  ./build.sh --target linux-arm64         # Build for Raspberry Pi (cross-compile)"
            echo "  ./build.sh --silent                     # Auto-detect, no output"
            echo "  ./build.sh --silent --target linux-arm64  # Both flags"
            exit 1
            ;;
    esac
done

# Logging helper (suppressed in silent mode)
log() { [ "$SILENT" = true ] || echo "$@"; }

# Clear screen (suppressed in silent mode)
[ "$SILENT" = true ] || clear

# Determine runtime identifier
case "$TARGET" in
    auto)
        # Auto-detect based on current platform
        if [[ "$OSTYPE" == "darwin"* ]]; then
            ARCH=$(uname -m)
            if [[ "$ARCH" == "arm64" ]]; then
                RUNTIME_ID="osx-arm64"
            else
                RUNTIME_ID="osx-x64"
            fi
        elif [[ "$OSTYPE" == "linux-gnu"* ]]; then
            ARCH=$(uname -m)
            if [[ "$ARCH" == "aarch64" ]]; then
                RUNTIME_ID="linux-arm64"
            else
                RUNTIME_ID="linux-x64"
            fi
        else
            echo "ERROR: Unsupported platform: $OSTYPE"
            echo "Please specify target explicitly: ./build.sh --target [linux|macos|linux-x64|linux-arm64|macos-x64|macos-arm64]"
            exit 1
        fi
        ;;
    linux)
        # Auto-detect Linux architecture if on Linux, otherwise error
        if [[ "$OSTYPE" == "linux-gnu"* ]]; then
            ARCH=$(uname -m)
            if [[ "$ARCH" == "aarch64" ]]; then
                RUNTIME_ID="linux-arm64"
            else
                RUNTIME_ID="linux-x64"
            fi
        else
            echo "ERROR: Cannot auto-detect Linux architecture on non-Linux system"
            echo "Please specify explicit target: ./build.sh --target linux-x64 or ./build.sh --target linux-arm64"
            exit 1
        fi
        ;;
    macos)
        # Auto-detect macOS architecture if on macOS, otherwise error
        if [[ "$OSTYPE" == "darwin"* ]]; then
            ARCH=$(uname -m)
            if [[ "$ARCH" == "arm64" ]]; then
                RUNTIME_ID="osx-arm64"
            else
                RUNTIME_ID="osx-x64"
            fi
        else
            echo "ERROR: Cannot auto-detect macOS architecture on non-macOS system"
            echo "Please specify explicit target: ./build.sh --target macos-x64 or ./build.sh --target macos-arm64"
            exit 1
        fi
        ;;
    linux-x64)
        RUNTIME_ID="linux-x64"
        ;;
    linux-arm64)
        RUNTIME_ID="linux-arm64"
        ;;
    macos-x64)
        RUNTIME_ID="osx-x64"
        ;;
    macos-arm64)
        RUNTIME_ID="osx-arm64"
        ;;
    *)
        echo "ERROR: Unknown target: $TARGET"
        echo ""
        echo "Usage: ./build.sh [OPTIONS]"
        echo ""
        echo "Options:"
        echo "  --target TARGET  Target platform and architecture (default: auto-detect)"
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
        echo ""
        echo "Examples:"
        echo "  ./build.sh                              # Auto-detect current platform"
        echo "  ./build.sh --target linux               # Auto-detect Linux architecture"
        echo "  ./build.sh --target macos               # Auto-detect macOS architecture"
        echo "  ./build.sh --target linux-arm64         # Build for Raspberry Pi (cross-compile)"
        echo "  ./build.sh --silent                     # Auto-detect, no output"
        echo "  ./build.sh --silent --target linux-arm64  # Both flags"
        exit 1
        ;;
esac

# Set binaries directory with architecture subdirectory
BINARIES_DIR="$ARTIFACTS_DIR/binaries/$RUNTIME_ID"

log "================================================"
log "Aeromux Build Script"
log "================================================"
log ""

# Show target determination
log "Determining target architecture..."
log "✓ Target architecture: $RUNTIME_ID"
log ""

# Clean artifacts directory
log "Cleaning artifacts directory..."
if [ -d "$ARTIFACTS_DIR" ]; then
    rm -rf "$ARTIFACTS_DIR"
fi
mkdir -p "$BINARIES_DIR"
touch "$ARTIFACTS_DIR/.gitkeep"
log "✓ Artifacts directory cleaned"
log ""

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

# Step 1: Restore dependencies
CURRENT_STEP="Dependency restore"
log "Restoring dependencies..."
run_quiet dotnet restore "$PROJECT_ROOT/Aeromux.sln"
log "✓ Dependencies restored"
log ""

# Step 2: Publish single-file self-contained executable
CURRENT_STEP="Publishing"
log "Publishing self-contained executable..."
run_quiet dotnet publish "$PROJECT_ROOT/src/Aeromux.CLI/Aeromux.CLI.csproj" \
    --configuration "$CONFIGURATION" \
    --runtime "$RUNTIME_ID" \
    --self-contained true \
    --output "$BINARIES_DIR" \
    -p:PublishSingleFile=true \
    -p:IncludeNativeLibrariesForSelfExtract=true \
    -p:EnableCompressionInSingleFile=true \
    -p:DebugType=embedded
log "✓ Executable published"
log ""

# Step 3: Rename the output file to 'aeromux'
log "Finalizing..."
if [ -f "$BINARIES_DIR/Aeromux.CLI" ]; then
    mv "$BINARIES_DIR/Aeromux.CLI" "$BINARIES_DIR/aeromux"
fi
log "✓ Build finalized"
log ""

# Step 4: Summary
log "================================================"
log "BUILD SUMMARY"
log "================================================"
log ""

if [ -f "$BINARIES_DIR/aeromux" ]; then
    FILESIZE=$(ls -lh "$BINARIES_DIR/aeromux" | awk '{print $5}')
    TOTAL_FILES=$(find "$ARTIFACTS_DIR" -type f | wc -l | tr -d ' ')

    log "Build completed successfully!"
    log "Architecture: $RUNTIME_ID"
    log "Total files created: $TOTAL_FILES"
    log "All artifacts are in: $ARTIFACTS_DIR"
    log ""
    log "Executable:"
    log "  - binaries/$RUNTIME_ID/aeromux ($FILESIZE)"
    log ""
    log "Run with: ./artifacts/binaries/$RUNTIME_ID/aeromux"
else
    echo ""
    echo "================================================"
    echo "BUILD FAILED"
    echo "================================================"
    echo ""
    echo "Binary not found after a successful build."
    echo ""
    exit 1
fi
log ""
