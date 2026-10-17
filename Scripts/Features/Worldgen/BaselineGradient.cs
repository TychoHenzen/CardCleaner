using CardCleaner.Scripts.Features.Card.Models;
using Godot;

namespace CardCleaner.Scripts.Features.Worldgen;

public abstract class BaselineGradient
{
    public abstract CardSignature GetSignatureAt(Vector2I position, Vector2I mapSize);
}
