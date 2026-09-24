#!/usr/bin/env bash
# remove-tool.sh [net8.0|net10.0]
# remove-tool.sh --framework <net8.0|net10.0>
# remove-tool.sh -f <net8.0|net10.0>
#
# Uninstalls the global docconv tool and deletes the locally built tool package for the given
# framework (artifacts/tool-packages/<framework>). Without a framework, the packages for every
# framework are deleted. Works with the bash 3.2 that ships with macOS and with Linux shells.
set -e

ROOT_DIR="$(cd "$(dirname "$0")" && pwd)"
PACKAGE_ROOT="$ROOT_DIR/artifacts/tool-packages"

usage() {
    echo "Usage: $(basename "$0") [net8.0|net10.0]"
    echo "       $(basename "$0") --framework <net8.0|net10.0>"
    echo "       $(basename "$0") -f <net8.0|net10.0>"
    echo "Uninstalls docconv and deletes the local tool package for the given framework,"
    echo "or for every framework when none is given."
}

FRAMEWORK_ARGUMENT=""
while [ $# -gt 0 ]; do
    case "$1" in
        -h|--help)
            usage
            exit 0
            ;;
        -f|--framework)
            if [ -z "${2:-}" ] || [ -n "$FRAMEWORK_ARGUMENT" ]; then
                usage
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

FRAMEWORK=""
case "$FRAMEWORK_ARGUMENT" in
    "") ;;
    net8|net8.0|NET8|NET8.0) FRAMEWORK="net8.0" ;;
    net10|net10.0|NET10|NET10.0) FRAMEWORK="net10.0" ;;
    *)
        echo "Unsupported framework '$FRAMEWORK_ARGUMENT'."
        echo "Supported frameworks: net8.0, net10.0."
        exit 1
        ;;
esac

if command -v pgrep >/dev/null 2>&1 && pgrep -x docconv >/dev/null 2>&1; then
    echo "A running docconv process is using the global tool install. Wait for it to finish."
    exit 1
fi

echo "Removing docconv..."
if ! UNINSTALL_OUTPUT="$(dotnet tool uninstall -g DocConverter.Cli 2>&1)"; then
    if echo "$UNINSTALL_OUTPUT" | grep -qiE 'could not be found|not currently installed'; then
        echo "docconv is not installed."
    else
        echo "$UNINSTALL_OUTPUT"
        echo "Failed to uninstall docconv."
        exit 1
    fi
else
    echo "$UNINSTALL_OUTPUT"
fi

if [ -z "$FRAMEWORK" ]; then
    rm -rf "$PACKAGE_ROOT"
    echo "Deleted local tool packages for all frameworks."
else
    rm -rf "${PACKAGE_ROOT:?}/$FRAMEWORK"
    echo "Deleted local tool package for $FRAMEWORK."
fi
