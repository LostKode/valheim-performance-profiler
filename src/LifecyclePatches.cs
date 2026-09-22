using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;

namespace ValheimPerformanceProfiler;

internal static class LifecyclePatches
{
    private sealed class Target
    {
        public Target(string typeName, string methodName, string marker)
        { TypeName = typeName; MethodName = methodName; Marker = marker; }
        public string TypeName { get; }
        public string MethodName { get; }
        public string Marker { get; }
    }

    private static readonly Target[] Targets =
    {
        new("FejdStartup", "Start", "main_menu_start"),
        new("ZNet", "Awake", "network_awake"),
        new("ZNet", "RPC_PeerInfo", "peer_info_received"),
        new("ZoneSystem", "Start", "zone_system_start"),
        new("Game", "Start", "game_start"),
        new("Player", "OnSpawned", "player_spawned")
    };

    private static readonly Dictionary<MethodBase, string> Markers = new();
    private static Action<string>? _marker;

    public static int Install(Harmony harmony, Action<string> marker, Action<string> skipped)
    {
        _marker = marker;
        var installed = 0;
        foreach (var target in Targets)
        {
            var type = AccessTools.TypeByName(target.TypeName);
            var methods = type?.GetMethods(BindingFlags.Instance | BindingFlags.Static |
                BindingFlags.Public | BindingFlags.NonPublic).Where(method => method.Name == target.MethodName).ToArray();
            if (methods == null || methods.Length != 1)
            {
                skipped($"{target.TypeName}.{target.MethodName}: expected one overload, found {methods?.Length ?? 0}");
                continue;
            }
            Markers[methods[0]] = target.Marker;
            harmony.Patch(methods[0], postfix: new HarmonyMethod(typeof(LifecyclePatches), nameof(Postfix)));
            installed++;
        }
        return installed;
    }

    private static void Postfix(MethodBase __originalMethod)
    {
        if (Markers.TryGetValue(__originalMethod, out var marker)) _marker?.Invoke(marker);
    }
}

