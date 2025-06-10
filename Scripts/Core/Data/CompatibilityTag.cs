using System.Linq;
using Godot;
using Godot.Collections;

namespace CardCleaner.Scripts.Core.Data;

[Tool]
[GlobalClass]
public partial class CompatibilityTag : Resource
{
    
    [Export] public string Tag { get; set; } = "";
    [Export] public Array<CompatibilityTag> CompatibleWith { get; set; } = new();
    [Export] public bool Any { get; set; }
    [Export] public bool Self { get; set; } = true;

    public bool IsCompatibleWith(CompatibilityTag other)
    {
        // Any test: if either tag is marked as Any we're always compatible
        if (Any || other.Any) 
            return true;
        // Self test: if the two tags are identical and we're set to be self-compatible
        if (other == this) 
            return Self;
        // Fast path: check if other tag is explicitly listed in our compatible tags
        if (CompatibleWith.Contains(other))
            return true;

        // Reciprocal check: check if we're listed in other's compatible tags  
        if (other.CompatibleWith.Contains(this))
            return true;

        // Fallback: if both compatibility lists are null/empty, compare tag strings
        bool ourListEmpty = CompatibleWith.Count == 0;
        bool theirListEmpty = other.CompatibleWith.Count == 0;

        if (ourListEmpty && theirListEmpty)
            return Tag.Equals(other.Tag);

        return false;
    }

    public static bool IsArrayCompatibleWith(Array<CompatibilityTag> self, Array<CompatibilityTag> other) => 
        self.Any(selfTag => other.Any(selfTag.IsCompatibleWith));
}