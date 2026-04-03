#!/bin/bash

# Aeromux Docker Image Build Script
# This script builds multi-arch Docker images and optionally pushes to GHCR
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
ARTIFACTS_DIR="$PROJECT_ROOT/artifacts"
DOCKER_OUTPUT_DIR="$ARTIFACTS_DIR/docker"
PROPS_FILE="$PROJECT_ROOT/src/Directory.Build.props"
EXAMPLE_CONFIG="$PROJECT_ROOT/aeromux.example.yaml"
IMAGE_NAME="ghcr.io/aeromux/aeromux"

# Fixed targets — always build both architectures
TARGETS=(linux-arm64 linux-x64)
DOCKER_ARCHS=(arm64 amd64)
DOCKER_PLATFORMS=("linux/arm64" "linux/amd64")

# ── Functions ────────────────────────────────────────────────────────────────

usage() {
    echo "Usage: ./docker/build-image.sh [OPTIONS]"
    echo ""
    echo "Options:"
    echo "  --push             Push to GHCR after building (requires authentication)"
    echo "  --rebuild          Force rebuild even if binaries exist and are recent"
    echo "  --silent           Suppress all output (only errors are shown)"
    echo ""
    echo "The script always builds both architectures (linux/arm64 and linux/amd64)."
    echo ""
    echo "Examples:"
    echo "  ./docker/build-image.sh"
    echo "  ./docker/build-image.sh --push"
    echo "  ./docker/build-image.sh --push --rebuild"
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
        echo "DOCKER BUILD FAILED" >&2
        echo "================================================" >&2
        if [ -n "$output" ]; then
            echo "" >&2
            echo "$output" >&2
        fi
        echo "" >&2
        exit 1
    }
}

# ── Argument parsing ─────────────────────────────────────────────────────────

SILENT=false
PUSH=false
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
        --push)
            PUSH=true
            shift
            ;;
        *)
            echo "ERROR: Unknown option: $1"
            echo ""
            usage
            ;;
    esac
done

# ── Main ─────────────────────────────────────────────────────────────────────

# Clear screen and print header
[ "$SILENT" = true ] || clear

log "================================================"
log "Aeromux Docker Image Build"
log "================================================"
log ""
log "Building all targets..."
log "✓ Target architectures: linux-arm64, linux-x64"
log ""

# Check build tools
log "Checking build tools..."
if ! command -v docker > /dev/null 2>&1; then
    echo ""
    echo "ERROR: docker not found."
    echo "Install Docker: https://docs.docker.com/get-docker/"
    exit 1
fi
log "✓ docker found"

if ! docker buildx version > /dev/null 2>&1; then
    echo ""
    echo "ERROR: docker buildx not found."
    echo "Install Docker Buildx: https://docs.docker.com/build/install-buildx/"
    exit 1
fi
log "✓ docker buildx found"

# Ensure a buildx builder with docker-container driver exists (required for --output and multi-arch)
BUILDER_NAME="aeromux-builder"
if ! docker buildx inspect "$BUILDER_NAME" > /dev/null 2>&1; then
    log ""
    log "Creating buildx builder ($BUILDER_NAME)..."
    run_quiet docker buildx create --name "$BUILDER_NAME" --driver docker-container --bootstrap
fi
log "✓ buildx builder available ($BUILDER_NAME)"

# Check GHCR authentication if --push
if [ "$PUSH" = true ]; then
    if ! grep -q "ghcr.io" ~/.docker/config.json 2>/dev/null; then
        echo ""
        echo "ERROR: Not authenticated to ghcr.io."
        echo "Run: echo \$GITHUB_TOKEN | docker login ghcr.io -u USERNAME --password-stdin"
        exit 1
    fi
    log "✓ GHCR authentication found"
fi
log ""

# Check/rebuild binaries
log "Checking binaries..."
for i in "${!TARGETS[@]}"; do
    target="${TARGETS[$i]}"
    binary="$ARTIFACTS_DIR/binaries/$target/aeromux"

    if [ "$REBUILD" = true ]; then
        run_quiet "$PROJECT_ROOT/build.sh" --target "$target" --silent
        log "✓ Binary rebuilt: $target"
    elif [ ! -f "$binary" ]; then
        echo ""
        echo "ERROR: Binary not found at: $binary"
        echo "Run './build.sh --target $target' first."
        exit 1
    elif [ -n "$(find "$binary" -mmin +60 2>/dev/null)" ]; then
        echo ""
        echo "ERROR: Binary is older than 1 hour: $binary"
        echo "Run './build.sh --target $target' to rebuild, or use --rebuild."
        exit 1
    else
        log "✓ Binary found: $target"
    fi
done
log ""

# Read version
VERSION=$(sed -n 's/.*<Version>\(.*\)<\/Version>.*/\1/p' "$PROPS_FILE")
log "Image version: $VERSION"
log ""

# Prepare build context
log "Preparing build context..."

BUILD_CONTEXT=$(mktemp -d)
trap 'rm -rf "$BUILD_CONTEXT"' EXIT

# Copy Dockerfile and entrypoint
cp "$SCRIPT_DIR/Dockerfile" "$BUILD_CONTEXT/"
cp "$SCRIPT_DIR/entrypoint.sh" "$BUILD_CONTEXT/"

# Prepare architecture-specific binary directories
mkdir -p "$BUILD_CONTEXT/binaries/arm64"
mkdir -p "$BUILD_CONTEXT/binaries/amd64"
cp "$ARTIFACTS_DIR/binaries/linux-arm64/aeromux" "$BUILD_CONTEXT/binaries/arm64/aeromux"
cp "$ARTIFACTS_DIR/binaries/linux-x64/aeromux" "$BUILD_CONTEXT/binaries/amd64/aeromux"

# Generate Docker config from aeromux.example.yaml
sed -e 's|level: debug                            |level: information                      |' \
    -e 's|enabled: false                        # Enable for|enabled: true                         # Enable for|' \
    -e 's|path: "logs/aeromux-.log"             |path: "/var/log/aeromux/aeromux-.log" |' \
    -e 's|enabled: false                          # Enable database|enabled: true                           # Enable database|' \
    -e 's|path: "artifacts/db/"                   |path: "/var/lib/aeromux/"               |' \
    "$EXAMPLE_CONFIG" > "$BUILD_CONTEXT/aeromux.yaml"

log "✓ Build context prepared"
log ""

# Build and save images
log "Building images..."
mkdir -p "$DOCKER_OUTPUT_DIR"

TAR_FILENAMES=()
for i in "${!DOCKER_ARCHS[@]}"; do
    arch="${DOCKER_ARCHS[$i]}"
    platform="${DOCKER_PLATFORMS[$i]}"
    tar_filename="aeromux_${VERSION}_linux_${arch}.tar"
    tar_path="$DOCKER_OUTPUT_DIR/$tar_filename"

    run_quiet docker buildx build \
        --builder "$BUILDER_NAME" \
        --platform "$platform" \
        --build-arg VERSION="$VERSION" \
        --output "type=docker,dest=$tar_path" \
        --tag "${IMAGE_NAME}:${VERSION}" \
        "$BUILD_CONTEXT"

    TAR_FILENAMES+=("$tar_filename")
    log "✓ $tar_filename built"
done
log ""

# Push to GHCR (optional)
PUSHED=false
if [ "$PUSH" = true ]; then
    log "Pushing multi-arch image to GHCR..."

    run_quiet docker buildx build \
        --builder "$BUILDER_NAME" \
        --platform "linux/arm64,linux/amd64" \
        --build-arg VERSION="$VERSION" \
        --tag "${IMAGE_NAME}:${VERSION}" \
        --tag "${IMAGE_NAME}:latest" \
        --annotation "index:org.opencontainers.image.title=Aeromux" \
        --annotation "index:org.opencontainers.image.description=Multi-SDR Mode S and ADS-B demodulator and decoder" \
        --annotation "index:org.opencontainers.image.version=${VERSION}" \
        --annotation "index:org.opencontainers.image.source=https://github.com/aeromux/aeromux" \
        --annotation "index:org.opencontainers.image.url=https://github.com/aeromux/aeromux" \
        --annotation "index:org.opencontainers.image.licenses=GPL-3.0-or-later" \
        --annotation "index:org.opencontainers.image.vendor=Nandor Toth" \
        --provenance=false \
        --push \
        "$BUILD_CONTEXT"

    PUSHED=true
    log "✓ Pushed ${IMAGE_NAME}:${VERSION} and ${IMAGE_NAME}:latest"
    log ""
fi

# Summary
log "================================================"
log "DOCKER BUILD SUMMARY"
log "================================================"
log ""
log "Images built successfully!"
log "Image:        ${IMAGE_NAME}:${VERSION}"
log "Architecture: arm64, amd64"
log "Version:      $VERSION"
log "Output:       artifacts/docker/"
log ""
log "Images:"
for tar in "${TAR_FILENAMES[@]}"; do
    tar_path="$DOCKER_OUTPUT_DIR/$tar"
    if [ -f "$tar_path" ]; then
        tar_size=$(ls -lh "$tar_path" | awk '{print $5}')
        log "  - artifacts/docker/$tar ($tar_size)"
    fi
done
log ""

if [ "$PUSHED" = true ]; then
    log "Pushed:       Yes (${IMAGE_NAME}:${VERSION}, ${IMAGE_NAME}:latest)"
else
    log "Pushed:       No"
fi
log ""
