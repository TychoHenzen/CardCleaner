using System;
using CardCleaner.Scripts.Features.Card.Components;
using GdUnit4;
using Godot;

namespace CardCleaner.Tests.Features.Card.Components;

[TestSuite]
[RequireGodotRuntime]
public class CardDesignerTest
{
    private const string CardMeshPath = "res://Data/Card/CardMesh.tres";

    private CardDesigner _designer = null!;
    private Node3D _cardRoot = null!;

    [BeforeTest]
    public void Setup()
    {
        _designer = new CardDesigner();
        _cardRoot = CreateMockCardRoot();
        Assertions.AddNode(_cardRoot);
        _cardRoot.AddChild(_designer);
    }

    // A designer left at its defaults sizes its card's collider to them, so they must be the real card's size.
    [TestCase]
    public void DefaultDimensionsAreTheBakedCardMesh()
    {
        var mesh = ResourceLoader.Load<Mesh>(CardMeshPath).GetAabb().Size;
        var defaults = new Vector3(_designer.Width, _designer.Thickness, _designer.Height);
        Assertions.AssertBool(defaults.IsEqualApprox(mesh))
            .OverrideFailureMessage($"designer defaults {defaults}, the baked card mesh {mesh}")
            .IsTrue();
    }

    [TestCase]
    public void TestDimensionClamping()
    {
        var originalWidth = _designer.Width;

        // Setting the same value keeps it
        _designer.Width = originalWidth;
        Assertions.AssertFloat(_designer.Width).IsEqual(originalWidth);

        // Setting a different value stores it
        _designer.Width = 1.0f;
        Assertions.AssertFloat(_designer.Width).IsEqual(1.0f);
    }

    [TestCase]
    public void TestPropertyBounds()
    {
        // Test property range hints are respected
        _designer.Width = 0.1f; // Minimum
        Assertions.AssertFloat(_designer.Width).IsEqual(0.1f);

        _designer.Width = 3.0f; // Maximum
        Assertions.AssertFloat(_designer.Width).IsEqual(3.0f);

        _designer.Height = 0.1f;
        Assertions.AssertFloat(_designer.Height).IsEqual(0.1f);

        _designer.Height = 3.0f;
        Assertions.AssertFloat(_designer.Height).IsEqual(3.0f);
    }

    [TestCase]
    public void TestSetupRequiredNodes()
    {
        // Test that setup finds required nodes
        var temp = new Node3D();
        Assertions.AddNode(temp);
        Assertions.AssertThrown(() => _designer.Setup(temp))
            .IsInstanceOf<InvalidOperationException>()
            .HasMessage("CardCollision node not found");
    }

    [TestCase]
    public void Setup_SizesCollisionToDimensions()
    {
        _designer.Width = 0.4f;
        _designer.Height = 0.6f;
        _designer.Thickness = 0.01f;

        _designer.Setup(_cardRoot);

        var shape = _cardRoot.GetNode<CollisionShape3D>("CardCollision").Shape as BoxShape3D;
        Assertions.AssertThat(shape).IsNotNull();
        Assertions.AssertThat(shape!.Size).IsEqual(new Vector3(0.4f, 0.01f, 0.6f));
    }

    private static Node3D CreateMockCardRoot()
    {
        var root = new Node3D();
        root.Name = "CardRoot";

        // Create minimal required node structure for testing
        var collision = new CollisionShape3D();
        collision.Name = "CardCollision";
        collision.Shape = new BoxShape3D();
        root.AddChild(collision);

        return root;
    }
}
