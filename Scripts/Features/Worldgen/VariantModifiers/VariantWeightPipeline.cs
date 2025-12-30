using System;
using System.Collections.Generic;

namespace CardCleaner.Scripts.Features.Worldgen.VariantModifiers;

/// <summary>
/// Pipeline that applies variant weight modifiers in sequence (Chain of Responsibility pattern).
/// Each modifier adjusts the variant weights array in-place, and the next modifier sees the cumulative result.
/// </summary>
public sealed class VariantWeightPipeline
{
    private readonly List<IVariantWeightModifier> _modifiers = [];

    /// <summary>Number of registered modifiers.</summary>
    public int Count => _modifiers.Count;

    /// <summary>
    /// Add a modifier to the end of the pipeline.
    /// </summary>
    public VariantWeightPipeline AddModifier(IVariantWeightModifier modifier)
    {
        ArgumentNullException.ThrowIfNull(modifier);
        _modifiers.Add(modifier);
        return this;
    }

    /// <summary>
    /// Insert a modifier at a specific position in the pipeline.
    /// </summary>
    public VariantWeightPipeline InsertModifier(int index, IVariantWeightModifier modifier)
    {
        ArgumentNullException.ThrowIfNull(modifier);
        _modifiers.Insert(index, modifier);
        return this;
    }

    /// <summary>
    /// Remove all modifiers of a specific type.
    /// </summary>
    /// <returns>Number of modifiers removed.</returns>
    public int RemoveModifier<T>() where T : IVariantWeightModifier
    {
        return _modifiers.RemoveAll(m => m is T);
    }

    /// <summary>
    /// Remove a specific modifier instance.
    /// </summary>
    public bool RemoveModifier(IVariantWeightModifier modifier)
    {
        return _modifiers.Remove(modifier);
    }

    /// <summary>
    /// Clear all modifiers from the pipeline.
    /// </summary>
    public void Clear() => _modifiers.Clear();

    /// <summary>
    /// Apply all modifiers in sequence to the context.
    /// Modifiers adjust context.VariantWeights in-place.
    /// </summary>
    public void ApplyAll(VariantSelectionContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        foreach (var modifier in _modifiers)
        {
            modifier.ApplyModifier(context);
        }
    }

    /// <summary>
    /// Get a read-only view of the registered modifiers.
    /// </summary>
    public IReadOnlyList<IVariantWeightModifier> GetModifiers() => _modifiers.AsReadOnly();
}
