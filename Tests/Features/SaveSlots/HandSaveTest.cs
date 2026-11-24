using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CardCleaner.Scripts.Core.Enumeration;
using CardCleaner.Scripts.Core.Interfaces;
using CardCleaner.Scripts.Features.Card.Controllers;
using CardCleaner.Scripts.Features.Card.Models;
using CardCleaner.Scripts.Features.SaveSlots.Services.SaveGame;
using CardCleaner.Scripts.Features.Packs.Components;
using CardCleaner.Scripts.Features.Packs.Models;
using CardCleaner.Tests.Features.SaveSlots.Services;
using CardCleaner.Tests.TestUtilities;
using Godot;
using NSubstitute;
using Saveable;

namespace CardCleaner.Tests.Features.SaveSlots;

/// <summary>
///     Saves the player's hand through the save service into an in-memory slot and loads it back: every item returns
///     to the hand in the order it was held, with the same front item, and nothing the hand held is left loose in the
///     world. A malformed hand record fails before the scene changes. Containers on the floor still save in the
///     container list.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class HandSaveTest
{
    private const string BoxScene = "res://Scenes/Shop/Items/CardboardBox.tscn";
    private const string PackScene = "res://Scenes/Shop/Items/Pack.tscn";
    private const string BoosterScene = "res://Scenes/Shop/Items/Booster.tscn";
    private const ulong Seed = 0x4E1DUL;
    private const int FirstCardPrice = 12;

    private static readonly CardSignature FirstSignature = new CardSignature().With(Element.Febris, 0.4f);
    private static readonly CardSignature LastSignature = new CardSignature().With(Element.Lumines, -0.7f);

    // A card, a booster and a card, with the booster in front: the order and the front both come back.
    [TestCase]
    public static async Task ACardABoosterAndACardComeBackInOrderWithTheBoosterInFront()
    {
        using var rig = new Rig("HandRoundTripWorld");
        var first = rig.LooseCard(FirstSignature);
        first.ShopPrice = FirstCardPrice;
        var booster = rig.Placed(BoosterScene);
        var last = rig.LooseCard(LastSignature);
        AssertBool(rig.Hand.TryTake(first)).IsTrue();
        AssertBool(rig.Hand.TryTake(booster)).IsTrue();
        AssertBool(rig.Hand.TryTake(last)).IsTrue();
        AssertBool(rig.Hand.SetFront(1)).IsTrue();

        await rig.SaveAndReload();

        var items = rig.Hand.Items;
        AssertThat(items.Count).IsEqual(3);
        AssertThat(rig.Hand.FrontIndex).IsEqual(1);
        var card0 = (CardController)items[0];
        var held = (CardContainer)items[1];
        var card2 = (CardController)items[2];
        AssertBool(card0.Signature.Elements.SequenceEqual(FirstSignature.Elements)).IsTrue();
        AssertThat(card0.ShopPrice).IsEqual(FirstCardPrice);
        AssertThat(held.Kind).IsEqual(CardContainerKind.Booster);
        AssertThat(held.ContentSeed).IsEqual(Seed);
        AssertBool(card2.Signature.Elements.SequenceEqual(LastSignature.Elements)).IsTrue();
        AssertBool(items.All(item => item.GetParent() == rig.Anchor)).IsTrue();
        AssertThat(held.CollisionLayer).IsEqual(0u);
        // Nothing the hand held comes back a second time from the card or container list.
        AssertThat(rig.LooseCards()).IsEmpty();
        AssertThat(rig.Containers()).ContainsExactly(held);
    }

    // A booster box emptied into the hand leaves its eight booster packs there; a save brings all eight back in the
    // same order, each with the seed it had.
    [TestCase]
    public static async Task EightBoosterPacksInHandComeBackInTheirOrderWithTheirSeeds()
    {
        using var rig = new Rig("BoosterStackSaveWorld");
        var pack = rig.Placed(PackScene);
        AssertBool(rig.Hand.TryTake(pack)).IsTrue();
        AssertBool(rig.Hand.UseFront()).IsTrue();
        var before = rig.HeldSeeds();
        AssertThat(before).IsEqual(Enumerable.Range(0, CardContainerLayout.ItemsPerContainer)
            .Select(index => ContainerSeeds.ChildSeed(Seed, index)).ToArray());
        var front = rig.Hand.FrontIndex;

        await rig.SaveAndReload();

        AssertThat(rig.HeldSeeds()).IsEqual(before);
        AssertThat(rig.Hand.FrontIndex).IsEqual(front);
        AssertBool(rig.Hand.Items.All(item => ((CardContainer)item).CollisionLayer == 0)).IsTrue();
    }

    // A box opened on the floor, partly emptied and picked up comes back open in the hand, holding the same items.
    [TestCase]
    public static async Task AnOpenPartlyTakenBoxInHandComesBackOpenWithTheSameItemsLeft()
    {
        using var rig = new Rig("OpenBoxSaveWorld");
        var box = rig.Placed(BoxScene);
        box.SetLidOpen(true, instantly: true);
        for (var i = 0; i < 3; i++)
            AssertBool(box.Storage!.TakeOnto(new Transform3D(Basis.Identity, new Vector3(3f + i, 0f, 0f)))).IsTrue();
        var left = StoredItemKeys.Of(box.Storage!.Items);
        AssertBool(rig.Hand.TryTake(box)).IsTrue();

        await rig.SaveAndReload();

        var restored = (CardContainer)rig.Hand.Front!;
        AssertThat(rig.Hand.Count).IsEqual(1);
        AssertThat(restored.Kind).IsEqual(CardContainerKind.Box);
        AssertBool(restored.IsLidOpen).IsTrue();
        AssertThat(restored.Remaining).IsEqual(CardContainerLayout.ItemsPerContainer - 3);
        AssertThat(StoredItemKeys.Of(restored.Storage!.Items)).IsEqual(left);
    }

    // The hand record is read before anything is freed, so a broken one leaves the hand and the world as they were.
    [TestCase]
    public static async Task AMalformedHandRecordLeavesTheSceneAsItWas()
    {
        using var rig = new Rig("MalformedHandWorld");
        var card = rig.LooseCard(FirstSignature);
        var booster = rig.Placed(BoosterScene);
        var floorBox = rig.Placed(BoxScene);
        AssertBool(rig.Hand.TryTake(card)).IsTrue();
        AssertBool(rig.Hand.TryTake(booster)).IsTrue();
        var save = new NodeSave();
        rig.Scene.Service!.Save(save);
        save.SetOrAddProperty("hand", new Dictionary<string, object>
        {
            ["Front"] = 5,
            ["Items"] = new List<object>(),
        });

        AssertThrown(() => rig.Scene.Service.Load(save)).IsInstanceOf<FormatException>();
        await rig.Scene.TwoFrames();

        AssertThat(rig.Hand.Items).ContainsExactly(card, booster);
        AssertThat(rig.Hand.FrontIndex).IsEqual(0);
        AssertBool(GodotObject.IsInstanceValid(floorBox) && !floorBox.IsQueuedForDeletion()).IsTrue();
    }

    // Each broken hand record fails the parse: a front that is not one of the items, an item that is neither one
    // card nor one container, and a card that cannot be read.
    [TestCase("front past the items")]
    [TestCase("front on an empty hand")]
    [TestCase("empty item")]
    [TestCase("card and container")]
    [TestCase("short signature")]
    public static void AMalformedHandRecordFailsTheParse(string fault)
    {
        var card = new Dictionary<string, object> { ["Type"] = "card", ["LookSeed"] = 1 };
        var container = new Dictionary<string, object>
        {
            ["ScenePath"] = BoosterScene, ["Kind"] = "Booster", ["Position"] = Vector3.Zero,
            ["Rotation"] = Vector3.Zero,
        };
        var (front, item) = fault switch
        {
            "front past the items" => (1, new Dictionary<string, object> { ["Card"] = card }),
            "front on an empty hand" => (0, null),
            "empty item" => (0, new Dictionary<string, object>()),
            "card and container" => (0, new Dictionary<string, object> { ["Card"] = card, ["Container"] = container }),
            "short signature" => (0, new Dictionary<string, object>
            {
                ["Card"] = new Dictionary<string, object> { ["Type"] = "card", ["Signature"] = new[] { 0.5f, 0.5f } },
            }),
            _ => throw new ArgumentOutOfRangeException(nameof(fault), fault, "Unknown fault")
        };
        var raw = new Dictionary<string, object>
        {
            ["Front"] = front,
            ["Items"] = item == null ? new List<object>() : new List<object> { item },
        };

        AssertThrown(() => SavedHand.Parse(raw)).IsInstanceOf<FormatException>();
    }

    [TestCase]
    public static async Task ABoxOnTheFloorComesBackOnTheFloor()
    {
        using var scene = new SaveWorld("FloorSaveWorld", addCardsToTree: false);
        AddNode(scene.World, autoFree: false);
        scene.Enter();
        var position = new Vector3(2f, 0f, 3f);
        PlacedBox(scene, position);

        SavedContainers.Restore(scene.Tree, CaptureAndParse(scene), scene.World);

        // Checked before any frame runs, so physics has not moved the box off its saved position.
        var restored = OnlyRestoredBox(scene);
        AssertBool(restored.GetParent() == scene.World).IsTrue();
        AssertThat(restored.GlobalPosition).IsEqual(position);
        AssertThat(restored.ContentSeed).IsEqual(Seed);

        // Restore queue-frees the scene's own box; a frame lets Godot free it before the test ends.
        await scene.TwoFrames();
    }

    [TestCase]
    public static void AShutBoxSavedOnTheFloorReadsShut()
    {
        using var scene = new SaveWorld("ShutBoxSaveWorld", addCardsToTree: false);
        AddNode(scene.World, autoFree: false);
        scene.Enter();
        PlacedBox(scene, Vector3.Zero);

        AssertBool(CaptureAndParse(scene).Single().LidOpen).IsFalse();
    }

    // Only a cardboard box holds stored items, and each one must be readable: a card with an eight-value signature,
    // or a booster or booster box with a scene.
    [TestCase("Pack", "card")]
    [TestCase("Box", "coin")]
    [TestCase("Box", "nested box")]
    [TestCase("Box", "short signature")]
    [TestCase("Box", "container without a scene")]
    public static void AContainerRecordWithUnreadableContentsIsMalformed(string kind, string item)
    {
        var raw = new List<object>
        {
            new Dictionary<string, object>
            {
                ["ScenePath"] = BoxScene,
                ["Kind"] = kind,
                ["Position"] = new Vector3(1f, 0f, 2f),
                ["Rotation"] = Vector3.Zero,
                ["Contents"] = new List<object> { StoredItemRecord(item) },
            },
        };

        AssertThrown(() => SavedContainers.Parse(raw)).IsInstanceOf<FormatException>();
    }

    private static Dictionary<string, object> StoredItemRecord(string item) => item switch
    {
        "card" => new Dictionary<string, object> { ["Type"] = "card", ["LookSeed"] = 1 },
        "coin" => new Dictionary<string, object> { ["Type"] = "coin" },
        "nested box" => new Dictionary<string, object>
            { ["Type"] = "container", ["ScenePath"] = BoxScene, ["Kind"] = "Box" },
        "short signature" => new Dictionary<string, object>
            { ["Type"] = "card", ["Signature"] = new[] { 0.5f, 0.5f } },
        "container without a scene" => new Dictionary<string, object> { ["Type"] = "container", ["Kind"] = "Booster" },
        _ => throw new ArgumentOutOfRangeException(nameof(item), item, "Unknown test item")
    };

    private static CardContainer PlacedBox(SaveWorld scene, Vector3 position)
    {
        var box = GD.Load<PackedScene>(BoxScene).Instantiate<CardContainer>();
        box.ContentSeed = Seed;
        box.Spawning = scene.Spawner;
        scene.World.AddChild(box);
        box.GlobalPosition = position;
        return box;
    }

    private static List<SavedContainers.Saved> CaptureAndParse(SaveWorld scene) =>
        SavedContainers.Parse(SavedContainers.Capture(scene.Tree));

    private static CardContainer OnlyRestoredBox(SaveWorld scene)
    {
        var boxes = scene.Tree.GetNodesInGroup("card_containers").OfType<CardContainer>()
            .Where(container => container.SceneFilePath == BoxScene && !container.IsQueuedForDeletion())
            .ToList();
        AssertThat(boxes).HasSize(1);
        return boxes.Single();
    }

    /// <summary>
    ///     A world with a save service on an in-memory slot, a camera as the hand anchor, and the player's hand, built
    ///     and injected the way the composition root does it.
    /// </summary>
    private sealed class Rig : IDisposable
    {
        public Rig(string name)
        {
            Scene = new SaveWorld(name);
            TestServices.Container.RegisterSingleton(Substitute.For<IInputService>());
            Anchor = new Camera3D { Name = "Eye" };
            Scene.World.AddChild(Anchor);
            Hand = TestServices.Inject(new Scripts.Features.Hand.Components.Hand { Name = "Hand" });
            Scene.World.AddChild(Hand);
            Hand.SetReferences(Anchor);
            Scene.Add(new GameSaveService(new InMemorySlotFileSystem(), SaveWorld.SlotNamed("hand"))
                { AutoLoadOnStart = false });
            AddNode(Scene.World, autoFree: false);
            Scene.Enter();
        }

        public SaveWorld Scene { get; }
        public Camera3D Anchor { get; }
        public Scripts.Features.Hand.Components.Hand Hand { get; }

        public CardController LooseCard(CardSignature signature)
        {
            var card = new CardController { Signature = signature };
            Scene.World.AddChild(card);
            return card;
        }

        public CardContainer Placed(string scenePath)
        {
            var container = GD.Load<PackedScene>(scenePath).Instantiate<CardContainer>();
            container.ContentSeed = Seed;
            container.Spawning = Scene.Spawner;
            Scene.World.AddChild(container);
            return container;
        }

        // Load defers the hand and card restores and frees the old containers at the end of the frame: wait for all.
        public async Task SaveAndReload()
        {
            AssertBool(Scene.Service!.ManualSave()).IsTrue();
            Scene.Service.LoadGame();
            await Scene.TwoFrames();
        }

        public List<CardController> LooseCards() =>
            Scene.World.GetChildren().OfType<CardController>().Where(card => !card.IsQueuedForDeletion()).ToList();

        public List<CardContainer> Containers() =>
            Scene.Tree.GetNodesInGroup("card_containers").OfType<CardContainer>()
                .Where(container => !container.IsQueuedForDeletion()).ToList();

        public ulong[] HeldSeeds() =>
            Hand.Items.OfType<CardContainer>().Select(container => container.ContentSeed!.Value).ToArray();

        public void Dispose() => Scene.Dispose();
    }
}
