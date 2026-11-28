using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CardCleaner.Scripts.Core.Interfaces;
using CardCleaner.Scripts.Core.Randomness;
using CardCleaner.Scripts.Features.Packs.Components;
using CardCleaner.Scripts.Features.Packs.Models;
using CardCleaner.Scripts.Features.Packs.Services;
using CardCleaner.Tests.TestUtilities;
using CardCleaner.Tests.TestUtilities.Helpers;
using Godot;

namespace CardCleaner.Tests.Features.Packs.Components;

[TestSuite]
[RequireGodotRuntime]
public class CardContainerSeedTest
{
    private const string PackScene = "res://Scenes/Shop/Items/Pack.tscn";
    private const string BoosterScene = "res://Scenes/Shop/Items/Booster.tscn";
    private const ulong SeedA = 777;
    private const ulong SeedB = 778;
    private const ulong SeedSearchLimit = 20000;

    private Node3D _world = null!;

    [BeforeTest]
    public void Setup()
    {
        _world = AddNode(new Node3D());
    }

    [TestCase]
    public async Task BoostersWithTheSameSeedOpenToIdenticalCards()
    {
        // Most boosters are all ordinary (all-zero) cards, which would match for any seed, so the test uses the
        // first seed whose booster holds a special card. The generator is seeded, so the search finds the same seed
        // on every run.
        var seed = FirstSeedWithSpecialBooster();
        var first = new RecordingCardSpawner();
        var second = new RecordingCardSpawner();
        var hand = InHandOpening.AddHand(_world);

        InHandOpening.Open(SeededBooster(_world, seed, first), hand);
        InHandOpening.Open(SeededBooster(_world, seed, second), hand);
        await BoundedWait.ProcessFrames(1);

        AssertThat(first.Spawned.Count).IsEqual(CardContainerLayout.ItemsPerContainer);
        AssertBool(first.SpecialCount > 0).IsTrue();
        AssertThat(Keys(second)).IsEqual(Keys(first));
    }

    [TestCase]
    public static async Task BoosterBoxesWithDifferentSeedsGiveTheirBoostersDifferentSeeds()
    {
        // The cards themselves are mostly all zero, so two booster boxes are compared by the seeds their boosters
        // received.
        var first = await OpenWholePack(SeedA);
        var second = await OpenWholePack(SeedB);

        AssertThat(first.BoosterSeeds.Distinct().Count()).IsEqual(first.BoosterSeeds.Count);
        AssertBool(second.BoosterSeeds.Intersect(first.BoosterSeeds).Any()).IsFalse();
    }

    [TestCase]
    public static async Task ABoosterBoxWithTheSameSeedOpensToTheSameCardsOnEveryRun()
    {
        var firstRun = await OpenWholePack(SeedA);
        var secondRun = await OpenWholePack(SeedA);

        const int perPack = CardContainerLayout.ItemsPerContainer * CardContainerLayout.ItemsPerContainer;
        AssertThat(firstRun.Cards.Spawned.Count).IsEqual(perPack);
        AssertThat(secondRun.BoosterSeeds).IsEqual(firstRun.BoosterSeeds);
        AssertThat(Keys(secondRun.Cards)).IsEqual(Keys(firstRun.Cards));
    }

    [TestCase]
    public void UnseededContainerDrawsFromTheSharedSourceOnlyWhenItIsSaved()
    {
        TestServices.Reset();
        var shared = RandomSources.Create(SeedA);
        var reference = RandomSources.Create(SeedA);
        TestServices.Container.RegisterSingleton<IRandom>(shared);
        var booster = TestServices.Inject(GD.Load<PackedScene>(BoosterScene).Instantiate<CardContainer>());

        _world.AddChild(booster);

        // Starting up draws nothing, so opening unseeded boxes keeps the shared source's draw order.
        AssertThat(booster.ContentSeed).IsNull();
        AssertThat(shared.Randi()).IsEqual(reference.Randi());

        var seed = booster.SeedForSave();

        // Saving draws exactly one seed, and asking again returns it without another draw.
        AssertBool(seed.HasValue).IsTrue();
        AssertThat(booster.SeedForSave()!.Value).IsEqual(seed!.Value);
        AssertThat(shared.Randi()).IsEqual(DrawAfterSeed(reference));
        TestServices.Reset();
    }

    private static uint DrawAfterSeed(IRandom reference)
    {
        reference.Randi();
        reference.Randi();
        return reference.Randi();
    }

    private static ulong FirstSeedWithSpecialBooster()
    {
        for (ulong seed = 1; seed < SeedSearchLimit; seed++)
        {
            var cards = new CardPackGenerator(RandomSources.Create(seed)).OpenBooster();
            if (cards.Any(card => card.HasMagicalPotential()))
                return seed;
        }

        throw new System.InvalidOperationException($"No booster seed below {SeedSearchLimit} holds a special card.");
    }

    private static CardContainer SeededBooster(Node3D parent, ulong seed, RecordingCardSpawner spawner)
    {
        var booster = GD.Load<PackedScene>(BoosterScene).Instantiate<CardContainer>();
        booster.ContentSeed = seed;
        booster.Spawning = spawner;
        parent.AddChild(booster);
        return booster;
    }

    private sealed record OpenedPack(RecordingCardSpawner Cards, List<ulong> BoosterSeeds);

    // Empties a seeded booster box into the hand, then opens its boosters in the order they came out, and returns
    // every card they spawned and the seed of every booster. A cardboard box is storage and never opens whole, so the
    // chain starts at a booster box.
    private static async Task<OpenedPack> OpenWholePack(ulong seed)
    {
        var world = AddNode(new Node3D());
        var hand = InHandOpening.AddHand(world);
        var opened = new OpenedPack(new RecordingCardSpawner(), new List<ulong>());
        var pack = GD.Load<PackedScene>(PackScene).Instantiate<CardContainer>();
        pack.ContentSeed = seed;
        pack.Spawning = opened.Cards;
        world.AddChild(pack);

        foreach (var booster in InHandOpening.Empty(pack, hand))
        {
            opened.BoosterSeeds.Add(booster.ContentSeed!.Value);
            InHandOpening.Open(booster, hand);
        }

        await BoundedWait.ProcessFrames(1);
        return opened;
    }

    private static string Keys(RecordingCardSpawner spawner) =>
        string.Join(";", spawner.Spawned.Select(s => string.Join(",", s.Signature.Elements)));

}
