using System.Linq;
using CardCleaner.Scripts.Features.Worldgen.AutoTiling;
using CardCleaner.Scripts.Features.Worldgen;
using CardCleaner.Scripts.Core.Services;

namespace CardCleaner.Tests.TestUtilities;

/// <summary>The game's transition pairs, built the way the composition root builds them.</summary>
public static class CompiledTransitionPairs
{
    public static TransitionPairs Load() =>
        new(new CompiledTransitionResolver(new GodotAssetPaths()).GetAllTransitionPairs().ToList());
}
