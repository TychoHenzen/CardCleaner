using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CardCleaner.Scripts.Core.Data;
using CardCleaner.Scripts.Core.DependencyInjection;
using CardCleaner.Scripts.Core.Enumeration;
using CardCleaner.Scripts.Core.ServiceProviders;
using CardCleaner.Scripts.Features.Card.Controllers;
using CardCleaner.Scripts.Features.Card.Debug;
using CardCleaner.Scripts.Features.Card.Services;
using CardCleaner.Tests.Features.Card.Services;
using Godot;
using CardCleaner.Scripts.Features.Card.Models.Effects;

namespace CardCleaner.Tests.Scenes;

/// <summary>
///     The comparison scene (F6 in the editor) spawns one real card for every rarity and tier through the normal
///     spawning service, labels each one and frames them all in the window. Keys 2 and 3 add batches of 10 and 100
///     cards to watch the frame time, with and without the effects. The scene runs in a sub-viewport here so the
///     tests can resize its window.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class CardEffectComparisonSceneTest
{
    private const string ScenePath = "res://Scenes/Debug/CardEffectComparison.tscn";
    private const int SettleFrames = 12;

    private static readonly int ArtLayerIndex = ArtIndexOf(new CardTemplate());

    private SubViewport _window = null!;
    private CardEffectComparison _scene = null!;

    [BeforeTest]
    public async Task Setup()
    {
        _scene = GD.Load<PackedScene>(ScenePath).Instantiate<CardEffectComparison>();

        // The test scene is not the current scene, so hand the scene-owned services over directly.
        ServiceLocator.ResetForTesting();
        ServiceLocator.Container.RegisterSingleton(new RandomNumberGenerator { Seed = 1 });
        _scene.GetNode<CardServiceProvider>("Services/CardServiceProvider").RegisterServices(ServiceLocator.Container);
        _scene.GetNode<CardSpawningService>("Services/SpawningService").RegisterServices(ServiceLocator.Container);

        _window = new SubViewport { Size = new Vector2I(1280, 720) };
        _window.AddChild(_scene);
        AddNode(_window);
        await CardEffectBakeWait.Frames(SettleFrames);
    }

    [AfterTest]
    public static void Teardown()
    {
        ServiceLocator.ResetForTesting();
        CardEffectNormalMapCache.ResetForTesting();
    }

    [TestCase]
    [TestCategory("Unit")]
    public void TheSceneHoldsFifteenCards()
    {
        AssertThat(Cards("Cards").Length).IsEqual(15);
    }

    [TestCase]
    [TestCategory("Unit")]
    public void EveryRarityAndTierHasALabelledCard()
    {
        foreach (var rarity in Enum.GetValues<CardRarity>())
        foreach (var tier in Enum.GetValues<IntensityTier>())
        {
            var label = _scene.GetNodeOrNull<Label3D>($"Labels/{rarity}_{tier}");

            AssertThat(label).OverrideFailureMessage($"no label for {rarity} / {tier}").IsNotNull();
            AssertThat(label!.Text).Contains(rarity.ToString());
            AssertThat(label.Text).Contains(tier.ToString());
        }
    }

    [TestCase]
    [TestCategory("Unit")]
    public async Task EveryCardShowsTheEffectsOfItsCell()
    {
        await AssertEveryCellShowsItsEffects();
    }

    private async Task AssertEveryCellShowsItsEffects()
    {
        await WaitForEffects("Cards");

        foreach (var rarity in Enum.GetValues<CardRarity>())
        foreach (var tier in Enum.GetValues<IntensityTier>())
        {
            var material = Material(_scene.GetNode<CardController>($"Cards/{rarity}_{tier}"))!;

            AssertThat((int)material.GetShaderParameter("rarity_effect"))
                .OverrideFailureMessage($"{rarity} / {tier} rarity effect")
                .IsEqual((int)CardEffectMapping.RarityEffectFor(rarity));
            AssertThat((int)material.GetShaderParameter("condition_effect"))
                .OverrideFailureMessage($"{rarity} / {tier} condition effect")
                .IsEqual((int)CardEffectMapping.ConditionEffectFor(tier));
        }
    }

    [TestCase]
    [TestCategory("Unit")]
    public void TheCardsStayWhereTheyAreLaidOut()
    {
        var card = _scene.GetNode<CardController>($"Cards/{CardRarity.Epic}_{IntensityTier.Active}");

        AssertThat(card.Freeze).IsTrue();
    }

    [TestCase]
    [TestCategory("Unit")]
    public void EveryCardIsInViewAndLargeEnoughToCompare()
    {
        foreach (var card in Cards("Cards"))
        {
            AssertInView(card);
            AssertThat(ProjectedHeight(card)).OverrideFailureMessage($"{card.Name} is too small to compare")
                .IsGreaterEqual(0.18f * _window.Size.Y);
        }
    }

    [TestCase]
    [TestCategory("Unit")]
    public async Task TheGridIsRefittedWhenTheWindowChangesShape()
    {
        var before = _window.GetCamera3D().Position;

        _window.Size = new Vector2I(600, 1000);
        await CardEffectBakeWait.Frames(2);

        AssertThat(_window.GetCamera3D().Position).IsNotEqual(before);
        foreach (var card in Cards("Cards"))
            AssertInView(card);
    }

    [TestCase]
    [TestCategory("Unit")]
    public void TheRandomCardKeysAreSwitchedOff()
    {
        AssertThat(_scene.GetNode<CardSpawner>("Cards").SpawnKeys).IsFalse();
    }

    [TestCase]
    [TestCategory("Unit")]
    public async Task BatchesOfTenAndAHundredAddFrozenCardsThatStayInView()
    {
        _scene.SpawnBatch(10);
        _scene.SpawnBatch(100);
        await WaitForEffects("Batch");

        var batch = Cards("Batch");
        AssertThat(batch.Length).IsEqual(110);
        foreach (var card in batch.Concat(Cards("Cards")))
        {
            AssertThat(card.Freeze).IsTrue();
            AssertInView(card);
        }

        _scene.ClearBatch();
        await CardEffectBakeWait.Frames(2);
        AssertThat(Cards("Batch").Length).IsEqual(0);
    }

    [TestCase]
    [TestCategory("Unit")]
    public async Task KeyTwoAddsTenCardsAndKeyESwitchesTheEffectsOff()
    {
        _window.PushInput(new InputEventKey { Keycode = Key.Key2, Pressed = true });
        await WaitForEffects("Batch");
        _window.PushInput(new InputEventKey { Keycode = Key.E, Pressed = true });

        AssertThat(Cards("Batch").Length).IsEqual(10);
        AssertThat(_scene.EffectsEnabled).IsFalse();
    }

    [TestCase]
    [TestCategory("Unit")]
    public async Task AHundredCardsBakeEachArtOnceAndKeepTheirSeeds()
    {
        _scene.SpawnBatch(100);
        await WaitForEffects("Batch");
        await WaitForEffects("Cards");

        var cards = Cards("Batch").Concat(Cards("Cards")).ToArray();
        var withRarity = cards.Select(Material).Where(m => (int)m!.GetShaderParameter("rarity_effect") != 0).ToArray();
        var normalMapsPerArt = withRarity
            .GroupBy(m => ArtTexture(m!).GetInstanceId())
            .Select(group => group.Select(m => NormalMap(m!).GetInstanceId()).Distinct().Count())
            .ToArray();
        var seedsPerSignature = cards
            .GroupBy(card => card.Signature.ToDebugString())
            .Select(group => group.Select(card => (float)Material(card)!.GetShaderParameter("art_seed")).Distinct())
            .ToArray();

        // One bevel map per art texture, however many cards show it: the cache is hit, not re-baked per card.
        AssertThat(normalMapsPerArt.Length).IsLess(withRarity.Length);
        AssertThat(normalMapsPerArt.All(count => count == 1)).IsTrue();
        // The same signature always gets the same seed, and the fifteen signatures get fifteen different seeds.
        AssertThat(seedsPerSignature.Length).IsEqual(15);
        AssertThat(seedsPerSignature.All(seeds => seeds.Count() == 1)).IsTrue();
        AssertThat(seedsPerSignature.Select(seeds => seeds.Single()).Distinct().Count()).IsEqual(15);
    }

    [TestCase]
    [TestCategory("Unit")]
    public async Task TheEffectsCanBeSwitchedOffAndBackOnToCompareFrameTimes()
    {
        // Switch off only once every bevel map is in, so no map arrives while the effects are off.
        await WaitForEffects("Cards");
        _scene.SetEffectsEnabled(false);
        AssertThat(_scene.EffectsEnabled).IsFalse();
        foreach (var material in Cards("Cards").Select(Material))
        {
            AssertThat((int)material!.GetShaderParameter("rarity_effect")).IsEqual(0);
            AssertThat((int)material.GetShaderParameter("condition_effect")).IsEqual(0);
        }

        _scene.SetEffectsEnabled(true);
        AssertThat(_scene.EffectsEnabled).IsTrue();
        await AssertEveryCellShowsItsEffects();
    }

    private CardController[] Cards(string parent)
    {
        return _scene.GetNode(parent).GetChildren().OfType<CardController>()
            .Where(card => !card.IsQueuedForDeletion()).ToArray();
    }

    private static ShaderMaterial? Material(CardController card)
    {
        return card.GetNodeOrNull<MeshInstance3D>("OuterBox_Baked")?.MaterialOverride as ShaderMaterial;
    }

    private static int ArtIndexOf(CardTemplate template)
    {
        return Array.IndexOf(template.GatherAllLayers(), template.Art);
    }

    private static Texture2D ArtTexture(ShaderMaterial material)
    {
        return material.GetShaderParameter("textures").AsGodotArray()[ArtLayerIndex].As<Texture2D>();
    }

    private static Texture2D NormalMap(ShaderMaterial material)
    {
        return material.GetShaderParameter("art_normal_map").As<Texture2D>();
    }

    private float ProjectedHeight(Node3D card)
    {
        var half = Vector3.Up * CardEffectComparisonLayout.CardHeight / 2f;
        var camera = _window.GetCamera3D();
        var bottom = camera.UnprojectPosition(card.GlobalPosition - half);
        var top = camera.UnprojectPosition(card.GlobalPosition + half);
        return bottom.Y - top.Y;
    }

    private void AssertInView(Node3D card)
    {
        var camera = _window.GetCamera3D();
        var view = new Rect2(Vector2.Zero, _window.Size);
        var corners = new List<Vector3>();
        foreach (var x in new[] { -1f, 1f })
        foreach (var y in new[] { -1f, 1f })
            corners.Add(card.GlobalPosition + new Vector3(x * CardEffectComparisonLayout.CardWidth,
                y * CardEffectComparisonLayout.CardHeight, 0f) / 2f);

        foreach (var corner in corners)
            AssertThat(!camera.IsPositionBehind(corner) && view.HasPoint(camera.UnprojectPosition(corner)))
                .OverrideFailureMessage($"{card.Name} is not fully in view").IsTrue();
    }

    /// <summary>
    ///     Waits until every card under <paramref name="parent" /> has its material and shows its rarity effect, which
    ///     waits for the art's bevel map to be baked off the main thread.
    /// </summary>
    private Task WaitForEffects(string parent)
    {
        return CardEffectBakeWait.Until(() => Cards(parent).All(ShowsItsEffects), $"every {parent} card's effects");
    }

    private static bool ShowsItsEffects(CardController card)
    {
        var material = Material(card);
        if (material == null) return false;

        var expected = CardEffectMapping.RarityEffectFor(SignatureCardHelper.DetermineRarity(new[] { card.Signature }));
        // A card whose art is still baking shows no rarity effect; it switches on together with the bevel map.
        return (int)material.GetShaderParameter("rarity_effect") == (int)expected;
    }

}
