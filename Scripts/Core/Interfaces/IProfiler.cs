using System;
using System.Collections.Generic;

namespace CardCleaner.Scripts.Core.Interfaces;

public interface IProfiler
{
    IDisposable BeginScope(string name);
    void RecordDuration(string name, long milliseconds);
    Dictionary<string, ProfileStats> GetStatistics();
    void Clear();
}

public class ProfileStats
{
    public long TotalMs { get; set; }
    public int Count { get; set; }
    public long MinMs { get; set; } = long.MaxValue;
    public long MaxMs { get; set; }

    public void Record(long ms)
    {
        TotalMs += ms;
        Count++;
        if (ms < MinMs) MinMs = ms;
        if (ms > MaxMs) MaxMs = ms;
    }

    public double AverageMs => Count > 0 ? TotalMs / (double)Count : 0;
}

public class NoOpProfiler : IProfiler
{
    private sealed class EmptyDisposable : IDisposable
    {
        public void Dispose() { }
    }

    private static readonly IDisposable EmptyDisposableInstance = new EmptyDisposable();

    public IDisposable BeginScope(string name) => EmptyDisposableInstance;
    public void RecordDuration(string name, long milliseconds) { }
    public Dictionary<string, ProfileStats> GetStatistics() => new();
    public void Clear() { }
}
