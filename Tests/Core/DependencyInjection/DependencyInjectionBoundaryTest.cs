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
        var types = typeof(ILog).Assembly.GetTypes().Where(type => IsIn(type, DiNamespace)).ToList();
        var hits = types.SelectMany(type => TypeReferences.Lines(type, IsForbiddenFromDi)).ToList();

        // A scan that sees too few types passes trivially, so it must find the whole folder, the service host included.
        AssertInt(types.Count(type => !type.IsNested)).IsGreater(3);
        AssertThat(types).Contains(typeof(ServiceHost), typeof(ServiceContainer));

        // Joined into one string so a failure names every reference, not just how many there are.
        AssertThat(string.Join(System.Environment.NewLine, hits)).IsEmpty();
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void NoFeatureTypeNamesTheCompositionRoot()
    {
        var features = typeof(ILog).Assembly.GetTypes().Where(type => IsIn(type, FeaturesNamespace)).ToList();
        var hits = features.SelectMany(type => TypeReferences.Lines(type, IsComposition)).ToList();

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
    public static void ContractAndHostReferencesAreNotReported()
    {
        AssertThat(ProbeHits(typeof(ProbeAllowed), IsForbiddenFromDi)).IsEmpty();
    }

    // The probes live in this test's namespace, which the DI and Features filters never select.
    private static List<string> ProbeHits(Type probe, Func<Type, bool> isForbidden) =>
        TypeReferences.Lines(probe, isForbidden).ToList();

    private static List<string> Hit(string probe, Type foreign) =>
        new() { $"{typeof(DependencyInjectionBoundaryTest).FullName}+{probe} -> {foreign.FullName}" };

    private static bool IsForbiddenFromDi(Type type) =>
        IsIn(type, FeaturesNamespace) || IsIn(type, CoreServicesNamespace) || IsIn(type, CompositionNamespace);

    private static bool IsComposition(Type type) => IsIn(type, CompositionNamespace);

    private static bool IsIn(Type type, string root) => TypeReferences.InNamespace(type.Namespace, root);

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
        public static void Rebuild() => CompositionRoot.RegisterGameServiceClasses(new ServiceContainer());
    }

    private static class ProbeAllowed
    {
        public static IServiceContainer Build() => new ServiceContainer();
    }
}
