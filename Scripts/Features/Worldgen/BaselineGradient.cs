using CardCleaner.Scripts.Features.Card.Models;
using Godot;

namespace CardCleaner.Scripts.Features.Worldgen;

public abstract partial class BaselineGradient : Resource
{
    public abstract CardSignature GetSignatureAt(Vector2I position, Vector2I mapSize);
}
