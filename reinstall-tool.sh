#!/usr/bin/env bash
# reinstall-tool.sh [net8.0|net10.0]
# reinstall-tool.sh --framework <net8.0|net10.0>
# reinstall-tool.sh -f <net8.0|net10.0>
#
# Uninstalls the global docconv tool (if installed), packs src/DocConverter.Cli for ONE target
# framework, installs it for that framework only, and verifies it. Refuses to run while docconv is
# running. Without a framework: net10.0 when a .NET 10 SDK is installed, otherwise net8.0.
# Works with the bash 3.2 that ships with macOS and with Linux shells.
set -e

ROOT_DIR="$(cd "$(dirname "$0")" && pwd)"
PACKAGE_ROOT="$ROOT_DIR/artifacts/tool-packages"
TOOL_STORE="${DOTNET_CLI_HOME:-$HOME}/.dotnet/tools/.store/docconverter.cli"
cd "$ROOT_DIR"

usage() {
    echo "Usage: $(basename "$0") [net8.0|net10.0]"
    echo "       $(basename "$0") --framework <net8.0|net10.0>"
    echo "       $(basename "$0") -f <net8.0|net10.0>"
    echo "Removes docconv, then builds and installs it for the given framework only."
    echo "Defaults to net10.0 when a .NET 10 SDK is installed, otherwise net8.0."
}

parse_args() {
    FRAMEWORK_ARGUMENT=""
    while [ $# -gt 0 ]; do
        case "$1" in
            -h|--help)
                usage
                exit 0
                ;;
            -f|--framework)
                if [ -z "${2:-}" ]; then
                    echo "Missing framework value after $1."
                    usage
                    exit 1
                fi
                if [ -n "$FRAMEWORK_ARGUMENT" ]; then
                    echo "Unexpected argument '$1'. Specify one framework."
                    exit 1
                fi
                FRAMEWORK_ARGUMENT="$2"
                shift 2
                ;;
            *)
                if [ -n "$FRAMEWORK_ARGUMENT" ]; then
                    echo "Unexpected argument '$1'. Specify one framework."
                    usage
                    exit 1
                fi
                FRAMEWORK_ARGUMENT="$1"
                shift
                ;;
        esac
    done
}

resolve_framework() {
    case "${1:-}" in
        "")
            if dotnet --list-sdks 2>/dev/null | grep -q '^10\.'; then
                FRAMEWORK="net10.0"
            else
                FRAMEWORK="net8.0"
            fi
            ;;
        net8|net8.0|NET8|NET8.0)
            FRAMEWORK="net8.0"
            ;;
        net10|net10.0|NET10|NET10.0)
            FRAMEWORK="net10.0"
            ;;
        *)
            echo "Unsupported framework '$1'."
            echo "Supported frameworks: net8.0, net10.0."
            echo "Use net8.0 on systems without a .NET 10 SDK."
            exit 1
            ;;
    esac
}

# Confirms the installed tool contains the requested framework and no other.
verify_install() {
    if [ ! -d "$TOOL_STORE" ]; then
        echo "Note: tool store not found at $TOOL_STORE; skipping the framework check."
        return 0
    fi
    found_target=""
    found_other=""
    for dir in "$TOOL_STORE"/*/docconverter.cli/*/tools/*; do
        [ -d "$dir" ] || continue
        name="$(basename "$dir")"
        if [ "$name" = "$FRAMEWORK" ]; then
            found_target="yes"
        else
            found_other="$found_other $name"
        fi
    done
    if [ -n "$found_other" ]; then
        echo "The installed tool also contains$found_other; expected $FRAMEWORK only."
        exit 1
    fi
    if [ -z "$found_target" ]; then
        echo "The installed tool does not contain $FRAMEWORK."
        exit 1
    fi
    echo "Installed for $FRAMEWORK only."
}

parse_args "$@"
resolve_framework "$FRAMEWORK_ARGUMENT"
PACKAGE_SOURCE="$PACKAGE_ROOT/$FRAMEWORK"

echo "docconv tool reinstall"
echo "Framework: $FRAMEWORK"
echo "Package source: $PACKAGE_SOURCE"
echo

echo "[1/6] Checking for running docconv processes..."
if command -v pgrep >/dev/null 2>&1 && pgrep -x docconv >/dev/null 2>&1; then
    echo "A running docconv process is using the global tool install."
    echo "Wait for it to finish and rerun reinstall-tool.sh."
    exit 1
fi
echo "No running docconv process found."
echo

echo "[2/6] Removing existing docconv global tool if present..."
if ! UNINSTALL_OUTPUT="$(dotnet tool uninstall -g DocConverter.Cli 2>&1)"; then
    if echo "$UNINSTALL_OUTPUT" | grep -qiE 'could not be found|not currently installed'; then
        echo "docconv is not installed; continuing..."
    else
        echo "$UNINSTALL_OUTPUT"
        echo "Failed to uninstall docconv. Ensure no docconv processes are running and rerun this script."
        exit 1
    fi
else
    echo "$UNINSTALL_OUTPUT"
fi
echo

echo "[3/6] Preparing package directory..."
rm -rf "$PACKAGE_SOURCE"
mkdir -p "$PACKAGE_SOURCE"
echo

echo "[4/6] Building docconv for $FRAMEWORK only..."
dotnet pack src/DocConverter.Cli/DocConverter.Cli.csproj --configuration Release -p:TargetFrameworks="$FRAMEWORK" --output "$PACKAGE_SOURCE"
echo

echo "[5/6] Installing docconv for $FRAMEWORK..."
dotnet tool install -g --source "$PACKAGE_SOURCE" --framework "$FRAMEWORK" --disable-parallel DocConverter.Cli
echo

echo "[6/6] Verifying the install..."
verify_install
docconv --version
