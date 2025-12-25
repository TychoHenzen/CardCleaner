using CardCleaner.Scripts.Features.Card.Models;
using Godot;

namespace CardCleaner.Scripts.Features.Worldgen.Biomes;

public interface IBiomeProvider
{
    BiomeDefinition GetBiomeAt(Vector2I position);
    CardSignature GetSignatureAt(Vector2I position);
}
