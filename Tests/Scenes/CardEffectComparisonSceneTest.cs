using System;
using System.Linq;
using System.Threading.Tasks;
using CardCleaner.Scripts.Core.DependencyInjection;
using CardCleaner.Scripts.Core.Enumeration;
using CardCleaner.Scripts.Core.ServiceProviders;
using CardCleaner.Scripts.Features.Card.Controllers;
using CardCleaner.Scripts.Features.Card.Models;
using CardCleaner.Scripts.Features.Card.Services;
using Godot;

namespace CardCleaner.Tests.Scenes;

/// <summary>
///     The comparison scene (F6 in the editor) spawns one real card for every rarity and tier through the normal
///     spawning service, and labels each one.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class CardEffectComparisonSceneTest
{
    private const string ScenePath = "res://Scenes/Debug/CardEffectComparison.tscn";
    private const int SettleFrames = 12;

    private Node3D _scene = null!;

    [BeforeTest]
    public async Task Setup()
    {
        _scene = GD.Load<PackedScene>(ScenePath).Instantiate<Node3D>();

        // The test scene is not the current scene, so hand the scene-owned services over directly.
        ServiceLocator.ResetForTesting();
        ServiceLocator.Container.RegisterSingleton(new RandomNumberGenerator { Seed = 1 });
        _scene.GetNode<CardServiceProvider>("Services/CardServiceProvider").RegisterServices(ServiceLocator.Container);
        _scene.GetNode<CardSpawningService>("Services/SpawningService").RegisterServices(ServiceLocator.Container);

        AddNode(_scene);
        for (var i = 0; i < SettleFrames; i++)
            await Engine.GetMainLoop().ToSignal(Engine.GetMainLoop(), SceneTree.SignalName.ProcessFrame);
    }

    [AfterTest]
    public static void Teardown()
    {
        ServiceLocator.ResetForTesting();
    }

    [TestCase]
    [TestCategory("Unit")]
    public void TheSceneHoldsFifteenCards()
    {
        var cards = _scene.GetNode("Cards").GetChildren().OfType<CardController>().ToArray();

        AssertThat(cards.Length).IsEqual(15);
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
    public void EveryCardShowsTheEffectsOfItsCell()
    {
        foreach (var rarity in Enum.GetValues<CardRarity>())
        foreach (var tier in Enum.GetValues<IntensityTier>())
        {
            var card = _scene.GetNode<CardController>($"Cards/{rarity}_{tier}");
            var mesh = card.GetNode<MeshInstance3D>("OuterBox_Baked");
            var material = (ShaderMaterial)mesh.MaterialOverride;

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
}
