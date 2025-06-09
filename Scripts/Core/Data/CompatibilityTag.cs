using System.Linq;
using Godot;
using Godot.Collections;

namespace CardCleaner.Scripts.Core.Data;

public partial class CompatibilityTag : Resource
{
    [Export] public string Tag { get; set; } = "";
    [Export] public Array<CompatibilityTag>? CompatibleWith { get; set; }
    
    public bool IsCompatibleWith(CompatibilityTag other)
    {
        // Fast path: check if other tag is explicitly listed in our compatible tags
        if (CompatibleWith?.Contains(other) == true)
            return true;
    
        // Reciprocal check: check if we're listed in other's compatible tags  
        if (other.CompatibleWith?.Contains(this) == true)
            return true;
    
        // Fallback: if both compatibility lists are null/empty, compare tag strings
        bool ourListEmpty = CompatibleWith == null || CompatibleWith.Count == 0;
        bool theirListEmpty = other.CompatibleWith == null || other.CompatibleWith.Count == 0;
    
        if (ourListEmpty && theirListEmpty)
            return Tag == other.Tag;
    
        return false;
    }
}