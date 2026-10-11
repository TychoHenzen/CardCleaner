using System;
using System.Collections.Generic;
using System.Linq;
using CardCleaner.Scripts.Composition;
using CardCleaner.Scripts.Core.DependencyInjection;
using CardCleaner.Scripts.Core.Interfaces;
using CardCleaner.Scripts.Core.Services;
using CardCleaner.Scripts.Features.Card.Models;
using CardCleaner.Tests.TestUtilities;

namespace CardCleaner.Tests.Core.DependencyInjection;

/// <summary>
/// Scripts/Core/DependencyInjection is the lookup layer that the composition root fills (#168). Its types may name the
/// Core contracts and System or Godot types. They may not name a feature type or a Core service type, and no feature
/// type may name the composition root. The check reads the compiled types, as ContractsLayerBoundaryTest does, so a
/// using alias or a partly qualified name cannot hide a reference.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class DependencyInjectionBoundaryTest
{
    private const string DiNamespace = "CardCleaner.Scripts.Core.DependencyInjection";
    private const string FeaturesNamespace = "CardCleaner.Scripts.Features";
    private const string CoreServicesNamespace = "CardCleaner.Scripts.Core.Services";
    private const string CompositionNamespace = "CardCleaner.Scripts.Composition";

    [TestCase]
    [TestCategory("Unit")]
    public static void NoDiTypeNamesAFeatureServiceOrCompositionType()
    {
        var types = typeof(ILog).Assembly.GetTypes().Where(type => IsIn(type.Namespace, DiNamespace)).ToList();
        var hits = types.SelectMany(type => ForbiddenReferences(type, IsForbiddenFromDi)).ToList();

        // A scan that sees too few types passes trivially, so it must find the whole folder, the locator included.
        AssertInt(types.Count(type => !type.IsNested)).IsGreater(3);
        AssertThat(types).Contains(typeof(ServiceLocator), typeof(ServiceContainer));

        // Joined into one string so a failure names every reference, not just how many there are.
        AssertThat(string.Join(System.Environment.NewLine, hits)).IsEmpty();
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void NoFeatureTypeNamesTheCompositionRoot()
    {
        var features = typeof(ILog).Assembly.GetTypes().Where(type => IsIn(type.Namespace, FeaturesNamespace)).ToList();
        var hits = features.SelectMany(type => ForbiddenReferences(type, IsComposition)).ToList();

        AssertInt(features.Count).IsGreater(100);
        AssertThat(string.Join(System.Environment.NewLine, hits)).IsEmpty();
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void CoreServiceInABodyIsReported()
    {
        AssertThat(ProbeHits(typeof(ProbeCoreService), IsForbiddenFromDi))
            .IsEqual(Hit("ProbeCoreService", typeof(LoggingService)));
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void FeatureTypeInASignatureIsReported()
    {
        AssertThat(ProbeHits(typeof(ProbeFeature), IsForbiddenFromDi))
            .IsEqual(Hit("ProbeFeature", typeof(CardTemplate)));
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void CompositionRootInABodyIsReported()
    {
        AssertThat(ProbeHits(typeof(ProbeComposition), IsComposition))
            .IsEqual(Hit("ProbeComposition", typeof(CompositionRoot)));
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void ContractAndLocatorReferencesAreNotReported()
    {
        AssertThat(ProbeHits(typeof(ProbeAllowed), IsForbiddenFromDi)).IsEmpty();
    }

    // The probes live in this test's namespace, which the DI and Features filters never select.
    private static List<string> ProbeHits(Type probe, Func<Type, bool> isForbidden) =>
        ForbiddenReferences(probe, isForbidden).ToList();

    private static List<string> Hit(string probe, Type foreign) =>
        new() { $"{typeof(DependencyInjectionBoundaryTest).FullName}+{probe} -> {foreign.FullName}" };

    // One "Contract -> Foreign.Type" line per distinct type the contract names that the filter selects.
    private static IEnumerable<string> ForbiddenReferences(Type contract, Func<Type, bool> isForbidden) =>
        TypeReferences.Of(contract)
            .Where(isForbidden)
            .Select(type => $"{contract.FullName} -> {type.FullName ?? type.Name}")
            .OrderBy(line => line, StringComparer.Ordinal);

    private static bool IsForbiddenFromDi(Type type) =>
        IsIn(type.Namespace, FeaturesNamespace) || IsIn(type.Namespace, CoreServicesNamespace) ||
        IsIn(type.Namespace, CompositionNamespace);

    private static bool IsComposition(Type type) => IsIn(type.Namespace, CompositionNamespace);

    private static bool IsIn(string? name, string root) =>
        name != null && (name == root || name.StartsWith(root + ".", StringComparison.Ordinal));

    private static class ProbeCoreService
    {
        public static int Code() => new LoggingService().GetHashCode();
    }

    private static class ProbeFeature
    {
        public static CardTemplate? Template() => null;
    }

    private static class ProbeComposition
    {
        public static void Rebuild() => CompositionRoot.ReinitializeServices();
    }

    private static class ProbeAllowed
    {
        public static IServiceContainer Build() => new ServiceContainer();
    }
}
