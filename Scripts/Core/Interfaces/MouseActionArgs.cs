namespace CardCleaner.Scripts.Core.Interfaces;

/// <summary>
///     What a registered mouse action is told about one button event: whether the button went down or up, and whether
///     Shift was held. Shift comes from the event itself, not the polled key state, so injected events carry it too.
/// </summary>
public readonly record struct MouseActionArgs(bool Pressed, bool Shift);
