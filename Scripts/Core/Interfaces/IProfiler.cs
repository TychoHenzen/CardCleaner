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
