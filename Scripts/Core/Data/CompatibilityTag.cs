using System.Linq;
using System.Text.Json.Serialization;
using Godot;
using Godot.Collections;

namespace CardCleaner.Scripts.Core.Data;

[Tool]
[GlobalClass]
public partial class CompatibilityTag : Resource
{
    public enum CompatibilityMode
    {
        None, // Use explicit CompatibleWith lists only
        Any, // Compatible with everything
        Self, // Compatible with identical tags AND listed tags
        Not // Compatible with tags that DON'T match this pattern
    }

    [Export] public string Tag { get; set; } = "";

    // Runtime object references - not serialized
    [Newtonsoft.Json.JsonIgnore]
    public Array<CompatibilityTag> CompatibleWith { get; set; } = new();

    // String names for JSON serialization
    [JsonPropertyName("compatibleWith")] public Array<string> CompatibleWithNames { get; set; } = new();

    [Export] public CompatibilityMode Mode { get; set; } = CompatibilityMode.Self;

    public bool IsCompatibleWith(CompatibilityTag other)
    {
        // Existing logic remains unchanged
        if (Mode == CompatibilityMode.Any || other.Mode == CompatibilityMode.Any)
            return true;

        if (Mode == CompatibilityMode.Self)
        {
            if (Tag == other.Tag) return true;
            if (CompatibleWith.Contains(other)) return true;
        }

        if (other.Mode == CompatibilityMode.Self)
        {
            if (other.Tag == Tag) return true;
            if (other.CompatibleWith.Contains(this)) return true;
        }

        if (Mode == CompatibilityMode.None && CompatibleWith.Contains(other))
            return true;
        if (other.Mode == CompatibilityMode.None && other.CompatibleWith.Contains(this))
            return true;

        if (Mode == CompatibilityMode.Not)
        {
            var otherMatchesPattern = other.Tag == Tag || CompatibleWith.Contains(other);
            return !otherMatchesPattern;
        }

        if (other.Mode == CompatibilityMode.Not)
        {
            var thisMatchesPattern = Tag == other.Tag || other.CompatibleWith.Contains(this);
            return !thisMatchesPattern;
        }

        return false;
    }

    public static bool IsArrayCompatibleWith(Array<CompatibilityTag> self, Array<CompatibilityTag> other)
    {
        if (self.Count == 0 || other.Count == 0) return self.Count == other.Count;
        return self.Any(selfTag => other.Any(selfTag.IsCompatibleWith));
    }
}