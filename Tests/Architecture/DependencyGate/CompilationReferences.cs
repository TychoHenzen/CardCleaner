using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.CodeAnalysis;

namespace CardCleaner.Tests.Architecture.DependencyGate;

internal static class CompilationReferences
{
    /// <summary>
    /// Compilation references from the running host: the framework's trusted platform assemblies plus the loaded
    /// GodotSharp, GodotSharpEditor and Newtonsoft.Json assemblies. The game assembly is never referenced, because
    /// its types would collide with the source types.
    /// </summary>
    public static IReadOnlyList<MetadataReference> FromHost()
    {
        var paths = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
        if (AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") is string trusted)
        {
            foreach (var path in trusted.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
                paths.Add(path);
        }

        string[] extra = ["GodotSharp", "GodotSharpEditor", "Newtonsoft.Json"];
        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            if (assembly.IsDynamic || string.IsNullOrEmpty(assembly.Location)) continue;
            if (extra.Contains(assembly.GetName().Name, StringComparer.Ordinal)) paths.Add(assembly.Location);
        }

        var gameAssembly = typeof(CompilationReferences).Assembly.Location;
        return paths.Where(p => p.EndsWith(".dll", StringComparison.OrdinalIgnoreCase)
                                && !string.Equals(p, gameAssembly, StringComparison.OrdinalIgnoreCase))
            .Select(p => (MetadataReference)MetadataReference.CreateFromFile(p)).ToList();
    }
}
