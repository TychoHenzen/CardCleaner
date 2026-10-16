using CardCleaner.Scripts.Core.Interfaces;

namespace CardCleaner.Scripts.Core.Services;

public static class VisibilityCheckers
{
    public static IVisibilityChecker CreateSimple() => new SimpleVisibilityChecker();
}
