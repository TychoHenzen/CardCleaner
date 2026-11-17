using CardCleaner.Scripts.Core.Resources;
using Godot;

namespace CardCleaner.Tests.Core.Resources;

/// <summary>
///     A C#-scripted resource that only <see cref="ScriptedResourceRevivalTest" /> loads, so no other suite holds its
///     wrapper or its native object when that suite lets go of it.
/// </summary>
public partial class WrapperProbe : PinnedResource
{
    [Export]
    public int Value { get; set; }
}
