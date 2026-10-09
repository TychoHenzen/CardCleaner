using CardCleaner.Scripts.Features.Card.Models;
using CardCleaner.Scripts.Features.Card.Models.Effects;
using Godot;

namespace CardCleaner.Scripts.Features.Card.Components;

public interface ICardMaterialComponent
{
    void SetLayerTextures(LayerData[] layers);
    ShaderMaterial? ApplyMaterial(MeshInstance3D target);
    void SetGemEmission(int index, Color color, float strength);

    /// <summary>
    ///     Starts an art-region request and returns its number. A null map leaves the shader's default, which shows no
    ///     rarity effect; a map arrives later through <see cref="SetArtNormalMap" /> with that number.
    /// </summary>
    int SetArtEffects(RarityEffect rarity, ConditionEffect condition, float seed, Texture2D? normalMap);

    /// <summary>
    ///     Delivers the bevel map of request <paramref name="request" />, unless a newer request has started since.
    /// </summary>
    void SetArtNormalMap(int request, RarityEffect rarity, Texture2D normalMap);

    /// <summary>
    ///     Whether the art effects show. Switching them off keeps the requested effects, which show again when they are
    ///     switched back on.
    /// </summary>
    bool ArtEffectsEnabled { get; set; }
}