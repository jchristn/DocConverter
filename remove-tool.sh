#!/usr/bin/env bash
# remove-tool.sh: uninstall the global docconv tool.
set -e
echo "Removing docconv..."
dotnet tool uninstall -g DocConverter.Cli
