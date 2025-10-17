using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace Hkmp.Util;

/// <summary>
/// High-performance monitoring system for tracking metrics with minimal overhead.
/// </summary>
internal sealed class PerformanceMonitor {
    private static readonly Lazy<PerformanceMonitor> _instance = new(() => new PerformanceMonitor());
    public static PerformanceMonitor Instance => _instance.Value;

    private readonly ConcurrentDictionary<string, MetricData> _metrics;
    private readonly Stopwatch _uptime;

    private sealed class MetricData {
        public long Count;
        public long TotalTicks;
        public long MinTicks = long.MaxValue;
        public long MaxTicks;
        public long LastTicks;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Record(long ticks) {
            Count++;
            TotalTicks += ticks;
            LastTicks = ticks;

            if (ticks < MinTicks) MinTicks = ticks;
            if (ticks > MaxTicks) MaxTicks = ticks;
        }

        public double AverageMs => Count > 0 ? (TotalTicks / (double)Count) / TimeSpan.TicksPerMillisecond : 0;
        public double MinMs => MinTicks / (double)TimeSpan.TicksPerMillisecond;
        public double MaxMs => MaxTicks / (double)TimeSpan.TicksPerMillisecond;
        public double LastMs => LastTicks / (double)TimeSpan.TicksPerMillisecond;
    }

    private PerformanceMonitor() {
        _metrics = new ConcurrentDictionary<string, MetricData>();
        _uptime = Stopwatch.StartNew();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public IDisposable Measure(string operationName) {
        return new MetricScope(this, operationName);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void RecordMetric(string name, long ticks) {
        var metric = _metrics.GetOrAdd(name, _ => new MetricData());
        metric.Record(ticks);
    }

    public string GetReport() {
        var report = $"Performance Report (Uptime: {_uptime.Elapsed:hh\\:mm\\:ss})\n";
        report += "===================================================\n";
        report += $"{"Operation",-30} {"Count",10} {"Avg(ms)",12} {"Min(ms)",12} {"Max(ms)",12} {"Last(ms)",12}\n";
        report += "---------------------------------------------------\n";

        foreach (var kvp in _metrics) {
            var metric = kvp.Value;
            report += $"{kvp.Key,-30} {metric.Count,10} {metric.AverageMs,12:F3} {metric.MinMs,12:F3} {metric.MaxMs,12:F3} {metric.LastMs,12:F3}\n";
        }

        return report;
    }

    public void Reset() {
        _metrics.Clear();
        _uptime.Restart();
    }

    private readonly struct MetricScope : IDisposable {
        private readonly PerformanceMonitor _monitor;
        private readonly string _operationName;
        private readonly long _startTicks;

        public MetricScope(PerformanceMonitor monitor, string operationName) {
            _monitor = monitor;
            _operationName = operationName;
            _startTicks = Stopwatch.GetTimestamp();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Dispose() {
            var elapsed = Stopwatch.GetTimestamp() - _startTicks;
            _monitor.RecordMetric(_operationName, elapsed);
        }
    }
}

/// <summary>
/// Extension methods for performance monitoring.
/// </summary>
internal static class PerformanceExtensions {
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static IDisposable MeasurePerformance(this string operationName) {
        return PerformanceMonitor.Instance.Measure(operationName);
    }
}
