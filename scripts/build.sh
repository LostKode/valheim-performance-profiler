#!/usr/bin/env bash
set -euo pipefail
if [[ $# -ne 2 ]]; then echo "usage: $0 VALHEIM_MANAGED_DIR BEPINEX_CORE_DIR" >&2; exit 2; fi
repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
dotnet_cmd="${DOTNET_CMD:-dotnet}"
"$dotnet_cmd" build "$repo_root/src/ValheimPerformanceProfiler.csproj" --configuration Release \
  -p:ValheimManagedDir="$1" -p:BepInExCoreDir="$2"

