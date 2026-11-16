namespace CardCleaner.Scripts.Core.Interfaces;

/// <summary>One key hint on the player's screen: the key or button to press and what it does now.</summary>
public readonly record struct ControlHint(string Key, string Action);
