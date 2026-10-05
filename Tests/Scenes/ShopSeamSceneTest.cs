using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using CardCleaner.Scripts.Core.DependencyInjection;
using CardCleaner.Scripts.Features.Card.Models;
using CardCleaner.Scripts.Features.Portal.Components;
using CardCleaner.Scripts.Features.Portal.Models;
using CardCleaner.Scripts.Features.Shop.Components;
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
    private const string ArchwayPath = "res://Assets/Synty/PolygonDungeon/SM_Env_Wall_Archway_01.fbx";
    private const string GlowTexturePath = "res://Assets/Synty/ParticleFx/Generic_Circle_Soft_01.png";

    private ShopSeamRig _rig = null!;

    [BeforeTest]
    public async Task Setup()
    {
        _rig = await ShopSeamRig.Create();
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
        await _rig.MovePlayer(ShopSeamRig.BackofficeMiddle);
        await _rig.Hold(new CardSignature());

        AssertThat(_rig.Seam.Phase).IsEqual(SeamPhase.Hidden);
        AssertBool(_rig.Seam.Glow!.Visible).IsFalse();
        AssertBool(_rig.Seam.Door!.Visible).IsFalse();
        AssertBool(_rig.Seam.HumRequested).IsFalse();
    }

    [TestCase]
    [TestCategory("Unit")]
    public async Task SpecialCardInTheBackofficeShowsAGlowingSeamThatHums()
    {
        await _rig.MovePlayer(ShopSeamRig.BackofficeMiddle);
        await _rig.Hold(ShopSeamRig.Special());

        AssertThat(_rig.Seam.Phase).IsEqual(SeamPhase.Glowing);
        AssertBool(_rig.Seam.Glow!.Visible).IsTrue();
        AssertBool(_rig.Seam.HumRequested).IsTrue();
        AssertBool(_rig.Seam.Door!.Visible).IsFalse();
    }

    [TestCase]
    [TestCategory("Unit")]
    public async Task SpecialCardOutsideTheBackofficeDoesNotShowTheSeam()
    {
        await _rig.MovePlayer(new Vector3(6f, 0.95f, 9f));
        await _rig.Hold(ShopSeamRig.Special());

        AssertThat(_rig.Seam.Phase).IsEqual(SeamPhase.Hidden);
        AssertBool(_rig.Seam.HumRequested).IsFalse();
    }

    [TestCase]
    [TestCategory("Unit")]
    public async Task ApproachingTheSeamOpensItIntoADoorway()
    {
        await _rig.MovePlayer(ShopSeamRig.BackofficeMiddle);
        await _rig.Hold(ShopSeamRig.Special());
        await _rig.MovePlayer(ShopSeamRig.NearTheSeam);

        AssertThat(_rig.Seam.Phase).IsEqual(SeamPhase.Open);
        AssertBool(_rig.Seam.Door!.Visible).IsTrue();
    }

    [TestCase]
    [TestCategory("Unit")]
    public async Task SteppingThroughTheDoorwayLandsAtTheWorkshopEntryWithTheSameFacing()
    {
        await _rig.MovePlayer(ShopSeamRig.BackofficeMiddle);
        await _rig.Hold(ShopSeamRig.Special());
        _rig.Player.RotationDegrees = new Vector3(0f, 30f, 0f);
        await _rig.MovePlayer(ShopSeamRig.InTheDoorway);

        AssertThat(_rig.Seam.CrossingCount).IsEqual(1);
        var offsetFromEntry = _rig.Player.GlobalPosition - _rig.WorkshopEntry.GlobalPosition;
        AssertBool(new Vector2(offsetFromEntry.X, offsetFromEntry.Z).Length() < 1.5f).IsTrue();
        AssertBool(_rig.Player.GlobalPosition.X > 50f).IsTrue();
        AssertBool(Mathf.IsEqualApprox(_rig.Player.GlobalRotationDegrees.Y, 30f)).IsTrue();
        AssertBool(_rig.Player.GlobalPosition.Y > -1f).IsTrue();
    }

    [TestCase]
    [TestCategory("Unit")]
    public void HumHasAGeneratedLoopingStream()
    {
        AssertThat(_rig.Seam.Hum!.Stream).IsNotNull();
        AssertBool(_rig.Seam.Hum.Stream!.GetLength() > 0).IsTrue();
    }

    [TestCase]
    [TestCategory("Unit")]
    public void SeamArtIsListedInTheManifestWithPlaceholderAndPlainGlowFallbacks()
    {
        var manifest = JsonDocument.Parse(File.ReadAllText(ProjectSettings.GlobalizePath("res://tools/synty-assets.json")));
        var targets = manifest.RootElement.GetProperty("files").EnumerateArray()
            .Select(entry => entry.GetProperty("target").GetString()).ToArray();
        var art = _rig.Seam.Door!.GetNode<ShopArtSlot>("DoorArt");

        AssertThat(art.ArtPath).IsEqual(ArchwayPath);
        AssertThat(_rig.Seam.GlowTexturePath).IsEqual(GlowTexturePath);
        AssertThat(targets).Contains(ArchwayPath["res://Assets/Synty/".Length..]);
        AssertThat(targets).Contains(GlowTexturePath["res://Assets/Synty/".Length..]);
        AssertBool(art.ArtLoaded || art.Placeholder!.Visible).IsTrue();
        AssertThat(_rig.Seam.GlowTextureLoaded).IsEqual(ResourceLoader.Exists(GlowTexturePath));
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
}
