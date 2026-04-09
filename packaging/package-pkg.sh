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
    echo "  --notarize         Notarize and staple the package synchronously (requires --sign)"
    echo "  --submit           Submit for notarization and return immediately (requires --sign)"
    echo "  --staple           Check notarization status; staple if accepted"
    echo "  --validate         Verify signature, notarization, and staple status"
    echo "  --rebuild          Force rebuild even if binaries exist and are recent"
    echo "  --silent           Suppress all output (only errors are shown)"
    echo ""
    echo "Notarization modes (mutually exclusive):"
    echo "  --notarize         Synchronous: submit + wait + staple + validate"
    echo "  --submit/--staple  Async: submit first, then staple when ready"
    echo "  --validate         Standalone: verify an existing package"
    echo ""
    echo "Examples:"
    echo "  ./packaging/package-pkg.sh --target osx-arm64"
    echo "  ./packaging/package-pkg.sh --target osx-arm64 --sign"
    echo "  ./packaging/package-pkg.sh --target all --sign --notarize --rebuild"
    echo ""
    echo "  # Async notarization workflow:"
    echo "  ./packaging/package-pkg.sh --target osx-arm64 --sign --submit"
    echo "  ./packaging/package-pkg.sh --target osx-arm64 --staple"
    echo "  ./packaging/package-pkg.sh --target osx-arm64 --validate"
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
    local sign_status="unsigned"
    if [ "$SIGN" = true ]; then
        if codesign --sign "Developer ID Application" --options runtime --entitlements "$PKG_DIR/entitlements.plist" --force "$staging/payload/bin/aeromux" > /dev/null 2>&1 \
            && codesign --verify "$staging/payload/bin/aeromux" > /dev/null 2>&1; then
            sign_status="binary_signed"
            log "✓ Binary signed: $runtime_id" >&2
        else
            sign_status="signing_failed"
            log "✗ Binary signing failed: $runtime_id" >&2
        fi
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

    if [ "$sign_status" = "binary_signed" ]; then
        # Attempt signed productbuild, fall back to unsigned on failure
        if productbuild \
                --distribution "$staging/distribution.xml" \
                --resources "$staging/resources" \
                --package-path "$staging" \
                --sign "Developer ID Installer: Nándor Tóth" \
                "$output_path" > /dev/null 2>&1 \
            && pkgutil --check-signature "$output_path" > /dev/null 2>&1; then
            sign_status="signed"
            log "✓ Package signed: $pkg_filename" >&2
        else
            log "✗ Package signing failed: $pkg_filename, building unsigned" >&2
            rm -f "$output_path"
            sign_status="signing_failed"
            run_quiet productbuild \
                --distribution "$staging/distribution.xml" \
                --resources "$staging/resources" \
                --package-path "$staging" \
                "$output_path"
        fi
    else
        run_quiet productbuild \
            --distribution "$staging/distribution.xml" \
            --resources "$staging/resources" \
            --package-path "$staging" \
            "$output_path"
    fi

    # Cleanup staging
    rm -rf "$staging"

    # Return filename and sign status as colon-delimited pair (subshell prevents global vars)
    echo "${pkg_filename}:${sign_status}"
}

# ── Notarization functions ───────────────────────────────────────────────────

# Submit a .pkg for notarization (no wait)
# Args: $1 = pkg_filename
# Stdout: submission UUID
submit_package() {
    local pkg_filename="$1"
    local output_path="$PACKAGES_DIR/$pkg_filename"
    local output
    output=$(xcrun notarytool submit "$output_path" \
        --keychain-profile "$NOTARY_PROFILE" 2>&1) || {
        echo "ERROR: Submission failed for $pkg_filename." >&2
        if [ -n "$output" ]; then
            echo "$output" >&2
        fi
        return 1
    }
    local submission_id
    submission_id=$(echo "$output" | sed -n 's/.*id: \([a-f0-9-]*\).*/\1/p' | head -1)
    if [ -z "$submission_id" ]; then
        echo "ERROR: Could not parse submission ID for $pkg_filename." >&2
        return 1
    fi
    echo "$submission_id"
}

# Submit a .pkg for notarization and wait for completion
# Args: $1 = pkg_filename
submit_and_wait_package() {
    local pkg_filename="$1"
    local output_path="$PACKAGES_DIR/$pkg_filename"
    xcrun notarytool submit "$output_path" \
        --keychain-profile "$NOTARY_PROFILE" --wait > /dev/null 2>&1 || {
        echo "ERROR: Notarization failed for $pkg_filename." >&2
        return 1
    }
}

# Check notarization status for a submission
# Args: $1 = submission_id
# Stdout: status string (Accepted, In Progress, Invalid, Rejected)
check_notarization_status() {
    local submission_id="$1"
    local output
    output=$(xcrun notarytool info "$submission_id" \
        --keychain-profile "$NOTARY_PROFILE" 2>&1) || {
        echo "ERROR: Could not check status for submission $submission_id." >&2
        echo "unknown"
        return 1
    }
    local status
    status=$(echo "$output" | sed -n 's/.*status: \(.*\)/\1/p' | head -1)
    echo "${status:-unknown}"
}

# Staple and validate the notarization ticket
# Args: $1 = pkg_filename
staple_package() {
    local pkg_filename="$1"
    local output_path="$PACKAGES_DIR/$pkg_filename"
    xcrun stapler staple "$output_path" > /dev/null 2>&1 || {
        echo "ERROR: Stapling failed for $pkg_filename." >&2
        return 1
    }
    if ! xcrun stapler validate "$output_path" > /dev/null 2>&1; then
        echo "ERROR: Staple validation failed for $pkg_filename." >&2
        return 1
    fi
    return 0
}

# Full validation: signature + notarization + staple
# Args: $1 = pkg_filename
# Stdout: multi-line "key:pass" or "key:fail" results
# Returns: number of failures (0 = all pass)
validate_package() {
    local pkg_filename="$1"
    local output_path="$PACKAGES_DIR/$pkg_filename"
    local failures=0

    if pkgutil --check-signature "$output_path" > /dev/null 2>&1; then
        echo "signature:pass"
    else
        echo "signature:fail"
        failures=$((failures + 1))
    fi

    if spctl --assess --type install "$output_path" > /dev/null 2>&1; then
        echo "notarization:pass"
    else
        echo "notarization:fail"
        failures=$((failures + 1))
    fi

    if xcrun stapler validate "$output_path" > /dev/null 2>&1; then
        echo "staple:pass"
    else
        echo "staple:fail"
        failures=$((failures + 1))
    fi

    return "$failures"
}

# Synchronous notarize: submit+wait, staple, validate (composes from above)
# Args: $1 = pkg_filename
notarize_package() {
    local pkg_filename="$1"
    submit_and_wait_package "$pkg_filename" || return 1
    staple_package "$pkg_filename" || return 1
    return 0
}

# ── Notarization record helpers ──────────────────────────────────────────────

# Write a .notarization sidecar file
# Args: $1 = pkg_filename, $2 = submission_id, $3 = target
write_notarization_record() {
    local pkg_filename="$1"
    local submission_id="$2"
    local target="$3"
    local record_path="$PACKAGES_DIR/${pkg_filename}.notarization"
    printf "%s\n" \
        "# Aeromux notarization record — do not edit" \
        "# Generated by package-pkg.sh" \
        "id=$submission_id" \
        "target=$target" \
        "submitted=$(date -u +"%Y-%m-%dT%H:%M:%SZ")" \
        > "$record_path"
}

# Read submission ID from a .notarization sidecar file
# Args: $1 = pkg_filename
# Stdout: submission ID
read_notarization_record() {
    local pkg_filename="$1"
    local record_path="$PACKAGES_DIR/${pkg_filename}.notarization"
    if [ ! -f "$record_path" ]; then
        return 1
    fi
    sed -n 's/^id=//p' "$record_path"
}

# Delete the .notarization sidecar file
# Args: $1 = pkg_filename
delete_notarization_record() {
    local pkg_filename="$1"
    rm -f "$PACKAGES_DIR/${pkg_filename}.notarization"
}

# ── Argument parsing ─────────────────────────────────────────────────────────

SILENT=false
TARGET=""
REBUILD=false
SIGN=false
NOTARIZE=false
SUBMIT=false
STAPLE=false
VALIDATE=false

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
        --submit)
            SUBMIT=true
            shift
            ;;
        --staple)
            STAPLE=true
            shift
            ;;
        --validate)
            VALIDATE=true
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

# Validate notarization mode mutual exclusivity
NOTARY_MODE_COUNT=0
for flag in NOTARIZE SUBMIT STAPLE VALIDATE; do
    if [ "${!flag}" = true ]; then
        NOTARY_MODE_COUNT=$((NOTARY_MODE_COUNT + 1))
    fi
done
if [ "$NOTARY_MODE_COUNT" -gt 1 ]; then
    echo "ERROR: --notarize, --submit, --staple, and --validate are mutually exclusive"
    exit 1
fi

# Validate --notarize requires --sign
if [ "$NOTARIZE" = true ] && [ "$SIGN" != true ]; then
    echo "ERROR: --notarize requires --sign"
    exit 1
fi

# Validate --submit requires --sign
if [ "$SUBMIT" = true ] && [ "$SIGN" != true ]; then
    echo "ERROR: --submit requires --sign"
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

# Determine if this is a standalone operation (no build pipeline)
STANDALONE=false
if [ "$STAPLE" = true ] || [ "$VALIDATE" = true ]; then
    STANDALONE=true
fi

# Build the --target hint for next-step messages
TARGET_HINT="$TARGET"

if [ "$STANDALONE" = true ]; then
    # ── Standalone mode: staple or validate existing packages ────────────

    # Read version
    VERSION=$(sed -n 's/.*<Version>\(.*\)<\/Version>.*/\1/p' "$PROPS_FILE")

    # Build expected filenames and verify packages exist
    PKG_FILENAMES=()
    for i in "${!TARGETS[@]}"; do
        pkg_arch="${PKG_ARCHS[$i]}"
        pkg_filename="aeromux_${VERSION}_macos_${pkg_arch}.pkg"
        pkg_path="$PACKAGES_DIR/$pkg_filename"
        if [ ! -f "$pkg_path" ]; then
            echo "ERROR: Package not found: artifacts/packages/$pkg_filename"
            echo "       Build first: ./packaging/package-pkg.sh --target ${TARGETS[$i]} --sign --rebuild"
            exit 1
        fi
        PKG_FILENAMES+=("$pkg_filename")
    done

    # Check notarization prerequisites for --staple
    if [ "$STAPLE" = true ]; then
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
    fi

    # ── Staple mode ─────────────────────────────────────────────────────
    if [ "$STAPLE" = true ]; then
        [ "$SILENT" = true ] || clear

        log "================================================"
        log "Aeromux Notarization Status"
        log "================================================"
        log ""
        log "Checking notarization status..."
        log ""

        HAS_FAILURES=false
        HAS_IN_PROGRESS=false
        ALL_STAPLED=true

        for i in "${!PKG_FILENAMES[@]}"; do
            pkg="${PKG_FILENAMES[$i]}"
            target="${TARGETS[$i]}"

            submission_id=$(read_notarization_record "$pkg") || {
                echo "ERROR: No pending notarization found for $pkg"
                echo "       Submit first: ./packaging/package-pkg.sh --target $target --sign --submit"
                HAS_FAILURES=true
                ALL_STAPLED=false
                continue
            }

            status=$(check_notarization_status "$submission_id") || {
                HAS_FAILURES=true
                ALL_STAPLED=false
                continue
            }

            case "$status" in
                Accepted)
                    if staple_package "$pkg"; then
                        delete_notarization_record "$pkg"
                        log "✓ $pkg stapled and validated"
                    else
                        log "✗ Stapling failed: $pkg"
                        HAS_FAILURES=true
                        ALL_STAPLED=false
                    fi
                    ;;
                "In Progress")
                    log "→ $pkg: notarization in progress (ID: $submission_id)"
                    HAS_IN_PROGRESS=true
                    ALL_STAPLED=false
                    ;;
                Invalid|Rejected)
                    log "✗ $pkg: notarization $status (ID: $submission_id)"
                    delete_notarization_record "$pkg"
                    HAS_FAILURES=true
                    ALL_STAPLED=false
                    ;;
                *)
                    log "✗ $pkg: unknown status '$status' (ID: $submission_id)"
                    HAS_FAILURES=true
                    ALL_STAPLED=false
                    ;;
            esac
        done

        log ""
        if [ "$ALL_STAPLED" = true ]; then
            log "================================================"
            log "STAPLING SUMMARY"
            log "================================================"
            log ""
            for i in "${!PKG_FILENAMES[@]}"; do
                log "✓ ${PKG_FILENAMES[$i]} stapled and validated"
            done
            log ""
            log "Next step: ./packaging/package-pkg.sh --target $TARGET_HINT --validate"
            log ""
        elif [ "$HAS_IN_PROGRESS" = true ]; then
            log "================================================"
            log "NOTARIZATION STATUS"
            log "================================================"
            log ""
            log "Notarization is still in progress."
            log ""
            log "Check again: ./packaging/package-pkg.sh --target $TARGET_HINT --staple"
            log ""
        fi

        if [ "$HAS_FAILURES" = true ]; then
            exit 1
        fi

    # ── Validate mode ───────────────────────────────────────────────────
    elif [ "$VALIDATE" = true ]; then
        [ "$SILENT" = true ] || clear

        log "================================================"
        log "Aeromux Package Validation"
        log "================================================"
        log ""

        HAS_FAILURES=false

        for i in "${!PKG_FILENAMES[@]}"; do
            pkg="${PKG_FILENAMES[$i]}"

            results=$(validate_package "$pkg") || true
            sig=$(echo "$results" | sed -n 's/^signature://p')
            notar=$(echo "$results" | sed -n 's/^notarization://p')
            stpl=$(echo "$results" | sed -n 's/^staple://p')

            all_pass=true
            [ "$sig" = "fail" ] && all_pass=false
            [ "$notar" = "fail" ] && all_pass=false
            [ "$stpl" = "fail" ] && all_pass=false

            if [ "$all_pass" = true ]; then
                log "✓ $pkg"
            else
                log "✗ $pkg"
                HAS_FAILURES=true
            fi
            log "  Signature:    $sig"
            log "  Notarization: $notar"
            log "  Staple:       $stpl"
            log ""
        done

        log "================================================"
        log "VALIDATION SUMMARY"
        log "================================================"
        log ""

        if [ "$HAS_FAILURES" = true ]; then
            log "Validation completed with failures."
            log ""
            exit 1
        else
            log "Ready for distribution."
            log ""
        fi
    fi

else
    # ── Full build pipeline ─────────────────────────────────────────────

    # Clear screen and print header
    [ "$SILENT" = true ] || clear

    log "================================================"
    log "Aeromux macOS Packaging"
    log "================================================"
    log ""

    if [ "${#TARGETS[@]}" -gt 1 ]; then
        log "Packaging all targets..."
        ARCH_PREVIEW="${TARGETS[0]}"
        for t in "${TARGETS[@]:1}"; do
            ARCH_PREVIEW="$ARCH_PREVIEW, $t"
        done
        log "✓ Target architectures: $ARCH_PREVIEW"
    else
        log "Packaging target..."
        log "✓ Target architecture: ${TARGETS[0]}"
    fi
    log ""

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

    # Check notarization prerequisites if --notarize or --submit
    if [ "$NOTARIZE" = true ] || [ "$SUBMIT" = true ]; then
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
            echo "Rebuild: ./packaging/package-pkg.sh --target $TARGET_HINT --rebuild"
            exit 1
        elif [ -n "$(find "$binary" -mmin +60 2>/dev/null)" ]; then
            echo "ERROR: Binary is older than 1 hour: $binary"
            echo "Rebuild: ./packaging/package-pkg.sh --target $TARGET_HINT --rebuild"
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
    PKG_SIGN_STATUSES=()
    for i in "${!TARGETS[@]}"; do
        pkg_arch="${PKG_ARCHS[$i]}"
        rid="${RUNTIME_IDS[$i]}"

        # Parse colon-delimited "filename:sign_status" from build_package
        result=$(build_package "$pkg_arch" "$rid") || exit 1
        pkg_filename="${result%%:*}"
        sign_status="${result##*:}"

        PKG_FILENAMES+=("$pkg_filename")
        PKG_SIGN_STATUSES+=("$sign_status")

        log "✓ $pkg_filename packaged"
    done
    log ""

    # ── Notarization operations ──────────────────────────────────────────

    # Initialize notarization status for all packages
    PKG_NOTARY_STATUSES=()
    for i in "${!PKG_FILENAMES[@]}"; do
        PKG_NOTARY_STATUSES+=("")
    done

    if [ "$NOTARIZE" = true ]; then
        # Synchronous: submit + wait + staple + validate (parallel when multiple targets)
        if [ "${#PKG_FILENAMES[@]}" -eq 1 ]; then
            log "Notarizing package (this may take up to 15 minutes)..."
        else
            log "Notarizing packages in parallel (this may take up to 15 minutes)..."
        fi

        NOTARY_PIDS=()
        NOTARY_LOGS=()
        NOTARY_IDX=()
        for i in "${!PKG_FILENAMES[@]}"; do
            pkg="${PKG_FILENAMES[$i]}"
            if [ "${PKG_SIGN_STATUSES[$i]}" != "signed" ]; then
                PKG_NOTARY_STATUSES[$i]="skipped"
                log "→ Skipped (unsigned): $pkg"
                continue
            fi
            notary_log=$(mktemp)
            NOTARY_LOGS+=("$notary_log")
            NOTARY_IDX+=("$i")
            notarize_package "$pkg" > "$notary_log" 2>&1 &
            NOTARY_PIDS+=($!)
            log "→ Submitted: $pkg"
        done

        # Wait for all notarizations and collect results
        for j in "${!NOTARY_PIDS[@]}"; do
            pid="${NOTARY_PIDS[$j]}"
            i="${NOTARY_IDX[$j]}"
            pkg="${PKG_FILENAMES[$i]}"
            notary_log="${NOTARY_LOGS[$j]}"

            if wait "$pid"; then
                PKG_NOTARY_STATUSES[$i]="notarized"
                log "✓ $pkg notarized and stapled"
            else
                PKG_NOTARY_STATUSES[$i]="notarization_failed"
                log "✗ Notarization failed: $pkg" >&2
                if [ -s "$notary_log" ]; then
                    cat "$notary_log" >&2
                fi
            fi
            rm -f "$notary_log"
        done
        log ""

    elif [ "$SUBMIT" = true ]; then
        # Async: submit and return immediately
        if [ "${#PKG_FILENAMES[@]}" -eq 1 ]; then
            log "Submitting package for notarization..."
        else
            log "Submitting packages for notarization..."
        fi

        for i in "${!PKG_FILENAMES[@]}"; do
            pkg="${PKG_FILENAMES[$i]}"
            target="${TARGETS[$i]}"

            if [ "${PKG_SIGN_STATUSES[$i]}" != "signed" ]; then
                PKG_NOTARY_STATUSES[$i]="skipped"
                log "→ Skipped (unsigned): $pkg"
                continue
            fi

            submission_id=$(submit_package "$pkg") || {
                PKG_NOTARY_STATUSES[$i]="submission_failed"
                log "✗ Submission failed: $pkg"
                continue
            }

            write_notarization_record "$pkg" "$submission_id" "$target"
            PKG_NOTARY_STATUSES[$i]="submitted"
            log "✓ Submitted: $pkg (ID: $submission_id)"
        done
        log ""
    fi

    # ── Summary ──────────────────────────────────────────────────────────

    ARCH_LIST="${PKG_ARCHS[0]}"
    for arch in "${PKG_ARCHS[@]:1}"; do
        ARCH_LIST="$ARCH_LIST, $arch"
    done

    HAS_FAILURES=false
    for i in "${!PKG_FILENAMES[@]}"; do
        case "${PKG_SIGN_STATUSES[$i]}" in signing_failed) HAS_FAILURES=true ;; esac
        case "${PKG_NOTARY_STATUSES[$i]}" in notarization_failed|submission_failed) HAS_FAILURES=true ;; esac
    done

    if [ "$SUBMIT" = true ]; then
        # Submission summary
        log "================================================"
        log "SUBMISSION SUMMARY"
        log "================================================"
        log ""

        if [ "$HAS_FAILURES" = true ]; then
            log "Submission completed with failures!"
        else
            log "Submission completed successfully!"
        fi
        log "Architecture: $ARCH_LIST"
        log "Version:      $VERSION"
        log ""

        for i in "${!PKG_FILENAMES[@]}"; do
            pkg="${PKG_FILENAMES[$i]}"
            case "${PKG_NOTARY_STATUSES[$i]}" in
                submitted)        log "  ✓ $pkg" ;;
                skipped)          log "  → $pkg (skipped, unsigned)" ;;
                submission_failed) log "  ✗ $pkg (submission failed)" ;;
            esac
        done

        if [ "$HAS_FAILURES" != true ]; then
            log ""
            log "Next step: ./packaging/package-pkg.sh --target $TARGET_HINT --staple"
        fi
        log ""

    else
        # Standard packaging summary (with or without --notarize)
        log "================================================"
        log "PACKAGING SUMMARY"
        log "================================================"
        log ""

        if [ "$HAS_FAILURES" = true ]; then
            log "Packaging completed with warnings!"
        else
            log "Package created successfully!"
        fi
        log "Architecture: $ARCH_LIST"
        log "Version:      $VERSION"
        log "Output:       artifacts/packages/"
        log ""

        if [ "${#PKG_FILENAMES[@]}" -eq 1 ]; then
            log "Package:"
        else
            log "Packages:"
        fi
        for i in "${!PKG_FILENAMES[@]}"; do
            pkg="${PKG_FILENAMES[$i]}"
            pkg_path="$PACKAGES_DIR/$pkg"
            pkg_size=""
            [ -f "$pkg_path" ] && pkg_size=$(ls -lh "$pkg_path" | awk '{print $5}')

            parts=""
            [ -n "$pkg_size" ] && parts="$pkg_size"
            case "${PKG_SIGN_STATUSES[$i]}" in
                signed)         parts="${parts:+$parts, }signed" ;;
                signing_failed) parts="${parts:+$parts, }signing failed" ;;
            esac
            case "${PKG_NOTARY_STATUSES[$i]}" in
                notarized)           parts="${parts:+$parts, }notarized" ;;
                notarization_failed) parts="${parts:+$parts, }notarization failed" ;;
            esac

            suffix=""
            [ -n "$parts" ] && suffix=" ($parts)"
            log "  - artifacts/packages/${pkg}${suffix}"
        done

        log ""
        log "Install on the target machine:"
        for i in "${!PKG_FILENAMES[@]}"; do
            log "  - ${TARGETS[$i]}: sudo installer -pkg artifacts/packages/${PKG_FILENAMES[$i]} -target /"
        done
        log "  - You also can double-click the .pkg file in Finder"

        # Next-step hints
        if [ "$HAS_FAILURES" != true ]; then
            if [ "$NOTARIZE" = true ]; then
                log ""
                log "Next step: ./packaging/package-pkg.sh --target $TARGET_HINT --validate"
            elif [ "$SIGN" = true ] && [ "$NOTARIZE" != true ] && [ "$SUBMIT" != true ]; then
                log ""
                log "Next step: ./packaging/package-pkg.sh --target $TARGET_HINT --submit"
            fi
        fi
        log ""
    fi

    if [ "$HAS_FAILURES" = true ]; then
        exit 1
    fi
fi
