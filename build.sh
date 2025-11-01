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
BINARIES_DIR="$ARTIFACTS_DIR/binaries"
CONFIGURATION="Release"

# Detect runtime identifier based on platform
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
    echo "Unsupported platform: $OSTYPE"
    exit 1
fi

echo "================================================"
echo "Aeromux Build Script"
echo "================================================"
echo ""

# Detect platform
echo "Detecting platform..."
echo "✓ Platform detected: $RUNTIME_ID"
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
    echo "Total files created: $TOTAL_FILES"
    echo "All artifacts are in: $ARTIFACTS_DIR"
    echo ""
    echo "Executable:"
    echo "  - binaries/aeromux ($FILESIZE)"
    echo ""
    echo "Run with: ./artifacts/binaries/aeromux"
else
    echo "ERROR: Binary not found!"
    exit 1
fi
echo ""
