using System;
using System.Collections.Generic;

namespace CardCleaner.Scripts.Core.Interfaces;

internal sealed class NoOpProfiler : IProfiler
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
