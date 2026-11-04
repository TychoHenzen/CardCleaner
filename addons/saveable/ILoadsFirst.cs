namespace Saveable;

/// <summary>
/// An <see cref="ISaveable"/> that creates or replaces nodes when it loads. <see cref="SaveSystem"/> loads every
/// such node before it loads the rest of the tree, so the nodes it makes are in the tree when the other saveables
/// among them (and their children) receive their data.
/// </summary>
public interface ILoadsFirst : ISaveable
{
}
