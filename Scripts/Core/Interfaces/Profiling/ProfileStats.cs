namespace CardCleaner.Scripts.Core.Interfaces;

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
