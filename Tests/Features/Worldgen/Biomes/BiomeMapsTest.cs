using System;
using CardCleaner.Scripts.Core.Interfaces;
using CardCleaner.Scripts.Core.Randomness;
using CardCleaner.Scripts.Core.Services;
using CardCleaner.Scripts.Features.Card.Models;
using CardCleaner.Scripts.Features.Worldgen;
using CardCleaner.Scripts.Features.Worldgen.Biomes;
using Godot;

namespace CardCleaner.Tests.Features.Worldgen.Biomes;

[TestSuite]
[RequireGodotRuntime]
public class BiomeMapsTest
{
    private static readonly Vector2I MapSize = new(20, 20);
    private static readonly Vector2I[] Positions = { new(0, 0), new(10, 10), new(19, 19) };

    private BiomeRegistry _registry = null!;
    private IRandom _rng = null!;

    [BeforeTest]
    public void Setup()
    {
        _registry = new BiomeRegistry();
        _registry.RegisterDefaultBiomes(new GodotAssetPaths());
        _rng = RandomSources.Create();
        _rng.Seed = 12345;
    }

    [TestCase]
    [TestCategory("Unit")]
    public void CreateGivesTheSameBiomesAsTheConcreteClass()
    {
        var set = _registry.ToBiomeSet();
        var signatureAt = Gradients.FromCards(
            new[] { new CardSignature(new[] { 0.5f, 0.5f, 0f, 0f, 0f, 0f, 0f, 0f }) },
            _rng).GetSignatureAt;

        var created = BiomeMaps.Create(set, signatureAt, MapSize);
        var concrete = new BiomeMapGenerator(set, signatureAt, MapSize);

        foreach (var position in Positions)
            AssertString(created.GetBiomeAt(position).Id).IsEqual(concrete.GetBiomeAt(position).Id);
    }

    [TestCase]
    [TestCategory("Unit")]
    public void CreateRejectsANullSampler()
    {
        var set = _registry.ToBiomeSet();

        Assertions.AssertThrown(() => BiomeMaps.Create(set, null!, MapSize))
            .IsInstanceOf<ArgumentNullException>();
    }
}
