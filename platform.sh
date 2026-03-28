#!/bin/bash

# Aeromux Platform Detection
# Shared platform detection functions and supported runtime identifiers.
# Source this file from other scripts — it has no side effects.
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

# All supported .NET runtime identifiers (single source of truth)
SUPPORTED_RUNTIME_IDS=(linux-arm64 linux-x64 osx-arm64 osx-x64)

# Detect the host OS as a .NET runtime prefix: "osx" or "linux"
detect_os() {
    case "$OSTYPE" in
        darwin*)  echo "osx" ;;
        linux*)   echo "linux" ;;
        *)
            echo "ERROR: Unsupported platform: $OSTYPE" >&2
            exit 1
            ;;
    esac
}

# Detect the host architecture as a .NET runtime suffix: "arm64" or "x64"
detect_arch() {
    case "$(uname -m)" in
        x86_64|amd64)  echo "x64" ;;
        arm64|aarch64) echo "arm64" ;;
        *)
            echo "ERROR: Unsupported architecture: $(uname -m)" >&2
            exit 1
            ;;
    esac
}

# Detect the full .NET runtime identifier for the current host
detect_runtime_id() {
    echo "$(detect_os)-$(detect_arch)"
}
