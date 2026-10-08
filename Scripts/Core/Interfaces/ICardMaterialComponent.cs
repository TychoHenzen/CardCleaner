using CardCleaner.Scripts.Features.Card.Models;
using Godot;

namespace CardCleaner.Scripts.Core.Interfaces;

public interface ICardMaterialComponent
{
    void SetLayerTextures(Data.LayerData[] layers);
    ShaderMaterial? ApplyMaterial(MeshInstance3D target);
    void SetGemEmission(int index, Color color, float strength);

    /// <summary>
    ///     Sets the art-region effects: the rarity and condition effect, the seed that places the scratches, and the
    ///     bevel map the rarity effects shade. A null map leaves the shader's default, which shows no rarity effect.
    /// </summary>
    void SetArtEffects(RarityEffect rarity, ConditionEffect condition, float seed, Texture2D? normalMap);
}