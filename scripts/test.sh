#!/usr/bin/env bash
set -euo pipefail
repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
dotnet_cmd="${DOTNET_CMD:-dotnet}"
"$dotnet_cmd" run --project "$repo_root/tests/ValheimPerformanceProfiler.Tests.csproj" --configuration Release

