namespace CardCleaner.Scripts.Features.Worldgen.AutoTiling.Transitions;

/// <summary>
/// The components of a transition lookup key.
/// </summary>
internal readonly record struct TransitionKey(string BorderId, string OuterTerrain);
