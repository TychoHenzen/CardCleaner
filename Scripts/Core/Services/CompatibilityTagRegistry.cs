using System.Collections.Generic;
using CardCleaner.Scripts.Core.Interfaces;

public class CompatibilityTagRegistry
{
    private static CompatibilityTagRegistry? _instance;
    public static CompatibilityTagRegistry Instance => _instance ??= new CompatibilityTagRegistry();

    private readonly Dictionary<string, CompatibilityTag> _tags = new();

    public void RegisterTag(CompatibilityTag tag)
    {
        _tags[tag.Tag] = tag;
    }

    public CompatibilityTag? GetTag(string name)
    {
        return _tags.GetValueOrDefault(name);
    }

    public bool IsCompatible(string tag1Name, string tag2Name)
    {
        var tag1 = GetTag(tag1Name);
        var tag2 = GetTag(tag2Name);

        return tag1 != null && tag2 != null && tag1.IsCompatibleWith(tag2);
    }

    public void Clear()
    {
        _tags.Clear();
    }

    // Resolve all string references to object references
    public void ResolveReferences()
    {
        foreach (var tag in _tags.Values)
        {
            tag.CompatibleWith.Clear();
            foreach (var refName in tag.CompatibleWithNames)
            {
                var referencedTag = GetTag(refName);
                if (referencedTag != null)
                    tag.CompatibleWith.Add(referencedTag);
                else
                    ILog.Warning($"Tag '{tag.Tag}' references unknown tag '{refName}'");
            }
        }
    }
}