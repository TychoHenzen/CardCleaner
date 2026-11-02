using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CardCleaner.Scripts.Core.Interfaces;
using CardCleaner.Scripts.Core.Randomness;
using CardCleaner.Scripts.Features.Packs.Components;
using CardCleaner.Scripts.Features.Packs.Models;
using CardCleaner.Scripts.Features.Packs.Services;
using CardCleaner.Tests.TestUtilities;
using Godot;

namespace CardCleaner.Tests.Features.Packs.Components;

[TestSuite]
[RequireGodotRuntime]
public class CardContainerSeedTest
{
    private const string BoxScene = "res://Scenes/Shop/Items/CardboardBox.tscn";
    private const string BoosterScene = "res://Scenes/Shop/Items/Booster.tscn";
    private const ulong SeedA = 777;
    private const ulong SeedB = 778;
    private const ulong SeedSearchLimit = 20000;
    private const int OpenFrames = 12;

    private Node3D _world = null!;

    [BeforeTest]
    public void Setup()
    {
        _world = AddNode(new Node3D());
    }

    [TestCase]
    [TestCategory("Unit")]
    public async Task BoostersWithTheSameSeedOpenToIdenticalCards()
    {
        // Most boosters are all ordinary (all-zero) cards, which would match for any seed, so the test uses the
        // first seed whose booster holds a special card. The generator is seeded, so the search finds the same seed
        // on every run.
        var seed = FirstSeedWithSpecialBooster();
        var first = new RecordingCardSpawner();
        var second = new RecordingCardSpawner();

        SeededBooster(_world, seed, first).Open();
        await Frames(OpenFrames);
        SeededBooster(_world, seed, second).Open();
        await Frames(OpenFrames);

        AssertThat(first.Spawned.Count).IsEqual(CardContainerLayout.ItemsPerContainer);
        AssertBool(first.SpecialCount > 0).IsTrue();
        AssertThat(Keys(second)).IsEqual(Keys(first));
    }

    [TestCase]
    [TestCategory("Unit")]
    public async Task BoxesWithDifferentSeedsGiveTheirBoostersDifferentSeeds()
    {
        // The cards themselves are mostly all zero, so two boxes are compared by the seeds their boosters received.
        var first = await OpenWholeBox(SeedA);
        var second = await OpenWholeBox(SeedB);

        AssertThat(first.BoosterSeeds.Distinct().Count()).IsEqual(first.BoosterSeeds.Count);
        AssertBool(second.BoosterSeeds.Intersect(first.BoosterSeeds).Any()).IsFalse();
    }

    [TestCase]
    [TestCategory("Unit")]
    public async Task BoxWithTheSameSeedOpensToTheSameCardsOnEveryRun()
    {
        var firstRun = await OpenWholeBox(SeedA);
        var secondRun = await OpenWholeBox(SeedA);

        const int perBox = CardContainerLayout.ItemsPerContainer * CardContainerLayout.ItemsPerContainer
            * CardContainerLayout.ItemsPerContainer;
        AssertThat(firstRun.Cards.Spawned.Count).IsEqual(perBox);
        AssertThat(secondRun.BoosterSeeds).IsEqual(firstRun.BoosterSeeds);
        AssertThat(Keys(secondRun.Cards)).IsEqual(Keys(firstRun.Cards));
    }

    [TestCase]
    [TestCategory("Unit")]
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

    private sealed record OpenedBox(RecordingCardSpawner Cards, List<ulong> BoosterSeeds);

    // Opens a seeded box, then each pack and its boosters in tree order, and returns every card it spawned and the
    // seed of every booster it opened.
    private static async Task<OpenedBox> OpenWholeBox(ulong seed)
    {
        var world = AddNode(new Node3D());
        var opened = new OpenedBox(new RecordingCardSpawner(), new List<ulong>());
        var box = GD.Load<PackedScene>(BoxScene).Instantiate<CardContainer>();
        box.ContentSeed = seed;
        box.Spawning = opened.Cards;
        world.AddChild(box);

        box.Open();
        await Frames(OpenFrames);

        var packs = Containers(world, CardContainerKind.Pack);
        AssertThat(packs.Length).IsEqual(CardContainerLayout.ItemsPerContainer);
        foreach (var pack in packs)
        {
            pack.Open();
            await Frames(OpenFrames);

            foreach (var booster in Containers(world, CardContainerKind.Booster))
            {
                opened.BoosterSeeds.Add(booster.ContentSeed!.Value);
                booster.Open();
            }

            await Frames(OpenFrames);
        }

        return opened;
    }

    private static CardContainer[] Containers(Node3D parent, CardContainerKind kind) =>
        parent.GetChildren().OfType<CardContainer>().Where(c => c.Kind == kind && !c.IsOpened).ToArray();

    private static string Keys(RecordingCardSpawner spawner) =>
        string.Join(";", spawner.Spawned.Select(s => string.Join(",", s.Signature.Elements)));

    private static async Task Frames(int count)
    {
        for (var i = 0; i < count; i++)
            await ISceneRunner.SyncProcessFrame;
    }
}
