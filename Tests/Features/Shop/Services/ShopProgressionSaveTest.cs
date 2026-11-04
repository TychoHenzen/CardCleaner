using System.Threading.Tasks;
using CardCleaner.Scripts.Features.Card.Services.SaveGame;
using CardCleaner.Scripts.Features.SaveSlots.Services;
using CardCleaner.Scripts.Features.Shop.Services;
using CardCleaner.Tests.Features.SaveSlots;
using CardCleaner.Tests.Features.SaveSlots.Services;
using Godot;
using Saveable;

namespace CardCleaner.Tests.Features.Shop.Services;

[TestSuite]
[RequireGodotRuntime]
public class ShopProgressionSaveTest
{
    private const int FirstAward = 1200;
    private const int SecondAward = 100;
    private const int ThirdAward = 1100;

    [TestCase]
    [TestCategory("Unit")]
    public void SavedXpRestoresOnAFreshNode()
    {
        var first = AutoFree(new ShopProgression());
        first.AwardCheckout(FirstAward);
        var save = new NodeSave();
        first.Save(save);

        var second = AutoFree(new ShopProgression());
        second.Load(save);

        AssertThat(second.Xp).IsEqual(120);
        AssertThat(second.Level).IsEqual(2);
    }

    [TestCase]
    [TestCategory("Unit")]
    public void LoadingASaveWithNoXpEntryStartsAtZero()
    {
        var progression = AutoFree(new ShopProgression());
        progression.AwardCheckout(FirstAward);

        progression.Load(new NodeSave());

        AssertThat(progression.Xp).IsEqual(0);
        AssertThat(progression.Level).IsEqual(1);
    }

    [TestCase]
    [TestCategory("Unit")]
    public void NegativeXpLoadsAsZero()
    {
        var progression = AutoFree(new ShopProgression());
        var save = new NodeSave();
        save.SetOrAddProperty("xp", -40L);

        progression.Load(save);

        AssertThat(progression.Xp).IsEqual(0);
    }

    [TestCase]
    [TestCategory("Unit")]
    public void NonNumericXpLoadsAsZeroWithoutThrowing()
    {
        var progression = AutoFree(new ShopProgression());
        var save = new NodeSave();
        save.SetOrAddProperty("xp", "many");

        progression.Load(save);

        AssertThat(progression.Xp).IsEqual(0);
    }

    [TestCase]
    [TestCategory("Unit")]
    public void HugeXpLoadsClampedToTheCeiling()
    {
        var progression = AutoFree(new ShopProgression());
        var save = new NodeSave();
        save.SetOrAddProperty("xp", long.MaxValue);

        progression.Load(save);

        AssertThat(progression.Xp).IsEqual(int.MaxValue / 2);
        AssertThat(progression.Level).IsEqual(5);
    }

    [TestCase]
    [TestCategory("Unit")]
    public void LoadRaisesProgressChangedExactlyOnce()
    {
        var progression = AutoFree(new ShopProgression());
        var save = new NodeSave();
        save.SetOrAddProperty("xp", 60L);
        var changes = 0;
        progression.ProgressChanged += () => changes++;

        progression.Load(save);

        AssertThat(changes).IsEqual(1);
    }

    [TestCase]
    [TestCategory("Unit")]
    public async Task EachSlotRestoresItsOwnXp()
    {
        using var scene = new SaveWorld("ShopProgressionWorld");
        var slots = new ActiveSaveSlot();
        var progression = new ShopProgression { Name = "Progression" };
        scene.World.AddChild(progression);
        scene.Add(new GameSaveService(new InMemorySlotFileSystem(), slots) { AutoLoadOnStart = false });
        AddNode(scene.World, autoFree: false);
        scene.Enter();

        progression.AwardCheckout(SecondAward);
        SaveWorld.Activate(slots, "b");
        scene.Service!.SaveGame();

        progression.AwardCheckout(ThirdAward);
        SaveWorld.Activate(slots, "a");
        scene.Service!.SaveGame();

        SaveWorld.Activate(slots, "b");
        scene.Service!.LoadGame();
        await scene.TwoFrames();
        AssertThat(progression.Xp).IsEqual(10);
        AssertThat(progression.Level).IsEqual(1);

        SaveWorld.Activate(slots, "a");
        scene.Service!.LoadGame();
        await scene.TwoFrames();
        AssertThat(progression.Xp).IsEqual(120);
        AssertThat(progression.Level).IsEqual(2);
    }
}
