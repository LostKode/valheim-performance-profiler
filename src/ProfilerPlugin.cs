using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using BepInEx;
using BepInEx.Bootstrap;
using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace ValheimPerformanceProfiler;

[BepInPlugin(PluginGuid, PluginName, PluginVersion)]
public sealed class ProfilerPlugin : BaseUnityPlugin
{
    public const string PluginGuid = "lostkode.valheimperformanceprofiler";
    public const string PluginName = "Valheim Performance Profiler";
    public const string PluginVersion = "0.1.0";

    private readonly Stopwatch _clock = new();
    private readonly FrameStatistics _frames = new();
    private readonly int[] _lastGc = new int[3];
    private Harmony? _harmony;
    private JsonLinesReport? _report;
    private ConfigEntry<bool>? _enabled;
    private ConfigEntry<float>? _sampleSeconds;
    private ConfigEntry<float>? _serverSampleSeconds;
    private ConfigEntry<KeyboardShortcut>? _snapshotKey;
    private ConfigEntry<KeyboardShortcut>? _pauseKey;
    private bool _paused;
    private bool _headless;
    private long _lastMemory;
    private long _lastSampleElapsedMs;
    private TimeSpan _lastCpuTime;

    private void Awake()
    {
        _headless = SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null;
        _enabled = Config.Bind("Sampling", "Enabled", true, "Record lightweight rolling performance samples.");
        _sampleSeconds = Config.Bind("Sampling", "IntervalSeconds", 10f,
            new ConfigDescription("Seconds included in each routine sample.", new AcceptableValueRange<float>(2f, 120f)));
        _serverSampleSeconds = Config.Bind("Sampling", "DedicatedServerIntervalSeconds", 30f,
            new ConfigDescription("Dedicated-server sample interval.", new AcceptableValueRange<float>(10f, 300f)));
        _snapshotKey = Config.Bind("Controls", "DeepSnapshotKey", new KeyboardShortcut(KeyCode.Insert, KeyCode.RightControl),
            "Write an expensive on-demand snapshot of all loaded Unity object types.");
        _pauseKey = Config.Bind("Controls", "PauseKey", new KeyboardShortcut(KeyCode.Delete, KeyCode.RightControl),
            "Pause or resume routine sampling.");

        var directory = Path.Combine(Paths.ConfigPath, "ValheimPerformanceProfiler", "reports");
        var path = Path.Combine(directory, $"session-{DateTime.UtcNow:yyyyMMdd-HHmmss}-{Process.GetCurrentProcess().Id}.jsonl");
        try { _report = new JsonLinesReport(path); }
        catch (Exception error) { Logger.LogError($"Cannot create performance report: {error}"); enabled = false; return; }

        _clock.Start();
        _lastCpuTime = Process.GetCurrentProcess().TotalProcessorTime;
        _lastMemory = GC.GetTotalMemory(false);
        for (var generation = 0; generation < 3; generation++) _lastGc[generation] = GC.CollectionCount(generation);
        _report.Write("session_start",
            ("pluginVersion", PluginVersion), ("unityVersion", Application.unityVersion),
            ("platform", Application.platform.ToString()), ("processorCount", SystemInfo.processorCount),
            ("processor", SystemInfo.processorType), ("graphicsDevice", SystemInfo.graphicsDeviceName),
            ("graphicsMemoryMb", SystemInfo.graphicsMemorySize), ("systemMemoryMb", SystemInfo.systemMemorySize), ("dedicatedServer", _headless));

        _harmony = new Harmony(PluginGuid);
        var count = LifecyclePatches.Install(_harmony, Mark,
            reason => _report.Write("lifecycle_patch_skipped", ("reason", reason)));
        SceneManager.sceneLoaded += OnSceneLoaded;
        StartCoroutine(WriteCensusAfterStartup());
        Logger.LogInfo($"Performance report: {path}");
        Logger.LogInfo(_headless ? $"Installed {count} lifecycle marker patch(es). Dedicated-server sampling uses the reduced-overhead path."
            : $"Installed {count} lifecycle marker patch(es). Right Ctrl + Insert: deep snapshot. Right Ctrl + Delete: pause.");
    }

    private void Update()
    {
        if (_report == null) return;
        if (!_headless && _pauseKey!.Value.IsDown())
        {
            _paused = !_paused;
            _frames.SummarizeAndReset();
            _report.Write("sampling_state", ("paused", _paused), ("elapsedMs", _clock.ElapsedMilliseconds));
            Logger.LogInfo(_paused ? "Routine sampling paused." : "Routine sampling resumed.");
        }
        if (!_headless && _snapshotKey!.Value.IsDown()) WriteDeepSnapshot();
        if (!_enabled!.Value || _paused) return;
        _frames.Add(Time.unscaledDeltaTime);
        var interval = _headless ? _serverSampleSeconds!.Value : _sampleSeconds!.Value;
        if (_frames.Seconds >= interval) WriteRoutineSample();
    }

    private IEnumerator WriteCensusAfterStartup()
    {
        yield return new WaitForSecondsRealtime(2f);
        if (_report == null) yield break;
        foreach (var pair in Chainloader.PluginInfos.OrderBy(pair => pair.Key, StringComparer.Ordinal))
        {
            var metadata = pair.Value.Metadata;
            _report.Write("plugin", ("guid", metadata.GUID), ("name", metadata.Name),
                ("version", metadata.Version?.ToString() ?? string.Empty), ("location", Path.GetFileName(pair.Value.Location)));
        }
        var owners = new Dictionary<string, int>(StringComparer.Ordinal);
        var patchedMethods = 0;
        foreach (var method in Harmony.GetAllPatchedMethods())
        {
            patchedMethods++;
            var info = Harmony.GetPatchInfo(method);
            if (info == null) continue;
            foreach (var owner in info.Owners.Distinct(StringComparer.Ordinal))
                owners[owner] = owners.TryGetValue(owner, out var value) ? value + 1 : 1;
        }
        _report.Write("census", ("plugins", Chainloader.PluginInfos.Count),
            ("patchedMethods", patchedMethods), ("patchOwners", owners.Count));
        foreach (var owner in owners.OrderByDescending(pair => pair.Value).ThenBy(pair => pair.Key, StringComparer.Ordinal))
            _report.Write("patch_owner", ("owner", owner.Key), ("patchedMethods", owner.Value));
    }

    private void WriteRoutineSample()
    {
        var summary = _frames.SummarizeAndReset();
        var memory = GC.GetTotalMemory(false);
        using var process = Process.GetCurrentProcess();
        var cpuTime = process.TotalProcessorTime;
        var elapsedMs = _clock.ElapsedMilliseconds;
        var wallDeltaMs = elapsedMs - _lastSampleElapsedMs;
        var processCpuPercent = wallDeltaMs <= 0 ? 0 :
            (cpuTime - _lastCpuTime).TotalMilliseconds * 100d / wallDeltaMs;
        var gc0 = GC.CollectionCount(0); var gc1 = GC.CollectionCount(1); var gc2 = GC.CollectionCount(2);
        _report!.Write("sample", ("elapsedMs", elapsedMs),
            ("scene", SceneManager.GetActiveScene().name), ("frames", summary.Frames), ("seconds", summary.Seconds),
            ("averageFps", summary.AverageFps), ("onePercentLowFps", summary.OnePercentLowFps),
            ("meanFrameMs", summary.MeanMs), ("medianFrameMs", summary.MedianMs),
            ("p95FrameMs", summary.P95Ms), ("p99FrameMs", summary.P99Ms), ("worstFrameMs", summary.WorstMs),
            ("managedMemoryBytes", memory), ("managedMemoryDeltaBytes", memory - _lastMemory),
            ("workingSetBytes", process.WorkingSet64), ("privateMemoryBytes", process.PrivateMemorySize64),
            ("processCpuPercent", processCpuPercent),
            ("gc0Delta", gc0 - _lastGc[0]), ("gc1Delta", gc1 - _lastGc[1]), ("gc2Delta", gc2 - _lastGc[2]));
        _lastMemory = memory; _lastGc[0] = gc0; _lastGc[1] = gc1; _lastGc[2] = gc2;
        _lastSampleElapsedMs = elapsedMs; _lastCpuTime = cpuTime;
    }

    private void WriteDeepSnapshot()
    {
        if (_report == null) return;
        var scan = Stopwatch.StartNew();
        var counts = new Dictionary<string, int>(StringComparer.Ordinal);
        var objects = Resources.FindObjectsOfTypeAll<Object>();
        foreach (var item in objects)
        {
            if (item == null) continue;
            var name = item.GetType().FullName ?? item.GetType().Name;
            counts[name] = counts.TryGetValue(name, out var value) ? value + 1 : 1;
        }
        scan.Stop();
        _report.Write("deep_snapshot", ("elapsedMs", _clock.ElapsedMilliseconds),
            ("scanDurationMs", scan.ElapsedMilliseconds), ("objects", objects.Length),
            ("types", counts.Count), ("scene", SceneManager.GetActiveScene().name));
        foreach (var pair in counts.OrderByDescending(pair => pair.Value).ThenBy(pair => pair.Key, StringComparer.Ordinal))
            _report.Write("object_type", ("name", pair.Key), ("count", pair.Value));
        Logger.LogInfo($"Deep snapshot recorded {objects.Length} Unity objects in {scan.ElapsedMilliseconds} ms.");
    }

    private void Mark(string marker) => _report?.Write("marker", ("name", marker),
        ("elapsedMs", _clock.ElapsedMilliseconds), ("scene", SceneManager.GetActiveScene().name));

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode) => _report?.Write("scene_loaded",
        ("name", scene.name), ("buildIndex", scene.buildIndex), ("mode", mode.ToString()),
        ("elapsedMs", _clock.ElapsedMilliseconds));

    private void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        if (_report != null)
        {
            if (_frames.Count > 0) WriteRoutineSample();
            _report.Write("session_end", ("elapsedMs", _clock.ElapsedMilliseconds));
            _report.Dispose(); _report = null;
        }
        _harmony?.UnpatchSelf();
    }
}
