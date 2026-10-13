using System.Collections.Generic;

namespace CardCleaner.Tests.Architecture.DependencyGate;

/// <summary>The middle-layer partition: a module id per clustered class, and the loose classes.</summary>
internal sealed record ConsensusResult(SortedDictionary<string, int> Module, SortedSet<string> Loose);
