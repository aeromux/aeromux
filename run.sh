#!/bin/bash

# Aeromux Run Script
# This script builds and runs aeromux with an interactive mode selector
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

# Prompts for a Beast source address and appends --beast-source to CMD_ARGS.
# Defaults to localhost:30005 if the user presses Enter without typing an address.
prompt_beast_source() {
    read -rp "Enter Beast source address (host:port, default: localhost:30005): " BEAST_ADDRESS
    echo ""

    if [[ -z "$BEAST_ADDRESS" ]]; then
        BEAST_ADDRESS="localhost:30005"
    fi

    CMD_ARGS+=("--beast-source" "$BEAST_ADDRESS")
}

# Prompts for input source selection (SDR, Beast, or both) and populates CMD_ARGS.
# Used by both daemon and live commands.
prompt_input_source() {
    echo "Select input source:"
    echo "  1) SDR only            Use RTL-SDR device(s) from config"
    echo "  2) Beast only          Connect to Beast TCP source"
    echo "  3) SDR + Beast         Use both SDR and Beast sources"
    echo ""
    read -rp "Enter selection (1-3): " SOURCE_CHOICE
    echo ""

    case "$SOURCE_CHOICE" in
        1) ;; # SDR is the default when no Beast flags are present
        2) prompt_beast_source ;;
        3)
            CMD_ARGS+=("--sdr-source")
            prompt_beast_source
            ;;
        *)
            echo "ERROR: Invalid selection"
            exit 1
            ;;
    esac
}

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
echo "  1) daemon              Start as background service"
echo "  2) live                Live aircraft display (TUI)"
echo "  3) database            Manage aircraft metadata database"
echo "  4) device              List RTL-SDR devices on the system"
echo "  5) version             Display version information"
echo ""
read -rp "Enter selection (1-5): " COMMAND_CHOICE
echo ""

case "$COMMAND_CHOICE" in
    1) CMD_ARGS=("daemon") ;;
    2) CMD_ARGS=("live") ;;
    3) CMD_ARGS=("database") ;;
    4) CMD_ARGS=("device") ;;
    5) CMD_ARGS=("version" "--verbose") ;;
    *)
        echo "ERROR: Invalid selection"
        exit 1
        ;;
esac

# Input source sub-menu for daemon and live commands
if [[ "$COMMAND_CHOICE" == "1" || "$COMMAND_CHOICE" == "2" ]]; then
    prompt_input_source
fi

# Database sub-menu: select action and database path
if [[ "$COMMAND_CHOICE" == "3" ]]; then
    echo "Select action:"
    echo "  1) update    Download or update the database"
    echo "  2) info      Show installed database details"
    echo ""
    read -rp "Enter selection (1-2): " DB_ACTION_CHOICE
    echo ""

    case "$DB_ACTION_CHOICE" in
        1) CMD_ARGS+=("update") ;;
        2) CMD_ARGS+=("info") ;;
        *)
            echo "ERROR: Invalid selection"
            exit 1
            ;;
    esac

    read -rp "Enter database directory path (e.g., artifacts/db/): " DB_PATH
    echo ""

    if [[ -z "$DB_PATH" ]]; then
        echo "ERROR: Database path is required"
        exit 1
    fi

    CMD_ARGS+=("--database" "$DB_PATH")
fi

# Device sub-menu: select verbose mode
if [[ "$COMMAND_CHOICE" == "4" ]]; then
    echo "Show detailed tuner parameters?"
    echo "  1) No       Basic device list"
    echo "  2) Yes      Detailed tuner parameters (opens each device)"
    echo ""
    read -rp "Enter selection (1-2): " DEVICE_VERBOSE_CHOICE
    echo ""

    case "$DEVICE_VERBOSE_CHOICE" in
        1) ;;
        2) CMD_ARGS+=("--verbose") ;;
        *)
            echo "ERROR: Invalid selection"
            exit 1
            ;;
    esac
fi

# Config selection (skip for device and version)
if [[ "$COMMAND_CHOICE" != "4" && "$COMMAND_CHOICE" != "5" ]]; then
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
