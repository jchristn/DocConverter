#!/usr/bin/env bash
# reinstall-tool.sh [net8.0|net10.0]
#
# Uninstalls the global docconv tool (if installed), packs src/DocConverter.Cli,
# installs it again, and verifies it runs. Works with macOS bash 3.2 and Linux.
set -e

ROOT_DIR="$(cd "$(dirname "$0")" && pwd)"
PACKAGE_ROOT="$ROOT_DIR/artifacts/tool-packages"
cd "$ROOT_DIR"

resolve_framework() {
    case "${1:-}" in
        "")
            if dotnet --list-sdks 2>/dev/null | grep -q '^10\.'; then
                FRAMEWORK="net10.0"
            else
                FRAMEWORK="net8.0"
            fi
            ;;
        -h|--help)
            echo "Usage: $(basename "$0") [net8.0|net10.0]"
            echo "Defaults to net10.0 when a .NET 10 SDK is installed, otherwise net8.0."
            exit 0
            ;;
        net8|net8.0)
            FRAMEWORK="net8.0"
            ;;
        net10|net10.0)
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

resolve_framework "${1:-}"

PACKAGE_SOURCE="$PACKAGE_ROOT/$FRAMEWORK"

echo "Removing docconv..."
dotnet tool uninstall -g DocConverter.Cli 2>/dev/null || true
rm -rf "$PACKAGE_SOURCE"
mkdir -p "$PACKAGE_SOURCE"

echo "Building docconv for $FRAMEWORK..."
dotnet pack src/DocConverter.Cli/DocConverter.Cli.csproj --configuration Release -p:TargetFrameworks="$FRAMEWORK" --output "$PACKAGE_SOURCE"

echo "Installing docconv..."
dotnet tool install -g --source "$PACKAGE_SOURCE" --framework "$FRAMEWORK" --disable-parallel DocConverter.Cli
docconv --version
