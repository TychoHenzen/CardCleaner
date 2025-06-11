using System.Linq;
using Godot;
using Godot.Collections;

namespace CardCleaner.Scripts.Core.Data;

[Tool]
[GlobalClass]
public partial class CompatibilityTag : Resource
{
    public enum CompatibilityMode
    {
        None,   // Use explicit CompatibleWith lists only
        Any,    // Compatible with everything
        Self,   // Compatible only with identical tags
        Not     // Compatible with tags that DON'T match this pattern
    }

    [Export] public string Tag { get; set; } = "";
    [Export] public Array<CompatibilityTag> CompatibleWith { get; set; } = new();
    [Export] public CompatibilityMode Mode { get; set; } = CompatibilityMode.Self;

    public bool IsCompatibleWith(CompatibilityTag other)
    {
        // Handle Any mode first - highest priority
        if (Mode == CompatibilityMode.Any || other.Mode == CompatibilityMode.Any)
            return true;

        // Handle Not mode - check if other tag matches our exclusion pattern
        if (Mode == CompatibilityMode.Not)
        {
            bool otherMatchesOurPattern = other.Tag.Equals(Tag) || 
                                         CompatibleWith.Contains(other) || 
                                         other.CompatibleWith.Contains(this);
            return !otherMatchesOurPattern;
        }

        // If other tag is Not mode, let it handle the logic
        if (other.Mode == CompatibilityMode.Not)
        {
            return other.IsCompatibleWith(this);
        }

        // Handle Self mode - only compatible with identical tags
        if (Mode == CompatibilityMode.Self && other == this)
            return true;

        // Handle None mode and explicit compatibility lists
        if (CompatibleWith.Contains(other))
            return true;

        if (other.CompatibleWith.Contains(this))
            return true;

        // Fallback for None mode: if both have empty lists, compare tag strings
        bool ourListEmpty = CompatibleWith.Count == 0;
        bool theirListEmpty = other.CompatibleWith.Count == 0;

        if (Mode == CompatibilityMode.None && other.Mode == CompatibilityMode.None && 
            ourListEmpty && theirListEmpty)
            return Tag.Equals(other.Tag);

        return false;
    }

    public static bool IsArrayCompatibleWith(Array<CompatibilityTag> self, Array<CompatibilityTag> other)
    {
        //if the list is empty, treat it as "any"
        if(self.Count == 0 || other.Count == 0) return self == other;
        return self.Any(selfTag => other.Any(selfTag.IsCompatibleWith));
    }
}