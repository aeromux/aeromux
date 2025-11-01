#!/bin/bash

# Aeromux Build Script
# This script builds the project and compiles a single self-contained binary
#
# Copyright (C) 2025 Nandor Toth <dev@nandortoth.com>
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
clear

# Configuration
PROJECT_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
ARTIFACTS_DIR="$PROJECT_ROOT/artifacts"
CONFIGURATION="Release"

# Parse target architecture parameter
TARGET="${1:-auto}"

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
            echo "Please specify target explicitly: ./build.sh [linux|macos|linux-x64|linux-arm64|macos-x64|macos-arm64]"
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
            echo "Please specify explicit target: ./build.sh linux-x64 or ./build.sh linux-arm64"
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
            echo "Please specify explicit target: ./build.sh macos-x64 or ./build.sh macos-arm64"
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
        echo "Usage: ./build.sh [TARGET]"
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
        echo "  ./build.sh                  # Auto-detect current platform"
        echo "  ./build.sh linux            # Auto-detect Linux architecture"
        echo "  ./build.sh macos            # Auto-detect macOS architecture"
        echo "  ./build.sh linux-arm64      # Build for Raspberry Pi (cross-compile)"
        exit 1
        ;;
esac

# Set binaries directory with architecture subdirectory
BINARIES_DIR="$ARTIFACTS_DIR/binaries/$RUNTIME_ID"

echo "================================================"
echo "Aeromux Build Script"
echo "================================================"
echo ""

# Show target determination
echo "Determining target architecture..."
echo "✓ Target architecture: $RUNTIME_ID"
echo ""

# Clean artifacts directory
echo "Cleaning artifacts directory..."
if [ -d "$ARTIFACTS_DIR" ]; then
    rm -rf "$ARTIFACTS_DIR"
fi
mkdir -p "$BINARIES_DIR"
echo "✓ Artifacts directory cleaned"
echo ""

# Step 1: Restore dependencies
echo "Restoring dependencies..."
dotnet restore "$PROJECT_ROOT/Aeromux.sln" > /dev/null 2>&1
echo "✓ Dependencies restored"
echo ""

# Step 2: Publish single-file self-contained executable
echo "Publishing self-contained executable..."
dotnet publish "$PROJECT_ROOT/src/Aeromux.CLI/Aeromux.CLI.csproj" \
    --configuration "$CONFIGURATION" \
    --runtime "$RUNTIME_ID" \
    --self-contained true \
    --output "$BINARIES_DIR" \
    -p:PublishSingleFile=true \
    -p:IncludeNativeLibrariesForSelfExtract=true \
    -p:EnableCompressionInSingleFile=true \
    -p:DebugType=embedded \
    > /dev/null 2>&1
echo "✓ Executable published"
echo ""

# Step 3: Rename the output file to 'aeromux'
echo "Finalizing..."
if [ -f "$BINARIES_DIR/Aeromux.CLI" ]; then
    mv "$BINARIES_DIR/Aeromux.CLI" "$BINARIES_DIR/aeromux"
fi
echo "✓ Build finalized"
echo ""

# Step 3: Summary
echo "================================================"
echo "BUILD SUMMARY"
echo "================================================"
echo ""

if [ -f "$BINARIES_DIR/aeromux" ]; then
    FILESIZE=$(ls -lh "$BINARIES_DIR/aeromux" | awk '{print $5}')
    TOTAL_FILES=$(find "$ARTIFACTS_DIR" -type f | wc -l | tr -d ' ')

    echo "Build completed successfully!"
    echo "Architecture: $RUNTIME_ID"
    echo "Total files created: $TOTAL_FILES"
    echo "All artifacts are in: $ARTIFACTS_DIR"
    echo ""
    echo "Executable:"
    echo "  - binaries/$RUNTIME_ID/aeromux ($FILESIZE)"
    echo ""
    echo "Run with: ./artifacts/binaries/$RUNTIME_ID/aeromux"
else
    echo "ERROR: Binary not found!"
    exit 1
fi
echo ""
