using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CardCleaner.Scripts.Core.Interfaces;
using CardCleaner.Scripts.Core.Randomness;
using CardCleaner.Scripts.Features.Card.Services;
using CardCleaner.Scripts.Features.Card.Components;
using CardCleaner.Scripts.Features.Card.Controllers;
using CardCleaner.Scripts.Features.Card.Models;
using CardCleaner.Scripts.Features.Card.Services.SaveGame;
using CardCleaner.Scripts.Features.SaveSlots.Models;
using CardCleaner.Scripts.Features.SaveSlots.Services;
using CardCleaner.Tests.Features.SaveSlots.Services;
using CardCleaner.Tests.Scenes;
using CardCleaner.Tests.TestUtilities;
using GdUnit4;
using Godot;

namespace CardCleaner.Tests.Features.Card.Services.SaveGame;

/// <summary>
/// A card's flavor sentence is not saved: it is chosen again from the saved signature and look seed. Real cards of
/// the three kinds (mundane with a look seed, faint, full signature) are spawned in the shop, saved, loaded, and each
/// reloaded card must show the sentence it showed before. The slot files live in an in-memory file system.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class CardFlavorSaveTest
{
    private const int MundaneLookSeed = 7;
    private const int SettleFrames = 10;

    private static readonly CardSignature Faint =
        new(new[] { 0.03f, -0.01f, 0f, 0.02f, 0f, -0.04f, 0f, 0.01f });

    private static readonly CardSignature Full =
        new(new[] { 0.95f, -0.6f, 0.2f, 0f, 0.4f, 0f, -0.1f, 0.3f });

    private SceneTree _tree = null!;
    private Node? _previousScene;
    private Node3D _shop = null!;
    private GameSaveService _saves = null!;

    [BeforeTest]
    public async Task Setup()
    {
        _tree = (SceneTree)Engine.GetMainLoop();
        _previousScene = _tree.CurrentScene;
        _shop = ShopOrderingRig.LoadShopWithServices();

        var files = new InMemorySlotFileSystem();
        var slots = new ActiveSaveSlot();
        if (!SaveSlotName.TryCreate("a", out var slot, out var reason))
            throw new InvalidOperationException(reason);
        slots.Activate(slot);

        _saves = _shop.GetNode<GameSaveService>("Services/GameSaveService");
        TestServices.Container.RegisterSingleton<IActiveSaveSlot>(slots);
        TestServices.Container.RegisterSingleton<ISlotFileSystem>(files);
        TestServices.Container.RegisterSingleton<SlotStore>(new SlotStore(files));
        TestServices.Container.RegisterSingleton<ISaveGameCommand>(_saves);

        // The rig hands over only the ordering services; the cards need the generator (from the composition root's
        // scan, as Compose does), the game's random sources and the shop's own card providers.
        TestServices.RegisterGameServiceClasses(TestServices.Container);
        TestServices.Container.RegisterSingleton(new RandomNumberGenerator { Seed = 1 });
        TestServices.Container.RegisterSingleton(RandomSources.Create(1u));
        _shop.GetNode<CardServiceProvider>("Services/CardServiceProvider").RegisterServices(TestServices.Container);
        _shop.GetNode<CardSpawningService>("Services/SpawningService").RegisterServices(TestServices.Container);

        AddNode(_shop, autoFree: false);
        _tree.CurrentScene = _shop;
        await SettleAsync();
    }

    [AfterTest]
    public void Teardown()
    {
        _tree.CurrentScene = _previousScene;
        _shop.Free();
        ShopPauseRig.Teardown();
    }

    [TestCase]
    public async Task EachReloadedCardShowsTheSentenceItShowedBeforeTheSave()
    {
        var spawning = TestServices.Resolver.Get<ICardSpawningService>();
        var world = _shop.GetNode<Node3D>("World");
        spawning.SpawnCard(new CardSignature(), MundaneLookSeed, At(6f), world);
        spawning.SpawnCard(Faint, null, At(7f), world);
        spawning.SpawnCard(Full, null, At(8f), world);
        // The renderer is baked on a deferred call after the spawn.
        await SettleAsync();
        var before = SentencesByIdentity();
        AssertThat(before.Count).IsEqual(3);
        AssertThat(before[Identity(new CardSignature(), MundaneLookSeed)])
            .IsEqual(FlavorText.For(new CardSignature(), MundaneLookSeed));
        AssertThat(before[Identity(Faint, null)]).IsEqual(FlavorText.For(Faint, null));
        AssertThat(before[Identity(Full, null)]).IsEqual(FlavorText.For(Full, null));

        AssertBool(_saves.ManualSave()).IsTrue();
        _saves.LoadGame();
        await SettleAsync();

        AssertThat(SentencesByIdentity()).IsEqual(before);
    }

    // Storefront floor, clear of the counter and the starting furniture.
    private static Transform3D At(float x) => new(Basis.Identity, new Vector3(x, 0.3f, 6f));

    private static string Identity(CardSignature signature, int? lookSeed) =>
        $"{signature.ToDebugString()}|{lookSeed}";

    private static readonly string[] Spawned =
    [
        Identity(new CardSignature(), MundaneLookSeed),
        Identity(Faint, null),
        Identity(Full, null)
    ];

    // Only the spawned cards: the workshop seeds a credit card of its own.
    private Dictionary<string, string> SentencesByIdentity() =>
        _tree.GetNodesInGroup("Cards").OfType<CardController>()
            .Where(card => !card.IsQueuedForDeletion() && Spawned.Contains(Identity(card.Signature, card.LookSeed)))
            .ToDictionary(
                card => Identity(card.Signature, card.LookSeed),
                card => card.GetNode<CardFace>("Face").Renderer.FlavorLabel.Text);

    private static async Task SettleAsync()
    {
        for (var i = 0; i < SettleFrames; i++)
            await ISceneRunner.SyncPhysicsFrame;
    }
}
