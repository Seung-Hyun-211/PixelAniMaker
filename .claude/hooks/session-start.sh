#!/bin/bash
# Claude Code on the web: install the .NET SDK and restore NuGet packages.
set -euo pipefail

if [ "${CLAUDE_CODE_REMOTE:-}" != "true" ]; then
  exit 0
fi

export DEBIAN_FRONTEND=noninteractive
export DOTNET_CLI_TELEMETRY_OPTOUT=1
export DOTNET_NOLOGO=1

# Avalonia 12 analyzers need Roslyn 4.14+, so the 8.0 SDK can't build the App.
# The 10.0 SDK builds net8.0 targets; the 8.0 runtime runs the net8.0 tests.
if ! dotnet --list-sdks 2>/dev/null | grep -q '^10\.' \
  || ! dotnet --list-runtimes 2>/dev/null | grep -q 'Microsoft.NETCore.App 8\.'; then
  apt-get update -qq
  apt-get install -y -qq dotnet-sdk-10.0 dotnet-runtime-8.0
fi

{
  echo 'export DOTNET_CLI_TELEMETRY_OPTOUT=1'
  echo 'export DOTNET_NOLOGO=1'
} >> "${CLAUDE_ENV_FILE:-/dev/null}"

cd "${CLAUDE_PROJECT_DIR:-$(dirname "$0")/../..}"
dotnet restore PixelAniMaker.sln
