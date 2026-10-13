using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace CardCleaner.Tests.Architecture.DependencyGate;

/// <summary>
/// Builds the class graph of the script sources with one Roslyn compilation (#240 requirements 1 to 3). Addon sources
/// compile alongside so their names bind, but only script types become nodes. Godot's source generators do not run,
/// so members that only a generator declares (signal helpers) bind to nothing and add no edge.
/// </summary>
internal static class ClassGraphExtractor
{
    // ASSUMPTION: these symbols and LanguageVersion.CSharp12 in Compile mirror CardCleaner.csproj (Godot.NET.Sdk
    // 4.7.1, net8.0, LangVersion 12.0) and must change with it on an SDK, framework or language bump.
    private static readonly string[] PreprocessorSymbols =
    [
        "GODOT", "GODOT4", "GODOT4_7", "GODOT4_7_1", "GODOT4_OR_GREATER", "GODOT4_7_OR_GREATER", "TOOLS", "DEBUG",
        "GODOT_PC", "GODOT_WINDOWS", "NET", "NETCOREAPP", "NET8_0", "NET8_0_OR_GREATER", "NET7_0_OR_GREATER",
        "NET6_0_OR_GREATER", "NET5_0_OR_GREATER",
    ];

    /// <summary>The graph plus the script file each class is declared in, for the scene link reader.</summary>
    internal sealed record Result(ClassGraph Graph, IReadOnlyDictionary<string, IReadOnlyList<string>> ClassesByFile,
        int ScriptErrors);

    private sealed class NodeFacts
    {
        public required INamedTypeSymbol Symbol { get; init; }
        public required string Key { get; init; }
        public List<string> Files { get; } = [];
        public bool UsesEngineApi { get; set; }
    }

    // The references found in one source tree. Trees are walked in parallel and merged in path order.
    private sealed class References
    {
        public SortedDictionary<(string From, string To, EdgeKind Kind), int> Counts { get; } = new();
        public SortedSet<string> ApiUsers { get; } = new(StringComparer.Ordinal);
        public int Errors { get; set; }
    }

    public static Result Extract(IReadOnlyList<SourceFile> scripts, IReadOnlyList<SourceFile> addons,
        IReadOnlyList<MetadataReference> references)
    {
        var (compilation, scriptTrees) = Compile(scripts, addons, references);
        var (facts, topDeclarations) = CollectDeclarations(compilation, scriptTrees);
        var merged = CollectReferences(compilation, facts, topDeclarations);
        return BuildResult(facts, merged.Counts, merged.Errors);
    }

    private static (CSharpCompilation Compilation, List<SyntaxTree> ScriptTrees) Compile(
        IReadOnlyList<SourceFile> scripts, IReadOnlyList<SourceFile> addons,
        IReadOnlyList<MetadataReference> references)
    {
        var parse = new CSharpParseOptions(LanguageVersion.CSharp12, preprocessorSymbols: PreprocessorSymbols);
        var scriptTrees = scripts.OrderBy(s => s.RelativePath, StringComparer.Ordinal)
            .Select(s => CSharpSyntaxTree.ParseText(s.Text, parse, s.RelativePath)).ToList();
        var addonTrees = addons.OrderBy(s => s.RelativePath, StringComparer.Ordinal)
            .Select(s => CSharpSyntaxTree.ParseText(s.Text, parse, s.RelativePath));
        var options = new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary,
            nullableContextOptions: NullableContextOptions.Enable, allowUnsafe: true);
        var compilation = CSharpCompilation.Create("DependencyGate", scriptTrees.Concat(addonTrees), references,
            options);
        return (compilation, scriptTrees);
    }

    private static (SortedDictionary<string, NodeFacts> Facts,
        List<(SyntaxTree Tree, SyntaxNode Declaration, string Key)> Declarations) CollectDeclarations(CSharpCompilation compilation, List<SyntaxTree> scriptTrees)
    {
        var facts = new SortedDictionary<string, NodeFacts>(StringComparer.Ordinal);
        var topDeclarations = new List<(SyntaxTree Tree, SyntaxNode Declaration, string Key)>();
        foreach (var tree in scriptTrees)
        {
            var model = compilation.GetSemanticModel(tree);
            foreach (var declaration in TopLevelDeclarations(tree.GetRoot()))
            {
                if (model.GetDeclaredSymbol(declaration) is not INamedTypeSymbol symbol) continue;
                var key = Key(symbol);
                if (!facts.TryGetValue(key, out var fact))
                {
                    fact = new NodeFacts { Symbol = symbol.OriginalDefinition, Key = key };
                    facts.Add(key, fact);
                }

                if (!fact.Files.Contains(tree.FilePath)) fact.Files.Add(tree.FilePath);
                topDeclarations.Add((tree, declaration, key));
            }
        }

        return (facts, topDeclarations);
    }

    private static References CollectReferences(CSharpCompilation compilation,
        SortedDictionary<string, NodeFacts> facts, List<(SyntaxTree Tree, SyntaxNode Declaration, string Key)> topDeclarations)
    {
        // Binding is the slow part, so each tree is walked on its own thread with its own semantic model. The
        // per-tree results are merged in path order, so the outcome does not depend on thread timing.
        var byTree = topDeclarations.GroupBy(d => d.Tree).ToList();
        var perTree = new References[byTree.Count];
        Parallel.For(0, byTree.Count, index =>
        {
            var sink = new References();
            var model = compilation.GetSemanticModel(byTree[index].Key);
            foreach (var (_, declaration, from) in byTree[index])
            {
                foreach (var syntax in declaration.DescendantNodesAndSelf())
                    CollectReference(model, syntax, from, facts, sink);
            }

            sink.Errors = model.GetDiagnostics().Count(d => d.Severity == DiagnosticSeverity.Error);
            perTree[index] = sink;
        });

        var merged = new References();
        foreach (var sink in perTree)
        {
            foreach (var (key, count) in sink.Counts) merged.Counts[key] = merged.Counts.GetValueOrDefault(key) + count;
            foreach (var user in sink.ApiUsers) facts[user].UsesEngineApi = true;
            merged.Errors += sink.Errors;
        }

        return merged;
    }

    // Every type declaration that is not nested in another type: a nested type folds into its outer type.
    private static IEnumerable<SyntaxNode> TopLevelDeclarations(SyntaxNode root) =>
        root.DescendantNodes(n => n is not BaseTypeDeclarationSyntax)
            .Where(n => n is BaseTypeDeclarationSyntax or DelegateDeclarationSyntax);

    private static INamedTypeSymbol Outer(INamedTypeSymbol type)
    {
        while (type.ContainingType != null) type = type.ContainingType;
        return type.OriginalDefinition;
    }

    private static string Key(INamedTypeSymbol type) =>
        Outer(type).ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat);

    private static void CollectReference(SemanticModel model, SyntaxNode syntax, string from,
        SortedDictionary<string, NodeFacts> facts, References sink)
    {
        if (syntax is ImplicitObjectCreationExpressionSyntax creation)
        {
            if (model.GetTypeInfo(creation).Type is INamedTypeSymbol created)
                AddReference(from, created, EdgeKind.Creates, facts, sink);
            return;
        }

        if (syntax is not SimpleNameSyntax name || name is IdentifierNameSyntax { IsVar: true }) return;
        var info = model.GetSymbolInfo(name);
        var symbol = info.Symbol ?? info.CandidateSymbols.FirstOrDefault();
        if (symbol is IAliasSymbol alias) symbol = alias.Target;
        var (target, kind) = TargetOf(symbol, name);
        AddReference(from, target, kind, facts, sink);
    }

    // The type a bound name refers to, and how: the type itself, a constructor's, a method's or a member's owner.
    private static (INamedTypeSymbol? Target, EdgeKind Kind) TargetOf(ISymbol? symbol, SyntaxNode name) => symbol switch
    {
        INamedTypeSymbol type => (type, ClassifyTypePosition(name)),
        IMethodSymbol { MethodKind: MethodKind.Constructor } constructor =>
            (constructor.ContainingType, EdgeKind.Creates),
        IMethodSymbol method => ((method.ReducedFrom ?? method).ContainingType, EdgeKind.Calls),
        IPropertySymbol or IFieldSymbol or IEventSymbol => (symbol.ContainingType, EdgeKind.Uses),
        _ => (null, EdgeKind.Uses),
    };

    private static void AddReference(string from, INamedTypeSymbol? target, EdgeKind kind,
        SortedDictionary<string, NodeFacts> facts, References sink)
    {
        if (target == null) return;
        var outer = Outer(target);
        var to = Key(outer);
        if (to == from) return;
        if (facts.ContainsKey(to))
        {
            sink.Counts[(from, to, kind)] = sink.Counts.GetValueOrDefault((from, to, kind)) + 1;
            return;
        }

        // UI rule 1: naming any GodotSharp type that is not a value type (struct or enum) is engine API.
        if (outer.ContainingAssembly?.Name.StartsWith("GodotSharp", StringComparison.Ordinal) == true
            && !outer.IsValueType)
            sink.ApiUsers.Add(from);
    }

    // Where a type name sits decides the edge kind: base list, member signature, creation, or any other use.
    private static EdgeKind ClassifyTypePosition(SyntaxNode name)
    {
        var current = name;
        while (current.Parent is TypeSyntax or TypeArgumentListSyntax or TupleElementSyntax
               or ArrayRankSpecifierSyntax)
        {
            if (current.Parent is GenericNameSyntax generic
                && generic.Parent is MemberAccessExpressionSyntax or InvocationExpressionSyntax)
            {
                current = generic;
                break;
            }

            current = current.Parent;
        }

        return current.Parent switch
        {
            SimpleBaseTypeSyntax or PrimaryConstructorBaseTypeSyntax => EdgeKind.Inherits,
            VariableDeclarationSyntax { Parent: FieldDeclarationSyntax or EventFieldDeclarationSyntax } =>
                EdgeKind.MemberType,
            PropertyDeclarationSyntax or IndexerDeclarationSyntax or EventDeclarationSyntax => EdgeKind.MemberType,
            ParameterSyntax { Parent: null } => EdgeKind.Uses,
            ParameterSyntax { Parent.Parent: AnonymousFunctionExpressionSyntax or LocalFunctionStatementSyntax } =>
                EdgeKind.Uses,
            ParameterSyntax => EdgeKind.MemberType,
            MethodDeclarationSyntax or DelegateDeclarationSyntax or OperatorDeclarationSyntax
                or ConversionOperatorDeclarationSyntax => EdgeKind.MemberType,
            TypeConstraintSyntax => EdgeKind.MemberType,
            ObjectCreationExpressionSyntax => EdgeKind.Creates,
            _ => EdgeKind.Uses,
        };
    }

    private static Result BuildResult(SortedDictionary<string, NodeFacts> facts,
        SortedDictionary<(string From, string To, EdgeKind Kind), int> siteCounts, int scriptErrors)
    {
        var nameCounts = facts.Values.GroupBy(f => f.Symbol.Name, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.Count(), StringComparer.Ordinal);
        var ids = new SortedDictionary<string, string>(StringComparer.Ordinal);
        var graph = new ClassGraph();
        var classesByFile = new SortedDictionary<string, List<string>>(StringComparer.Ordinal);
        foreach (var fact in facts.Values)
        {
            var id = UniqueId(fact, nameCounts, graph);
            ids.Add(fact.Key, id);
            var isGodot = NodeRoleRules.DerivesFromGodotObject(fact.Symbol);
            graph.AddNode(new GateNode(id, NodeRoleRules.Role(fact.Symbol, isGodot), Home(fact.Files[0]),
                isGodot || fact.UsesEngineApi));
            foreach (var file in fact.Files)
            {
                if (!classesByFile.TryGetValue(file, out var list)) classesByFile.Add(file, list = []);
                list.Add(id);
            }
        }

        foreach (var ((from, to, kind), count) in siteCounts) graph.AddEdge(ids[from], ids[to], kind, count);
        var byFile = classesByFile.ToDictionary(p => p.Key, p => (IReadOnlyList<string>)p.Value,
            StringComparer.Ordinal);
        return new Result(graph, byFile, scriptErrors);
    }

    // ASSUMPTION: a name that is still taken after the namespace suffix (two types with one name in one namespace
    // tail) falls back to the full display name, so no node is ever merged into another.
    private static string UniqueId(NodeFacts fact, Dictionary<string, int> nameCounts, ClassGraph graph)
    {
        var name = fact.Symbol.Name;
        if (nameCounts[name] == 1) return name;
        var tail = fact.Symbol.ContainingNamespace?.ToDisplayString().Split('.')[^1] ?? "";
        var id = $"{name}@{tail}";
        return graph.Nodes.ContainsKey(id) ? fact.Key : id;
    }

    /// <summary>Features/X, Core/X (or Core), or the first folder under Scripts/.</summary>
    internal static string Home(string relativePath)
    {
        var parts = relativePath.Split('/');
        var folders = parts.Skip(1).Take(Math.Max(0, parts.Length - 2)).ToArray();
        if (folders.Length == 0) return "Scripts";
        if (folders[0] is "Features" or "Core") return folders.Length > 1 ? $"{folders[0]}/{folders[1]}" : folders[0];
        return folders[0];
    }
}
