using CardCleaner.Scripts.Features.Card.Components;
using CardCleaner.Scripts.Features.Card.Controllers;
using CardCleaner.Scripts.Features.Card.Models;
using Godot;

namespace CardCleaner.Tests.Features.Shop;

/// <summary>Builds card nodes for shop tests: the real card footprint and the parts CardDesigner requires.</summary>
public static class ShopTestCards
{
    private const string CardScene = "res://Scenes/Components/CardShader.tscn";
    private const string CollisionNode = "CardCollision";

    /// <summary>
    ///     The size of the real card's collision box, read from the card scene, so a test card fills the space a
    ///     spawned card does. The card's designer sizes the box again on setup, to its defaults, which
    ///     <c>CardDesignerTest</c> holds to the same size.
    /// </summary>
    public static readonly Vector3 CardSize = RealCardSize();

    private static int _created;

    /// <summary>Card names must stay unique under one parent, or Godot renames them and slots ignore them.</summary>
    public static CardController Create(CardSignature? signature)
    {
        var card = new CardController { Name = $"Card{++_created}" };
        if (signature.HasValue)
            card.Signature = signature.Value;

        card.AddChild(new CollisionShape3D { Name = CollisionNode, Shape = new BoxShape3D { Size = CardSize } });
        card.AddChild(new CardDesigner { Name = "Designer" });
        return card;
    }

    private static Vector3 RealCardSize()
    {
        var state = GD.Load<PackedScene>(CardScene).GetState();
        for (var node = 0; node < state.GetNodeCount(); node++)
        {
            if (state.GetNodeName(node) != CollisionNode)
                continue;
            for (var property = 0; property < state.GetNodePropertyCount(node); property++)
            {
                if (state.GetNodePropertyName(node, property) == CollisionShape3D.PropertyName.Shape
                    && state.GetNodePropertyValue(node, property).AsGodotObject() is BoxShape3D box)
                    return box.Size;
            }
        }

        throw new System.InvalidOperationException($"{CardScene} has no {CollisionNode} box");
    }
}
