using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CardCleaner.Scripts.Core.DependencyInjection;
using CardCleaner.Scripts.Features.Shop.Components;
using Godot;

namespace CardCleaner.Tests.Scenes;

/// <summary>
/// The workshop is a dungeon-like graybox room inside the shop scene, far from the shop interior. It holds the
/// arcade cabinet, two card holders joined to it by visible wires, and a credit card seated in one holder.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class ShopWorkshopSceneTest
{
    private const float WireTouchTolerance = 0.12f;
    private const float MinimumDistanceFromShop = 30f;

    private WorkshopSceneRig _rig = null!;

    [BeforeTest]
    public async Task Setup()
    {
        _rig = await WorkshopSceneRig.Create();
    }

    [AfterTest]
    public static void Teardown()
    {
        ServiceLocator.ResetForTesting();
    }

    [TestCase]
    [TestCategory("Unit")]
    public void RoomHasEntryMarkerAboveItsFloorAndEnclosingCollision()
    {
        var room = _rig.Workshop.GetNode("Room");

        AssertThat(room.GetNodeOrNull<CollisionShape3D>("Floor/CollisionShape3D")).IsNotNull();
        AssertThat(room.GetNodeOrNull<CollisionShape3D>("Ceiling/CollisionShape3D")).IsNotNull();
        foreach (var wall in new[] { "NorthWall", "EastWall", "SouthWall", "WestWall" })
            AssertThat(room.GetNodeOrNull<CollisionShape3D>($"Walls/{wall}/CollisionShape3D")).IsNotNull();

        AssertBool(_rig.Entry.GlobalPosition.Y > 0f).IsTrue();
        AssertBool(IsInsideRoom(_rig.Entry.GlobalPosition)).IsTrue();
    }

    [TestCase]
    [TestCategory("Unit")]
    public void RoomSitsFarFromTheShopInteriorSoItCannotBeSeenFromIt()
    {
        var spawn = _rig.Shop.GetNode<Node3D>("Player").GlobalPosition;
        var backoffice = _rig.Shop.GetNode<Node3D>("World/Markers/PcLocation").GlobalPosition;

        AssertBool(_rig.Entry.GlobalPosition.DistanceTo(spawn) > MinimumDistanceFromShop).IsTrue();
        AssertBool(_rig.Entry.GlobalPosition.DistanceTo(backoffice) > MinimumDistanceFromShop).IsTrue();
    }

    [TestCase]
    [TestCategory("Unit")]
    public void CabinetHoldersAndButtonStandInsideTheRoom()
    {
        foreach (var part in new Node3D[] { _rig.Cabinet, _rig.DeckHolder, _rig.CardHolder, _rig.Button })
            AssertBool(IsInsideRoom(part.GlobalPosition)).IsTrue();
    }

    [TestCase]
    [TestCategory("Unit")]
    public void EachHolderIsJoinedToTheCabinetByAVisibleWire()
    {
        foreach (var (holder, prefix) in new[] { (_rig.DeckHolder, "Deck"), (_rig.CardHolder, "Card") })
        {
            var chain = _rig.Workshop.GetNode("Wires").GetChildren()
                .OfType<MeshInstance3D>()
                .Where(wire => wire.Name.ToString().StartsWith(prefix))
                .Select(WorldBounds)
                .ToList();

            AssertBool(chain.Count > 0).IsTrue();
            AssertBool(chain.TrueForAll(bounds => bounds.Size.Length() > 0f)).IsTrue();
            AssertBool(chain.Exists(bounds => TouchesHolder(bounds, holder.GlobalPosition))).IsTrue();
            AssertBool(chain.Exists(TouchesCabinetFront)).IsTrue();
            AssertBool(ChainIsConnected(chain)).IsTrue();
        }
    }

    [TestCase]
    [TestCategory("Unit")]
    public void CreditCardEndsUpSeatedInTheDeckHolderOnly()
    {
        AssertBool(_rig.DeckHolder.HasCards).IsTrue();
        AssertThat(_rig.DeckHolder.Cards.Count).IsEqual(1);
        AssertBool(_rig.CardHolder.HasCards).IsFalse();
    }

    [TestCase]
    [TestCategory("Unit")]
    public void WorkshopArtUsesPackMeshesListedInTheManifestWithPlaceholderFallbacks()
    {
        var slots = new List<ShopArtSlot>();
        CollectArtSlots(_rig.Workshop, slots);
        var manifest = File.ReadAllText(ProjectSettings.GlobalizePath("res://tools/synty-assets.json"));

        AssertBool(slots.Exists(s => s.ArtPath.Contains("SM_Env_Wall"))).IsTrue();
        AssertBool(slots.Exists(s => s.ArtPath.Contains("SM_Env_Tiles"))).IsTrue();
        AssertBool(slots.Exists(s => s.ArtPath.Contains("ArcadeMachine"))).IsTrue();
        foreach (var slot in slots)
        {
            AssertBool(slot.ArtPath.StartsWith("res://Assets/Synty/")).IsTrue();
            AssertThat(manifest).Contains(Path.GetFileName(slot.ArtPath));
            AssertThat(slot.Placeholder).IsNotNull();
            AssertBool(slot.ArtLoaded || slot.Placeholder!.Visible).IsTrue();
        }
    }

    private bool IsInsideRoom(Vector3 position)
    {
        var floor = _rig.Workshop.GetNode<CollisionShape3D>("Room/Floor/CollisionShape3D");
        var half = ((BoxShape3D)floor.Shape).Size / 2f;
        var center = floor.GlobalPosition;
        return Mathf.Abs(position.X - center.X) <= half.X && Mathf.Abs(position.Z - center.Z) <= half.Z;
    }

    private static Aabb WorldBounds(MeshInstance3D mesh) => mesh.GlobalTransform * mesh.Mesh.GetAabb();

    private static bool TouchesHolder(Aabb wire, Vector3 holder) =>
        Mathf.Abs(wire.GetCenter().X - holder.X) <= WireTouchTolerance
        && wire.Position.Z <= holder.Z + WireTouchTolerance
        && wire.End.Z >= holder.Z - WireTouchTolerance - 0.5f;

    private bool TouchesCabinetFront(Aabb wire)
    {
        var shape = _rig.Cabinet.GetNode<CollisionShape3D>("CollisionShape3D");
        var front = shape.GlobalPosition.Z + ((BoxShape3D)shape.Shape).Size.Z / 2f;
        return Mathf.Abs(wire.Position.Z - front) <= WireTouchTolerance;
    }

    private static bool ChainIsConnected(List<Aabb> chain)
    {
        var reached = new List<Aabb> { chain[0] };
        var pending = chain.Skip(1).ToList();
        var progress = true;
        while (progress && pending.Count > 0)
        {
            progress = false;
            foreach (var wire in pending.ToList().Where(w => reached.Exists(r => r.Grow(WireTouchTolerance).Intersects(w))))
            {
                reached.Add(wire);
                pending.Remove(wire);
                progress = true;
            }
        }

        return pending.Count == 0;
    }

    private static void CollectArtSlots(Node node, List<ShopArtSlot> slots)
    {
        if (node is ShopArtSlot slot)
            slots.Add(slot);

        foreach (var child in node.GetChildren())
            CollectArtSlots(child, slots);
    }
}
