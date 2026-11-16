using System;
using System.Collections.Generic;
using System.Linq;
using CardCleaner.Scripts.Core.DependencyInjection;
using CardCleaner.Scripts.Core.Interfaces;

namespace CardCleaner.Scripts.Core.Services;

/// <summary>
///     The one board of key hints for the whole run. Sources are kept in the order they first published, so a
///     hint line does not jump when another source updates.
/// </summary>
[Service(typeof(IControlHints))]
public sealed class ControlHintBoard : IControlHints
{
    private readonly List<(object Source, IReadOnlyList<ControlHint> Hints)> _entries = [];

    public event Action? Changed;

    public IReadOnlyList<ControlHint> Hints => _entries.SelectMany(entry => entry.Hints).ToList();

    public void Publish(object source, IReadOnlyList<ControlHint> hints)
    {
        if (hints.Count == 0)
        {
            Clear(source);
            return;
        }

        var index = _entries.FindIndex(entry => ReferenceEquals(entry.Source, source));
        if (index >= 0)
        {
            if (_entries[index].Hints.SequenceEqual(hints))
                return;
            _entries[index] = (source, hints.ToArray());
        }
        else
        {
            _entries.Add((source, hints.ToArray()));
        }

        Changed?.Invoke();
    }

    public void Clear(object source)
    {
        if (_entries.RemoveAll(entry => ReferenceEquals(entry.Source, source)) > 0)
            Changed?.Invoke();
    }
}
