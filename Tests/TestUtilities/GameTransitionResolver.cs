using System;
using CardCleaner.Scripts.Core.Services;
using CardCleaner.Scripts.Features.Worldgen.AutoTiling;

namespace CardCleaner.Tests.TestUtilities;

/// <summary>
///     The game's transition resolver, parsed from the 2 MB transition_map.json once per test run. A resolver only
///     reads its map after construction, so suites share this one instead of parsing the file before every case, as
///     the composition root's lazy registration shares one across the game.
/// </summary>
public static class GameTransitionResolver
{
    private static readonly Lazy<CompiledTransitionResolver> Instance =
        new(() => new CompiledTransitionResolver(new GodotAssetPaths()));

    public static CompiledTransitionResolver Shared => Instance.Value;
}
