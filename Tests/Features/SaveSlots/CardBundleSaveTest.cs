using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CardCleaner.Scripts.Features.Card.Models;
using CardCleaner.Scripts.Features.Card.Services.SaveGame;
using CardCleaner.Scripts.Features.CardStacks.Components;
using CardCleaner.Scripts.Features.CardStacks.Models;
using CardCleaner.Scripts.Features.Packs.Models;
using CardCleaner.Tests.Features.SaveSlots.Services;
using CardCleaner.Tests.TestUtilities;
using CardCleaner.Tests.TestUtilities.Helpers;
using GdUnit4;
using Godot;

namespace CardCleaner.Tests.Features.SaveSlots;

/// <summary>
///     Saves a strapped deck and a card pile through the save service and reloads them from the slot: each comes back
///     as the same kind, at its pose, holding the same cards in the same order. A malformed record fails the parse.
///     The in-memory slot file system keeps the test off the user folder.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class CardBundleSaveTest
{
    private static readonly Vector3 DeckPosition = new(1f, 0.5f, 2f);
    private static readonly Vector3 DeckRotation = new(0f, 0.7f, 0f);
    private static readonly Vector3 PilePosition = new(-3f, 0.25f, 4f);

    [TestCase]
    public static async Task ADeckAndAPileReloadAtTheirPosesWithEveryCardInOrder()
    {
        using var scene = new SaveWorld("CardBundleWorld", addCardsToTree: false);
        var deckCards = new List<StoredCard>
        {
            new(new CardSignature([0.5f, -0.25f, 0f, 1f, -1f, 0.125f, 0.75f, -0.5f]), 41, 12),
            new(null, 3, null),
            new(new CardSignature([0f, 0f, 0f, 0f, 0f, 0f, 0f, 0.25f]), null, 7),
        };
        var pileCards = Enumerable.Range(0, 150).Select(i => new StoredCard(null, i, i % 5 == 0 ? i + 1 : null))
            .ToList();
        var deck = Add(scene, CardBundleKind.Deck, deckCards, DeckPosition, DeckRotation);
        var pile = Add(scene, CardBundleKind.Pile, pileCards, PilePosition, Vector3.Zero);
        var service = scene.Add(
            new GameSaveService(new InMemorySlotFileSystem(), SaveWorld.SlotNamed("a")) { AutoLoadOnStart = false });
        AddNode(scene.World, autoFree: false);
        scene.Enter();
        service.SaveGame();

        service.LoadGame();

        AssertBool(deck.IsInsideTree()).IsFalse();
        AssertBool(pile.IsInsideTree()).IsFalse();
        var bundles = scene.Tree.GetNodesInGroup(CardBundle.GroupName).OfType<CardBundle>()
            .Where(bundle => bundle.IsInsideTree()).ToList();
        AssertThat(bundles).HasSize(2);
        var deckBack = bundles.Single(bundle => bundle.Kind == CardBundleKind.Deck);
        var pileBack = bundles.Single(bundle => bundle.Kind == CardBundleKind.Pile);
        AssertObject(deckBack.GetParent()).IsSame(scene.World);
        AssertThat(StoredItemKeys.Of(deckBack.Cards)).IsEqual(StoredItemKeys.Of(deckCards));
        AssertThat(StoredItemKeys.Of(pileBack.Cards)).IsEqual(StoredItemKeys.Of(pileCards));
        AssertThat(deckBack.GlobalPosition).IsEqualApprox(DeckPosition, new Vector3(0.001f, 0.001f, 0.001f));
        AssertThat(deckBack.GlobalRotation).IsEqualApprox(DeckRotation, new Vector3(0.001f, 0.001f, 0.001f));
        AssertThat(pileBack.GlobalPosition).IsEqualApprox(PilePosition, new Vector3(0.001f, 0.001f, 0.001f));
        await BoundedWait.ForQueuedFrees();
    }

    [TestCase("Heap", "card")]
    [TestCase("Pile", "none")]
    [TestCase("Deck", "container")]
    [TestCase("Deck", "short signature")]
    public static void AMalformedDeckOrPileRecordFailsTheParse(string kind, string item)
    {
        var cards = item switch
        {
            "none" => new List<object>(),
            "container" => [Item("container", scenePath: "res://Scenes/Shop/Items/Booster.tscn", kind: "Booster")],
            "short signature" => [Item("card", signature: [0.5f, 0.5f])],
            _ => [Item("card")]
        };
        var raw = new List<object>
        {
            new Dictionary<string, object>
            {
                ["Kind"] = kind,
                ["Position"] = new Vector3(1f, 0f, 2f),
                ["Rotation"] = Vector3.Zero,
                ["Cards"] = cards,
            },
        };

        Assertions.AssertThrown(() => SavedBundles.Parse(raw)).IsInstanceOf<FormatException>();
    }

    private static CardBundle Add(SaveWorld scene, CardBundleKind kind, IEnumerable<StoredCard> cards, Vector3 at,
        Vector3 rotation)
    {
        var bundle = CardBundle.Create(kind, cards);
        scene.World.AddChild(bundle);
        bundle.Position = at;
        bundle.Rotation = rotation;
        return bundle;
    }

    private static Dictionary<string, object?> Item(string type, string? scenePath = null, string? kind = null,
        float[]? signature = null) => new()
    {
        ["Type"] = type,
        ["ScenePath"] = scenePath,
        ["Kind"] = kind,
        ["Signature"] = signature,
        ["LookSeed"] = 1,
    };
}
