namespace CardCleaner.Scripts.Features.Card.Models;

/// <summary>
///     The wear or shimmer an intensity tier puts on the art region (Axiom2d <c>ConditionEffect</c>). The numbers
///     are the <c>CONDITION_*</c> constants in <c>Shaders/card_layers.gdshader</c>; a contract test keeps the two
///     in step.
/// </summary>
public enum ConditionEffect
{
    None = 0,
    Worn = 1,
    Shiny = 2
}
