using ValheimPerformanceProfiler;

static void Near(double expected, double actual, double tolerance, string name)
{
    if (Math.Abs(expected - actual) > tolerance) throw new Exception($"{name}: expected {expected}, got {actual}");
}

var frames = new FrameStatistics();
for (var index = 0; index < 99; index++) frames.Add(0.01);
frames.Add(0.10);
var summary = frames.SummarizeAndReset();
if (summary.Frames != 100) throw new Exception("frame count");
Near(1.09, summary.Seconds, 0.0001, "seconds");
Near(10.9, summary.MeanMs, 0.0001, "mean");
Near(10, summary.MedianMs, 0.0001, "median");
Near(10, summary.P95Ms, 0.0001, "p95");
Near(10.9, summary.P99Ms, 0.0001, "p99");
Near(10, summary.OnePercentLowFps, 0.0001, "one percent low");
Near(100, summary.WorstMs, 0.0001, "worst");
if (frames.Count != 0) throw new Exception("reset count");
frames.Add(double.NaN); frames.Add(double.PositiveInfinity); frames.Add(0);
if (frames.Count != 0) throw new Exception("invalid samples were accepted");
Console.WriteLine("Frame statistics tests passed.");

