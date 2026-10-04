namespace CardCleaner.Scripts.Core.Devices;

/// <summary>
/// Full jack interface combining input and output capabilities.
/// Concrete implementations use this; devices expose the specific directional interface.
/// </summary>
public interface IJack<T> : IInputJack<T>, IOutputJack<T>
{
}
