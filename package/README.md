# Valheim Performance Profiler

Measure modded Valheim instead of guessing which mod is expensive.

The profiler records rolling FPS and frame times, 1% lows, process memory and
CPU, garbage collection changes, game lifecycle timing, installed plugins, and Harmony
patch ownership. Press `Right Ctrl + Insert` for an on-demand deep Unity-object snapshot.

Reports are stored locally under
`BepInEx/config/ValheimPerformanceProfiler/reports`. Routine sampling is
designed to be lightweight. Deep snapshots are manual because they may cause a
brief pause in very large profiles.

Dedicated servers automatically use a reduced-overhead path with no keyboard
polling or deep snapshots and a 30-second default sample interval.

