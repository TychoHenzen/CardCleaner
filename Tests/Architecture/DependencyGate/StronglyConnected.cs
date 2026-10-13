using System;
using System.Collections.Generic;
using System.Linq;

namespace CardCleaner.Tests.Architecture.DependencyGate;

/// <summary>Tarjan's strongly connected components over nodes in ordinal order; each component sorted ordinally.</summary>
internal static class StronglyConnected
{
    public static List<List<string>> Components(IEnumerable<string> nodes, IEnumerable<EdgeKey> edges)
    {
        var sorted = nodes.Distinct().OrderBy(n => n, StringComparer.Ordinal).ToList();
        var known = sorted.ToHashSet(StringComparer.Ordinal);
        var successors = sorted.ToDictionary(n => n, _ => new SortedSet<string>(StringComparer.Ordinal),
            StringComparer.Ordinal);
        foreach (var edge in edges)
        {
            if (known.Contains(edge.From) && known.Contains(edge.To) && edge.From != edge.To)
                successors[edge.From].Add(edge.To);
        }

        var state = new TarjanState(successors);
        foreach (var node in sorted)
        {
            if (!state.Index.ContainsKey(node)) state.Visit(node);
        }

        return state.Components;
    }

    private sealed class TarjanState(Dictionary<string, SortedSet<string>> successors)
    {
        public Dictionary<string, int> Index { get; } = new(StringComparer.Ordinal);
        public List<List<string>> Components { get; } = [];
        private readonly Dictionary<string, int> _low = new(StringComparer.Ordinal);
        private readonly Stack<string> _stack = new();
        private readonly HashSet<string> _onStack = new(StringComparer.Ordinal);

        public void Visit(string node)
        {
            Index[node] = _low[node] = Index.Count;
            _stack.Push(node);
            _onStack.Add(node);
            foreach (var next in successors[node])
            {
                if (!Index.TryGetValue(next, out var nextIndex))
                {
                    Visit(next);
                    _low[node] = Math.Min(_low[node], _low[next]);
                }
                else if (_onStack.Contains(next))
                {
                    _low[node] = Math.Min(_low[node], nextIndex);
                }
            }

            if (_low[node] != Index[node]) return;
            var component = new List<string>();
            string member;
            do
            {
                member = _stack.Pop();
                _onStack.Remove(member);
                component.Add(member);
            } while (member != node);

            component.Sort(StringComparer.Ordinal);
            Components.Add(component);
        }
    }
}
