using System.IO;
using System.Threading.Tasks;
using CardCleaner.Scripts.Core.Data;
using CardCleaner.Scripts.Core.DependencyInjection;
using CardCleaner.Scripts.Core.Interfaces;
using CardCleaner.Scripts.Features.Card.Controllers;
using CardCleaner.Scripts.Features.Card.Models;
using CardCleaner.Scripts.Features.Player.Controllers;
using CardCleaner.Scripts.Features.Portal.Components;
using CardCleaner.Scripts.Features.Portal.Models;
using CardCleaner.Scripts.Features.Shop.Components;
using CardCleaner.Tests.Features.Shop;
using Godot;

namespace CardCleaner.Tests.Scenes;

/// <summary>
/// End to end in the shop scene: the backoffice wall seam stays hidden and silent until the player
/// carries a special card into the backoffice, opens as the player comes close, and moves the player to the
/// workshop entry with the same facing when the player steps through.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class ShopSeamSceneTest
{
    private const int SettleFrames = 4;
    private const string ArchwayPath = "res://Assets/Synty/PolygonDungeon/SM_Env_Wall_Archway_01.fbx";
    private const string GlowTexturePath = "res://Assets/Synty/ParticleFx/Generic_Circle_Soft_01.png";

    private static readonly Vector3 BackofficeMiddle = new(12f, 0.95f, -3f);
    private static readonly Vector3 NearTheSeam = new(12f, 0.95f, -5.5f);
    private static readonly Vector3 InTheDoorway = new(12.2f, 0.95f, -7.4f);

    private Node3D _shop = null!;
    private PlayerController _player = null!;
    private WallSeam _seam = null!;
    private Node3D _workshopEntry = null!;

    [BeforeTest]
    public async Task Setup()
    {
        _shop = GD.Load<PackedScene>(ShopSceneProbe.ScenePath).Instantiate<Node3D>();
        _player = _shop.GetNode<PlayerController>("Player");
        _seam = _shop.GetNode<WallSeam>("World/Markers/SeamLocation/Seam");
        _workshopEntry = _shop.GetNode<Node3D>("World/WorkshopPlaceholder/WorkshopEntry");

        // The test scene is not the current scene, so hand the scene-owned services over directly.
        ServiceLocator.ResetForTesting();
        ServiceLocator.Container.RegisterSingleton<IGameSettings>(_shop.GetNode<GameSettings>("Services/GameSettings"));
        ServiceLocator.Container.RegisterSingleton<ICardSpawner>(_shop.GetNode<Node3D>("World/Cards") as ICardSpawner
            ?? throw new System.InvalidOperationException("World/Cards must be the card spawner"));

        AddNode(_shop);
        await Settle();
    }

    [AfterTest]
    public static void Teardown()
    {
        ServiceLocator.ResetForTesting();
    }

    [TestCase]
    [TestCategory("Unit")]
    public async Task WithoutASpecialCardTheSeamIsHiddenAndSilent()
    {
        await MovePlayer(BackofficeMiddle);
        await Hold(new CardSignature());

        AssertThat(_seam.Phase).IsEqual(SeamPhase.Hidden);
        AssertBool(_seam.Glow!.Visible).IsFalse();
        AssertBool(_seam.Door!.Visible).IsFalse();
        AssertBool(_seam.HumRequested).IsFalse();
    }

    [TestCase]
    [TestCategory("Unit")]
    public async Task SpecialCardInTheBackofficeShowsAGlowingSeamThatHums()
    {
        await MovePlayer(BackofficeMiddle);
        await Hold(Special());

        AssertThat(_seam.Phase).IsEqual(SeamPhase.Glowing);
        AssertBool(_seam.Glow!.Visible).IsTrue();
        AssertBool(_seam.HumRequested).IsTrue();
        AssertBool(_seam.Door!.Visible).IsFalse();
    }

    [TestCase]
    [TestCategory("Unit")]
    public async Task SpecialCardOutsideTheBackofficeDoesNotShowTheSeam()
    {
        await MovePlayer(new Vector3(6f, 0.95f, 9f));
        await Hold(Special());

        AssertThat(_seam.Phase).IsEqual(SeamPhase.Hidden);
        AssertBool(_seam.HumRequested).IsFalse();
    }

    [TestCase]
    [TestCategory("Unit")]
    public async Task ApproachingTheSeamOpensItIntoADoorway()
    {
        await MovePlayer(BackofficeMiddle);
        await Hold(Special());
        await MovePlayer(NearTheSeam);

        AssertThat(_seam.Phase).IsEqual(SeamPhase.Open);
        AssertBool(_seam.Door!.Visible).IsTrue();
    }

    [TestCase]
    [TestCategory("Unit")]
    public async Task SteppingThroughTheDoorwayLandsAtTheWorkshopEntryWithTheSameFacing()
    {
        await MovePlayer(BackofficeMiddle);
        await Hold(Special());
        _player.RotationDegrees = new Vector3(0f, 30f, 0f);
        await MovePlayer(InTheDoorway);

        AssertThat(_seam.CrossingCount).IsEqual(1);
        var offsetFromEntry = _player.GlobalPosition - _workshopEntry.GlobalPosition;
        AssertBool(new Vector2(offsetFromEntry.X, offsetFromEntry.Z).Length() < 1.5f).IsTrue();
        AssertBool(_player.GlobalPosition.X > 50f).IsTrue();
        AssertBool(Mathf.IsEqualApprox(_player.GlobalRotationDegrees.Y, 30f)).IsTrue();
        AssertBool(_player.GlobalPosition.Y > -1f).IsTrue();
    }

    [TestCase]
    [TestCategory("Unit")]
    public void HumHasAGeneratedLoopingStream()
    {
        AssertThat(_seam.Hum!.Stream).IsNotNull();
        AssertBool(_seam.Hum.Stream!.GetLength() > 0).IsTrue();
    }

    [TestCase]
    [TestCategory("Unit")]
    public void SeamArtIsListedInTheManifestWithPlaceholderAndPlainGlowFallbacks()
    {
        var manifest = File.ReadAllText(ProjectSettings.GlobalizePath("res://tools/synty-assets.json"));
        var art = _seam.Door!.GetNode<ShopArtSlot>("DoorArt");

        AssertThat(art.ArtPath).IsEqual(ArchwayPath);
        AssertThat(_seam.GlowTexturePath).IsEqual(GlowTexturePath);
        AssertThat(manifest).Contains(Path.GetFileName(ArchwayPath));
        AssertThat(manifest).Contains(Path.GetFileName(GlowTexturePath));
        AssertBool(art.ArtLoaded || art.Placeholder!.Visible).IsTrue();
        AssertThat(_seam.GlowTextureLoaded).IsEqual(ResourceLoader.Exists(GlowTexturePath));
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void MissingGlowTextureLeavesThePlainEmissiveMaterial()
    {
        var material = new StandardMaterial3D();
        var glow = new MeshInstance3D { MaterialOverride = material };
        var seam = new WallSeam { Glow = glow, GlowTexturePath = "res://Assets/Synty/ParticleFx/DoesNotExist.png" };
        seam.AddChild(glow);
        AddNode(seam);

        AssertBool(seam.GlowTextureLoaded).IsFalse();
        AssertThat(glow.MaterialOverride).IsEqual(material);
    }

    private static CardSignature Special() => new() { Febris = 0.3f };

    private async Task<CardController> Hold(CardSignature signature)
    {
        var card = ShopTestCards.Create(signature);
        _shop.GetNode("World/Cards").AddChild(card);
        _player.GetNode<CardCleaner.Scripts.Features.Card.Components.CardHolder>("CardInteraction/CardHolder")
            .AddCard(card);
        await Settle();
        return card;
    }

    private async Task MovePlayer(Vector3 position)
    {
        _player.GlobalPosition = position;
        _player.Velocity = Vector3.Zero;
        await Settle();
    }

    private static async Task Settle()
    {
        for (var i = 0; i < SettleFrames; i++)
            await ISceneRunner.SyncPhysicsFrame;
    }
}
