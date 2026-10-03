namespace CardCleaner.Scripts.Features.Worldgen.Wfc.Constraints;

/// <summary>Applies a probability modifier to a candidate WFC tile.</summary>
public interface IWfcConstraint
{
    /// <summary>Calculates the probability modifier for placing a tile.</summary>
    float GetProbabilityModifier(WfcConstraintContext context);
}
