using System.Linq;
using System.Threading.Tasks;
using CardCleaner.Scripts.Core.Data;
using CardCleaner.Scripts.Core.DependencyInjection;
using CardCleaner.Scripts.Core.Interfaces;
using CardCleaner.Scripts.Features.Packs.Components;
using CardCleaner.Scripts.Features.Packs.Models;
using CardCleaner.Scripts.Features.Packs.Services;
using CardCleaner.Scripts.Features.Shop.Components;
using CardCleaner.Scripts.Features.Shop.Services;
using CardCleaner.Tests.Features.Packs;
using Godot;

namespace CardCleaner.Tests.Scenes;

[TestSuite]
[RequireGodotRuntime]
public class ShopGrayboxPackOpeningSceneTest
{
    private const string ScenePath = "res://Scenes/Graybox/ShopGraybox.tscn";
    private const ulong Seed = 20240531;
    private const int OpenFrames = 12;

    private Node3D _shop = null!;
    private OrderTerminal _terminal = null!;
    private MoneyService _money = null!;
    private OrderingService _ordering = null!;
    private RecordingCardSpawner _spawner = null!;

    [BeforeTest]
    public async Task Setup()
    {
        _shop = GD.Load<PackedScene>(ScenePath).Instantiate<Node3D>();
        _terminal = _shop.GetNode<OrderTerminal>("World/Markers/PcLocation/PcTerminal");
        _money = _shop.GetNode<MoneyService>("Services/MoneyService");
        _ordering = _shop.GetNode<OrderingService>("Services/OrderingService");
        _spawner = new RecordingCardSpawner();

        ServiceLocator.ResetForTesting();
        ServiceLocator.Container.RegisterSingleton<IGameSettings>(_shop.GetNode<GameSettings>("Services/GameSettings"));
        ServiceLocator.Container.RegisterSingleton<IMoneyService>(_money);
        ServiceLocator.Container.RegisterSingleton<IOrderingService>(_ordering);
        ServiceLocator.Container.RegisterSingleton(new RandomNumberGenerator { Seed = Seed });
        ServiceLocator.Container.RegisterSingleton<ICardSpawningService>(_spawner);
        _ordering.Money = _money;

        AddNode(_shop);
        await ISceneRunner.SyncPhysicsFrame;
        await ISceneRunner.SyncPhysicsFrame;
    }

    [AfterTest]
    public void Teardown()
    {
        if (GodotObject.IsInstanceValid(_shop))
            _shop.Free();

        ServiceLocator.ResetForTesting();
    }

    [TestCase]
    [TestCategory("Unit")]
    public async Task GrayboxOrderOpensOneBoxInto512SeededCards()
    {
        var item = _terminal.Catalog!.Items.Single(candidate => candidate.Id == "cardboard_box");
        var result = _ordering.Order(item);

        AssertBool(result.Succeeded).IsTrue();
        var box = (CardContainer)result.Spawned!;
        AssertBool(box.Open()).IsTrue();
        await Frames(OpenFrames);

        var packs = Containers(CardContainerKind.Pack);
        AssertThat(packs.Length).IsEqual(CardContainerLayout.ItemsPerContainer);
        foreach (var pack in packs)
            AssertBool(pack.Open()).IsTrue();

        await Frames(OpenFrames);
        var boosters = Containers(CardContainerKind.Booster);
        AssertThat(boosters.Length).IsEqual(CardContainerLayout.ItemsPerContainer * CardContainerLayout.ItemsPerContainer);
        foreach (var booster in boosters)
            AssertBool(booster.Open()).IsTrue();

        var budget = CardContainerLayout.CardsPerBox;
        while (_spawner.Spawned.Count < CardContainerLayout.CardsPerBox && budget-- > 0)
            await Frames(1);

        AssertThat(_spawner.Spawned.Count).IsEqual(CardContainerLayout.CardsPerBox);
        var specialCount = 0;
        foreach (var spawned in _spawner.Spawned)
        {
            var signature = spawned.Signature;
            if (signature.HasMagicalPotential())
            {
                specialCount++;
                AssertBool(signature.Elements.Any(element => element != 0f)).IsTrue();
            }
            else
                AssertThat(signature.Elements).IsEqual(new float[8]);
        }

        AssertThat(CardPackGenerator.SpecialCardOdds).IsEqual(CardContainerLayout.CardsPerBox);
        AssertBool(specialCount > 0).IsTrue();
    }

    private CardContainer[] Containers(CardContainerKind kind) =>
        _shop.GetNode("World").GetChildren().OfType<CardContainer>()
            .Where(container => container.Kind == kind && !container.IsOpened).ToArray();

    private static async Task Frames(int count)
    {
        for (var i = 0; i < count; i++)
            await ISceneRunner.SyncProcessFrame;
    }
}
