using System;
using CardCleaner.Scripts.Features.Card.Components;
using GdUnit4;
using Godot;

namespace CardCleaner.Tests.Features;

[TestSuite]
[RequireGodotRuntime]
public class CardDesignerTest
{
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

    [TestCase]
    public void TestDefaultDimensions()
    {
        Assertions.AssertFloat(_designer.Width).IsEqualApprox(0.635f, 0.001f);
        Assertions.AssertFloat(_designer.Height).IsEqualApprox(0.889f, 0.001f);
        Assertions.AssertFloat(_designer.Thickness).IsEqualApprox(0.005f, 0.001f);
    }

    [TestCase]
    public void TestDimensionClamping()
    {
        var originalWidth = _designer.Width;
        
        // Setting same value should not trigger update
        _designer.Width = originalWidth;
        Assertions.AssertFloat(_designer.Width).IsEqual(originalWidth);
        
        // Setting different value should update
        _designer.Width = 1.0f;
        Assertions.AssertFloat(_designer.Width).IsEqual(1.0f);
    }

    [TestCase]
    public void TestPropertyBounds()
    {
        // Test property range hints are respected
        _designer.Width = 0.1f;  // Minimum
        Assertions.AssertFloat(_designer.Width).IsEqual(0.1f);
        
        _designer.Width = 3.0f;  // Maximum  
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
            .HasMessage("OuterBox node not found");
    }

    [TestCase]
    public void TestBevelConfiguration()
    {
        _designer.BevelSize = 0.05f;
        _designer.BevelSides = 8;
        
        Assertions.AssertFloat(_designer.BevelSize).IsEqual(0.05f);
        Assertions.AssertThat(_designer.BevelSides).IsEqual(8);
    }

    [TestCase]
    public void TestOutlineMargin()
    {
        _designer.OutlineMargin = 0.02f;
        Assertions.AssertFloat(_designer.OutlineMargin).IsEqual(0.02f);
    }

    private static Node3D CreateMockCardRoot()
    {
        var root = new Node3D();
        root.Name = "CardRoot";
        
        // Create minimal required node structure for testing
        var outerBox = new CsgBox3D();
        outerBox.Name = "OuterBox";
        root.AddChild(outerBox);
        
        var combiner = new CsgCombiner3D();
        combiner.Name = "Combiner";
        outerBox.AddChild(combiner);
        
        // Add corner cylinders
        for (int i = 1; i <= 4; i++)
        {
            var cylinder = new CsgCylinder3D();
            cylinder.Name = $"Corner{i}";
            combiner.AddChild(cylinder);
        }
        
        // Add trim boxes
        for (int i = 1; i <= 2; i++)
        {
            var trimBox = new CsgBox3D();
            trimBox.Name = $"TrimBox{i}";
            combiner.AddChild(trimBox);
        }
        
        var collision = new CollisionShape3D();
        collision.Name = "CardCollision";
        collision.Shape = new BoxShape3D();
        root.AddChild(collision);
        
        var outlineBox = new CsgBox3D();
        outlineBox.Name = "OutlineBox";
        root.AddChild(outlineBox);
        
        return root;
    }
}