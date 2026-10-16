using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using CardCleaner.Scripts.Core.DependencyInjection;
using CardCleaner.Scripts.Core.Interfaces;
using CardCleaner.Scripts.Core.Services;
using CardCleaner.Scripts.Features.Card.Models;
using CardCleaner.Scripts.Features.Deckbuilder.Tiles;
using CardCleaner.Tests.TestUtilities;
using Godot;
using FeatureRoot = CardCleaner.Scripts.Features;

namespace CardCleaner.Tests.Core.Interfaces;

/// <summary>
/// Scripts/Core/Interfaces is the bottom layer (#167, decision Q2 A). Every type a contract names, in a base type, an
/// interface, a member signature, a generic argument, an attribute or a method body, must be a contract itself, a
/// System or Godot type, or one of the allowed types below. The check reads the compiled types, so a using alias, a
/// partly qualified name or a name resolved through an enclosing namespace cannot hide a reference.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class ContractsLayerBoundaryTest
{
    private const string ContractsNamespace = "CardCleaner.Scripts.Core.Interfaces";
    private const string ContractsRoot = "res://Scripts/Core/Interfaces";

    // CardSignature: the one feature type a contract may name (#167, decision Q1 A); it depends on no feature code.
    // LightMode: a plain enum in Core.Enumeration that IGameSettings names; that namespace imports nothing from
    // CardCleaner (#167, decided in the owner's absence; the alternative is moving LightMode into the contracts).
    private static readonly HashSet<string> AllowedTypes = new(StringComparer.Ordinal)
    {
        "CardCleaner.Scripts.Features.Card.Models.CardSignature",
        "CardCleaner.Scripts.Core.Enumeration.LightMode",
    };

    private static readonly Regex NamespaceDeclaration = new(@"\bnamespace\s+([\w.]+)", RegexOptions.Compiled);

    [TestCase]
    [TestCategory("Unit")]
    public static void NoContractTypeNamesATypeOutsideTheContractsLayer()
    {
        var contracts = typeof(ILog).Assembly.GetTypes().Where(type => IsContract(type.Namespace)).ToList();
        var hits = contracts.SelectMany(type => TypeReferences.Lines(type, named => !IsAllowed(named))).ToList();

        // A scan that sees too few types passes trivially, so it must find the whole layer, including the two types
        // whose references #167 cut (ILog's locator calls, MapGenerationConfig's biome registry).
        AssertInt(contracts.Count(type => !type.IsNested)).IsGreater(23);
        AssertThat(contracts).Contains(typeof(ILog), typeof(MapGenerationConfig));

        // Joined into one string so a failure names every reference, not just how many there are.
        AssertThat(string.Join(System.Environment.NewLine, hits)).IsEmpty();
    }

    // The type scan only sees the contracts namespace, so a file under the folder that declares another namespace
    // would escape it.
    [TestCase]
    [TestCategory("Unit")]
    public static void EveryContractFileDeclaresAContractsNamespace()
    {
        var files = SourceScan.CsFiles(ContractsRoot);
        var strays = files
            .SelectMany(path => NamespaceDeclaration.Matches(SourceScan.Blanked(path))
                .Select(match => match.Groups[1].Value)
                .Where(name => !IsContract(name))
                .Select(name => $"{SourceScan.RelativePath(path)} {name}"))
            .ToList();

        AssertInt(files.Count).IsGreater(23);
        AssertThat(string.Join(System.Environment.NewLine, strays)).IsEmpty();
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void FeatureTypeInASignatureIsReported()
    {
        AssertThat(ProbeHits(typeof(ProbeFeatureSignature)))
            .IsEqual(Hit("ProbeFeatureSignature", typeof(CardTemplate)));
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void FeatureTypeThroughABareRootAliasIsReported()
    {
        AssertThat(ProbeHits(typeof(ProbeAliasedFeature))).IsEqual(Hit("ProbeAliasedFeature", typeof(CardTemplate)));
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void FeatureTypeAsAGenericArgumentIsReported()
    {
        AssertThat(ProbeHits(typeof(ProbeGenericArgument)))
            .IsEqual(Hit("ProbeGenericArgument", typeof(TileDefinition)));
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void ServiceHostInAMethodBodyIsReported()
    {
        AssertThat(ProbeHits(typeof(ProbeHostInBody))).IsEqual(Hit("ProbeHostInBody", typeof(ServiceHost)));
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void NonFeatureCoreTypeIsReported()
    {
        AssertThat(ProbeHits(typeof(ProbeCoreService))).IsEqual(Hit("ProbeCoreService", typeof(LoggingService)));
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void AllowedTypesAreNotReported()
    {
        AssertThat(ProbeHits(typeof(ProbeAllowed))).IsEmpty();
    }

    // The probes live in this test's namespace, which stands in for the contracts namespace here.
    private static List<string> ProbeHits(Type probe) =>
        TypeReferences.Lines(probe, type => !IsAllowed(type) && type.Namespace != probe.Namespace).ToList();

    private static List<string> Hit(string probe, Type foreign) =>
        new() { $"{typeof(ContractsLayerBoundaryTest).FullName}+{probe} -> {foreign.FullName}" };

    private static bool IsContract(string? name) => TypeReferences.InNamespace(name, ContractsNamespace);

    private static bool IsAllowed(Type type)
    {
        var name = type.Namespace;
        if (name == null)
            return type.Name.StartsWith('<'); // compiler-generated helpers such as <PrivateImplementationDetails>
        return IsContract(name) || TypeReferences.InNamespace(name, "System") ||
            TypeReferences.InNamespace(name, "Godot") || AllowedTypes.Contains(type.FullName ?? "");
    }

    private interface ProbeFeatureSignature
    {
        CardTemplate Template { get; }
    }

    private interface ProbeAliasedFeature
    {
        void Use(FeatureRoot.Card.Models.CardTemplate template);
    }

    private interface ProbeGenericArgument
    {
        IReadOnlyList<TileDefinition> Tiles { get; }
    }

    private static class ProbeHostInBody
    {
        public static bool Ready() => new ServiceHost().Has<ILog>();
    }

    private static class ProbeCoreService
    {
        public static int Code() => new LoggingService().GetHashCode();
    }

    private static class ProbeAllowed
    {
        public static CardSignature[] Seeds(Vector2I size, IMapData map) =>
            map.CellCount == size.X ?new[] { new CardSignature() } : Array.Empty<CardSignature>();
    }
}
