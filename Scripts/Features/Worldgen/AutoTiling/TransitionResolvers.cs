using CardCleaner.Scripts.Core.Interfaces;

namespace CardCleaner.Scripts.Features.Worldgen.AutoTiling;

/// <summary>
/// Builds the transition resolver that the renderers share. The compiled atlas is loaded from the asset paths here,
/// so renderers receive an <see cref="ITransitionResolver"/> and never construct the concrete class.
/// </summary>
public static class TransitionResolvers
{
    public static ITransitionResolver Create(IAssetPaths paths) => new CompiledTransitionResolver(paths);
}
