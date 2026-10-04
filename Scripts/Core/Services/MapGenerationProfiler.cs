using System;
using System.Collections.Generic;
using System.Diagnostics;
using CardCleaner.Scripts.Core.Interfaces;

namespace CardCleaner.Scripts.Core.Services;

public class MapGenerationProfiler : IProfiler
{
    private readonly Dictionary<string, ProfileStats> _stats = new();

    public IDisposable BeginScope(string name)
    {
        return new ScopedTimer(this, name);
    }

    public void RecordDuration(string name, long milliseconds)
    {
        if (!_stats.TryGetValue(name, out var stat))
        {
            stat = new ProfileStats();
            _stats[name] = stat;
        }
        stat.Record(milliseconds);
    }

    public Dictionary<string, ProfileStats> GetStatistics() => _stats;

    public void Clear() => _stats.Clear();

    public void PrintReport()
    {
        ILog.Print("=== Map Generation Profile ===");
        foreach (var (name, stats) in _stats)
        {
            ILog.Print(
                $"  {name}: {stats.TotalMs}ms total ({stats.Count}x, " +
                $"avg {stats.AverageMs:F1}ms, min {stats.MinMs}ms, max {stats.MaxMs}ms)");
        }
    }

    private sealed class ScopedTimer : IDisposable
    {
        private readonly MapGenerationProfiler _profiler;
        private readonly string _name;
        private readonly Stopwatch _sw;

        public ScopedTimer(MapGenerationProfiler profiler, string name)
        {
            _profiler = profiler;
            _name = name;
            _sw = Stopwatch.StartNew();
        }

        public void Dispose()
        {
            _sw.Stop();
            _profiler.RecordDuration(_name, _sw.ElapsedMilliseconds);
        }
    }
}
