using CardCleaner.Scripts.Core.Data;

namespace CardCleaner.Scripts.Core.Interfaces;

public interface ICompatibilityTagRegistry
{
    void RegisterTag(CompatibilityTag tag);
    CompatibilityTag? GetTag(string name);
    bool IsCompatible(string tag1Name, string tag2Name);
    void Clear();
    void ResolveReferences();
}
