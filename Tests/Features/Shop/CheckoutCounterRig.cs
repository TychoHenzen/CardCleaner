using System.Linq;
using System.Threading.Tasks;
using CardCleaner.Scripts.Features.Shop.Components;
using CardCleaner.Scripts.Features.Shop.Services;
using CardCleaner.Tests.TestUtilities;
using Godot;

namespace CardCleaner.Tests.Features.Shop;

/// <summary>
/// A checkout counter in a bare world with three queue slots, two item spots and a money service, shared by the
/// counter suites so each only states its scenario.
/// </summary>
public sealed class CheckoutCounterRig
{
    public const int StartingBalance = 100;
    private const int QueueSlotCount = 3;
    private const int ItemSpotCount = 2;
    private const float QueueSlotSpacing = 1.2f;
    private const float QueueSlotStart = 1.4f;
    private const float ItemSpotX = 0.4f;
    private const float ItemSpotY = 1.15f;

    private CheckoutCounterRig(
        MoneyService money,
        OpenProgression progression,
        CheckoutCounter counter,
        Marker3D[] slots,
        Marker3D[] itemSpots)
    {
        Money = money;
        Progression = progression;
        Counter = counter;
        Slots = slots;
        ItemSpots = itemSpots;
    }

    public MoneyService Money { get; }
    public OpenProgression Progression { get; }
    public CheckoutCounter Counter { get; }
    public Marker3D[] Slots { get; }
    public Marker3D[] ItemSpots { get; }

    public static async Task<CheckoutCounterRig> Create()
    {
        TestServices.Reset();
        var world = new Node3D();
        var money = new MoneyService { StartingBalance = StartingBalance };
        var label = new Label3D();
        var counter = new CheckoutCounter { StatusLabel = label };
        TestServices.Container.RegisterSingleton<IMoneyService>(money);
        TestServices.Inject(counter);

        var slots = Enumerable.Range(0, QueueSlotCount)
            .Select(i => new Marker3D
            {
                Name = $"QueueSlot{i}",
                Position = new Vector3(0f, 0f, QueueSlotStart + QueueSlotSpacing * i),
            })
            .ToArray();
        var itemSpots = Enumerable.Range(0, ItemSpotCount)
            .Select(i => new Marker3D
            {
                Name = $"ItemSpot{i}",
                Position = new Vector3(i == 0 ? -ItemSpotX : ItemSpotX, ItemSpotY, 0f),
            })
            .ToArray();

        // The markers are children of the counter before it enters the tree, so its _Ready finds them.
        foreach (var marker in slots.Concat(itemSpots))
            counter.AddChild(marker);

        counter.AddChild(label);
        world.AddChild(money);
        world.AddChild(counter);
        AddNode(world);
        var progression = new OpenProgression();
        counter.Money = money;
        counter.Progression = progression;
        await ISceneRunner.SyncPhysicsFrame;
        return new CheckoutCounterRig(money, progression, counter, slots, itemSpots);
    }
}
