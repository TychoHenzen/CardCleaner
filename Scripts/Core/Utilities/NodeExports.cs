using CardCleaner.Scripts.Core.Interfaces;
using Godot;

namespace CardCleaner.Scripts.Core.Utilities;

/// <summary>Checks that the node references a scene assigns to a node's exports are set.</summary>
public static class NodeExports
{
    /// <summary>Returns false when the export is set; otherwise logs it as unassigned and returns true.</summary>
    public static bool ExportCheck(Node? export, string name, Node target)
    {
        if (export != null)
            return false;
        ILog.Error($"{name} not assigned to {target.Name}");
        return true;
    }
}
