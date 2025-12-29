using System;
using System.Collections.Generic;

namespace CardCleaner.Scripts.Features.Worldgen.WeightModifiers;

/// <summary>
/// Pipeline that applies weight modifiers in sequence (Chain of Responsibility pattern).
/// Each modifier adjusts the weights dictionary in-place, and the next modifier sees the cumulative result.
/// </summary>
public sealed class WeightModifierPipeline
{
    private readonly List<IWeightModifier> _modifiers = [];

    /// <summary>Number of registered modifiers.</summary>
    public int Count => _modifiers.Count;

    /// <summary>
    /// Add a modifier to the end of the pipeline.
    /// </summary>
    public WeightModifierPipeline AddModifier(IWeightModifier modifier)
    {
        ArgumentNullException.ThrowIfNull(modifier);
        _modifiers.Add(modifier);
        return this;
    }

    /// <summary>
    /// Insert a modifier at a specific position in the pipeline.
    /// </summary>
    public WeightModifierPipeline InsertModifier(int index, IWeightModifier modifier)
    {
        ArgumentNullException.ThrowIfNull(modifier);
        _modifiers.Insert(index, modifier);
        return this;
    }

    /// <summary>
    /// Remove all modifiers of a specific type.
    /// </summary>
    /// <returns>Number of modifiers removed.</returns>
    public int RemoveModifier<T>() where T : IWeightModifier
    {
        return _modifiers.RemoveAll(m => m is T);
    }

    /// <summary>
    /// Remove a specific modifier instance.
    /// </summary>
    public bool RemoveModifier(IWeightModifier modifier)
    {
        return _modifiers.Remove(modifier);
    }

    /// <summary>
    /// Clear all modifiers from the pipeline.
    /// </summary>
    public void Clear() => _modifiers.Clear();

    /// <summary>
    /// Apply all modifiers in sequence to the context.
    /// Modifiers adjust context.Weights in-place.
    /// </summary>
    public void ApplyAll(TileSelectionContext context)
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
    public IReadOnlyList<IWeightModifier> GetModifiers() => _modifiers.AsReadOnly();
}
