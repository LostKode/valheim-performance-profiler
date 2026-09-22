#!/usr/bin/env bash
set -euo pipefail
if [[ $# -ne 2 ]]; then echo "usage: $0 VALHEIM_MANAGED_DIR BEPINEX_CORE_DIR" >&2; exit 2; fi
repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
"$repo_root/scripts/build.sh" "$1" "$2"
version="$(python3 -c 'import json,sys; print(json.load(open(sys.argv[1]))["version_number"])' "$repo_root/package/manifest.json")"
dll="$repo_root/src/bin/Release/netstandard2.1/ValheimPerformanceProfiler.dll"
output="$repo_root/artifacts/LostKode-Valheim_Performance_Profiler-$version.zip"
python3 "$repo_root/scripts/package.py" "$repo_root/package" "$dll" "$output"
sha256sum "$output"
