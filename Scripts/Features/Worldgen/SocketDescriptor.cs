using System.Linq;
using CardCleaner.Scripts.Core.Data;
using CardCleaner.Scripts.Core.Enum;
using Godot;

[Tool]
[GlobalClass]
public partial class SocketDescriptor : Resource
{
    [Export] public SocketType BiomeType { get; set; } = SocketType.None;
    [Export] public Godot.Collections.Array<CompatibilityTag> CompatibilityTags { get; set; } = new();
    [Export] public bool AcceptsAny { get; set; } = false;
    
    private uint _cachedHash;
    private bool _hashValid = false;
    
    public bool IsCompatibleWith(SocketDescriptor other)
    {
        if (AcceptsAny || other.AcceptsAny) return true;
        
        // Biome-level compatibility
        if (BiomeType != other.BiomeType) return false;
        
        // Tag-level compatibility - any shared tag = compatible
        return CompatibilityTags.Any(tag => other.CompatibilityTags.Contains(tag));
    }
    
    private uint CalculateHash()
    {
        uint hash = (uint)BiomeType;
        foreach (var tag in CompatibilityTags)
        {
            hash ^= (uint)tag.GetHashCode();
        }
        return hash;
    }
    
    public uint GetCompatibilityHash()
    {
        if (!_hashValid)
        {
            _cachedHash = CalculateHash();
            _hashValid = true;
        }
        return _cachedHash;
    }
}