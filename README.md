# Valheim Performance Profiler

Valheim Performance Profiler is a low-overhead BepInEx diagnostic mod for
measuring real modded-game performance. It records rolling frame-time
statistics, managed-memory and garbage-collection changes, lifecycle phase
timings, loaded-plugin inventory, and Harmony patch ownership.

Reports are written as JSON Lines under:

`BepInEx/config/ValheimPerformanceProfiler/reports`

The mod is useful in any Valheim profile. It has no Ragnavik-specific runtime
dependency.

## Measurements

- Mean, median, 95th percentile, and 99th percentile frame time
- Average FPS and 1% low FPS
- Managed, working-set, and private memory; process CPU; garbage-collection deltas
- Main-menu, networking, world, and local-player lifecycle markers
- Loaded BepInEx plugin inventory
- Harmony patch counts grouped by owner
- On-demand Unity object counts grouped by runtime type

Routine samples do not scan all Unity objects. The deep object snapshot is
intentionally manual because enumerating every loaded object can briefly pause
a heavily modded game.

On a dedicated server, keyboard polling and deep snapshots are disabled and the
default sample interval increases from 10 to 30 seconds. Server reports retain
frame-time, CPU, memory, plugin, patch, and lifecycle evidence.

## Default controls

- `Right Ctrl + Insert`: write a deep Unity-object snapshot
- `Right Ctrl + Delete`: pause or resume routine sampling

Both keys and all intervals are configurable in
`BepInEx/config/lostkode.valheimperformanceprofiler.cfg`.

## Building

```sh
./scripts/build.sh "/path/to/Valheim/valheim_Data/Managed" "/path/to/BepInEx/core"
./scripts/test.sh
./scripts/package.sh "/path/to/Valheim/valheim_Data/Managed" "/path/to/BepInEx/core"
```

The package script creates a Gale-compatible ZIP under `artifacts/`.

## Current boundary

Version 0.1 records everything observable after BepInEx creates the plugin.
Exact cold-start time for every earlier plugin requires a BepInEx preloader
patcher and is deliberately not included until that instrumentation can be
validated across supported BepInEx versions.

