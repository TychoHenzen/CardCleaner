using System.Collections.Generic;

namespace CardCleaner.Scripts.Features.Worldgen.Transitions;

/// <summary>
/// Registry of all transition rules between terrain groups.
/// </summary>
public class TransitionRuleRegistry
{
    private readonly Dictionary<(string From, string To), TransitionRule> _rules = new();

    public int Count => _rules.Count;

    /// <summary>
    /// Register a transition rule
    /// </summary>
    public void RegisterRule(TransitionRule rule) =>
        _rules[(rule.FromGroupId, rule.ToGroupId)] = rule;

    /// <summary>
    /// Get a specific transition rule by group IDs
    /// </summary>
    public TransitionRule? GetRule(string fromGroupId, string toGroupId) =>
        _rules.GetValueOrDefault((fromGroupId, toGroupId));

    /// <summary>
    /// Get the appropriate transition rule for two groups, considering priority.
    /// The higher priority group's edges render on top.
    /// </summary>
    /// <param name="group1">First terrain group</param>
    /// <param name="group2">Second terrain group</param>
    /// <returns>The transition rule where fromGroup is the higher priority group, or null if no rule exists</returns>
    public TransitionRule? GetRuleByPriority(TerrainGroup group1, TerrainGroup group2)
    {
        // Higher priority group should be "from" (its edges render on top)
        var (highPriority, lowPriority) = group1.Priority >= group2.Priority
            ? (group1, group2)
            : (group2, group1);

        // Try to find a rule in the preferred direction
        var rule = GetRule(highPriority.Id, lowPriority.Id);
        if (rule != null)
            return rule;

        // Check reverse direction as fallback
        return GetRule(lowPriority.Id, highPriority.Id);
    }

    /// <summary>
    /// Get all registered transition rules
    /// </summary>
    public IEnumerable<TransitionRule> GetAllRules() => _rules.Values;

    /// <summary>
    /// Clear all registered rules
    /// </summary>
    public void Clear() => _rules.Clear();
}
