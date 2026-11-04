using System;
using CardCleaner.Scripts.Core.Interfaces;
using CardCleaner.Tests.TestUtilities;
using Godot;

namespace CardCleaner.Tests.Scenes;

/// <summary>
///     Does what the Services autoload does in the game: injects every <see cref="IDependent" /> that enters the
///     tree, so a box that the ordering service delivers finds its services on its own.
/// </summary>
internal sealed class TreeInjection : IDisposable
{
    private readonly SceneTree _tree;

    public TreeInjection(SceneTree tree)
    {
        _tree = tree;
        _tree.NodeAdded += OnNodeAdded;
    }

    public void Dispose() => _tree.NodeAdded -= OnNodeAdded;

    private static void OnNodeAdded(Node node)
    {
        if (node is IDependent dependent)
            dependent.Inject(TestServices.Resolver);
    }
}
