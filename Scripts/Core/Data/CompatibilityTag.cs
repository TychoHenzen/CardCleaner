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
    
    /// <summary>
    /// Check if any tag in the first array is compatible with any tag in the second array
    /// </summary>
    public static bool IsArrayCompatibleWith(Array<CompatibilityTag>? array1, Array<CompatibilityTag>? array2)
    {
        // Empty arrays mean "accepts anything" 
        if (array1 == null || array1.Count == 0 || array2 == null || array2.Count == 0)
            return true;

        return array1.Any(tag1 => array2.Any(tag1.IsCompatibleWith));
    }
}