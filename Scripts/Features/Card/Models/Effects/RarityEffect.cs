namespace CardCleaner.Scripts.Features.Card.Models.Effects;

/// <summary>
///     The shine a rarity puts on the art region (Axiom2d <c>ShaderVariant</c>). The numbers are the
///     <c>RARITY_*</c> constants in <c>Shaders/card_layers.gdshader</c>; a contract test keeps the two in step.
/// </summary>
public enum RarityEffect
{
    None = 0,
    Embossed = 1,
    Glow = 2,
    Glossy = 3,
    Foil = 4
}
