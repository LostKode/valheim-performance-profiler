using System;
using System.Collections.Generic;

namespace ValheimPerformanceProfiler;

public readonly struct FrameSummary
{
    public FrameSummary(int frames, double seconds, double meanMs, double medianMs,
        double p95Ms, double p99Ms, double onePercentLowFps, double worstMs)
    {
        Frames = frames; Seconds = seconds; MeanMs = meanMs; MedianMs = medianMs;
        P95Ms = p95Ms; P99Ms = p99Ms; OnePercentLowFps = onePercentLowFps; WorstMs = worstMs;
    }
    public int Frames { get; }
    public double Seconds { get; }
    public double MeanMs { get; }
    public double MedianMs { get; }
    public double P95Ms { get; }
    public double P99Ms { get; }
    public double OnePercentLowFps { get; }
    public double WorstMs { get; }
    public double AverageFps => MeanMs <= 0 ? 0 : 1000d / MeanMs;
}

public sealed class FrameStatistics
{
    private readonly List<double> _milliseconds = new(4096);
    private double _seconds;
    public int Count => _milliseconds.Count;
    public double Seconds => _seconds;

    public void Add(double seconds)
    {
        if (seconds <= 0 || double.IsNaN(seconds) || double.IsInfinity(seconds)) return;
        _seconds += seconds;
        _milliseconds.Add(seconds * 1000d);
    }

    public FrameSummary SummarizeAndReset()
    {
        if (_milliseconds.Count == 0) return new FrameSummary(0, _seconds, 0, 0, 0, 0, 0, 0);
        var sorted = _milliseconds.ToArray();
        Array.Sort(sorted);
        double total = 0;
        foreach (var value in sorted) total += value;
        var slowestCount = Math.Max(1, (int)Math.Ceiling(sorted.Length * 0.01d));
        double slowestTotal = 0;
        for (var index = sorted.Length - slowestCount; index < sorted.Length; index++) slowestTotal += sorted[index];
        var slowestMean = slowestTotal / slowestCount;
        var result = new FrameSummary(sorted.Length, _seconds, total / sorted.Length,
            Percentile(sorted, 0.50d), Percentile(sorted, 0.95d), Percentile(sorted, 0.99d),
            slowestMean <= 0 ? 0 : 1000d / slowestMean, sorted[sorted.Length - 1]);
        _milliseconds.Clear();
        _seconds = 0;
        return result;
    }

    private static double Percentile(double[] sorted, double percentile)
    {
        if (sorted.Length == 1) return sorted[0];
        var position = (sorted.Length - 1) * percentile;
        var lower = (int)Math.Floor(position);
        var upper = (int)Math.Ceiling(position);
        return lower == upper ? sorted[lower] : sorted[lower] + ((sorted[upper] - sorted[lower]) * (position - lower));
    }
}
