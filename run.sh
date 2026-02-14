#!/bin/bash

# Aeromux Run Script
# This script builds and runs aeromux with an interactive mode selector
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

# Configuration
PROJECT_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
ARTIFACTS_DIR="$PROJECT_ROOT/artifacts"

# Parse --no-build flag (must be first argument if present)
NO_BUILD=false
if [[ "$1" == "--no-build" ]]; then
    NO_BUILD=true
    shift
fi

# Detect platform to determine binary path
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
    exit 1
fi

BINARY="$ARTIFACTS_DIR/binaries/$RUNTIME_ID/aeromux"

clear

echo "================================================"
echo "Aeromux Run Script"
echo "================================================"
echo ""

# Build step
if [ "$NO_BUILD" = true ]; then
    echo "Skipping build (--no-build)"
else
    echo "Building Aeromux..."
    if ! "$PROJECT_ROOT/build.sh" --silent; then
        exit 1
    fi
    echo "✓ Build complete"
fi

# Verify binary exists
if [ ! -f "$BINARY" ]; then
    echo "ERROR: Binary not found at $BINARY"
    echo "Run ./build.sh first or remove --no-build flag"
    exit 1
fi
echo "✓ Binary found: artifacts/binaries/$RUNTIME_ID/aeromux"
echo ""

# If arguments were provided, use pass-through mode
if [[ $# -gt 0 ]]; then
    echo "================================================"
    echo "RUN SUMMARY"
    echo "================================================"
    echo ""
    echo "Binary: artifacts/binaries/$RUNTIME_ID/aeromux"
    echo "Command: ./artifacts/binaries/$RUNTIME_ID/aeromux $*"
    echo ""
    echo "Starting aeromux..."
    echo ""
    exec "$BINARY" "$@"
fi

# Interactive mode
echo "Select a command:"
echo "  1) daemon             Start as background service"
echo "  2) live --standalone  Live TUI with direct RTL-SDR access"
echo "  3) live --connect     Live TUI connecting to Beast source"
echo "  4) version            Display version information"
echo ""
read -rp "Enter selection (1-4): " COMMAND_CHOICE
echo ""

case "$COMMAND_CHOICE" in
    1) CMD_ARGS=("daemon") ;;
    2) CMD_ARGS=("live" "--standalone") ;;
    3) CMD_ARGS=("live" "--connect") ;;
    4) CMD_ARGS=("version" "--details") ;;
    *)
        echo "ERROR: Invalid selection"
        exit 1
        ;;
esac

# For live --connect, prompt for address (must come before config so argument order is correct)
if [[ "$COMMAND_CHOICE" == "3" ]]; then
    read -rp "Enter Beast source address (host:port): " CONNECT_ADDRESS
    echo ""

    if [[ -z "$CONNECT_ADDRESS" ]]; then
        echo "ERROR: Address is required for live --connect mode"
        exit 1
    fi

    CMD_ARGS+=("$CONNECT_ADDRESS")
fi

# Config selection (skip for version)
if [[ "$COMMAND_CHOICE" != "4" ]]; then
    echo "Select configuration:"
    echo "  1) Single device      aeromux.test-singledevice.yaml"
    echo "  2) Multi-device       aeromux.test-multidevice.yaml"
    echo "  3) No config          Run without a configuration file"
    echo ""
    read -rp "Enter selection (1-3): " CONFIG_CHOICE
    echo ""

    case "$CONFIG_CHOICE" in
        1) CMD_ARGS+=("--config" "aeromux.test-singledevice.yaml") ;;
        2) CMD_ARGS+=("--config" "aeromux.test-multidevice.yaml") ;;
        3) ;;
        *)
            echo "ERROR: Invalid selection"
            exit 1
            ;;
    esac
fi

# Build the display command string
DISPLAY_CMD="./artifacts/binaries/$RUNTIME_ID/aeromux"
for arg in "${CMD_ARGS[@]}"; do
    DISPLAY_CMD+=" $arg"
done

# Show run summary and confirm
echo "================================================"
echo "RUN SUMMARY"
echo "================================================"
echo ""
echo "Binary: artifacts/binaries/$RUNTIME_ID/aeromux"
echo "Command: $DISPLAY_CMD"
echo ""

# Wait for Enter or cancel
read -rsn1 -p "Press Enter to start aeromux (any other key to cancel)... " KEY
echo ""

if [[ -n "$KEY" ]]; then
    echo ""
    echo "Cancelled."
    exit 0
fi

echo ""
echo "Starting aeromux..."
echo ""
exec "$BINARY" "${CMD_ARGS[@]}"
