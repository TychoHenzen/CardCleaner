using CardCleaner.Scripts.Features.Card.Components;
using CardCleaner.Scripts.Features.Card.Controllers;
using CardCleaner.Scripts.Features.Card.Models;
using Godot;

namespace CardCleaner.Tests.Features.Shop;

/// <summary>Builds card nodes for shop tests: the real card footprint and the parts CardDesigner requires.</summary>
public static class ShopTestCards
{
    public static readonly Vector3 CardSize = new(0.635f, 0.005f, 0.889f);

    private static int _created;

    /// <summary>Card names must stay unique under one parent, or Godot renames them and slots ignore them.</summary>
    public static CardController Create(CardSignature? signature)
    {
        var card = new CardController { Name = $"Card{++_created}", Signature = signature! };
        var outer = new CsgBox3D { Name = "OuterBox" };
        outer.AddChild(new CsgCombiner3D { Name = "Combiner" });
        card.AddChild(outer);
        card.AddChild(new CollisionShape3D { Name = "CardCollision", Shape = new BoxShape3D { Size = CardSize } });
        card.AddChild(new CardDesigner { Name = "Designer" });
        return card;
    }
}
