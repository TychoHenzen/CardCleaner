using CardCleaner.Scripts.Core.Interfaces;

namespace CardCleaner.Scripts.Core.Services;

public static class SafePositionTrackers
{
    public static ISafePositionTracker Create() => new SafePositionTracker();
}
