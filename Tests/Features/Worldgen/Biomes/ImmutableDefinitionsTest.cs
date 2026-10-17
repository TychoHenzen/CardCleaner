using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using CardCleaner.Scripts.Core.Services;
using CardCleaner.Scripts.Features.Deckbuilder.Tiles;
using CardCleaner.Scripts.Features.Worldgen.Biomes;
using CardCleaner.Scripts.Features.Worldgen.IrregularMesh.MeshGeneration;
using Godot;

namespace CardCleaner.Tests.Features.Worldgen.Biomes;

[TestSuite]
[RequireGodotRuntime]
public class ImmutableDefinitionsTest
{
    private static readonly Type[] Definitions =
    [
        typeof(TileDefinition), typeof(BiomeDefinition), typeof(VariationGroup),
        typeof(TilePool), typeof(TilePoolEntry), typeof(WorkingFace)
    ];

    [TestCase]
    [TestCategory("Unit")]
    public static void NoDefinitionHasAPublicNonInitSetter()
    {
        var offenders = Definitions
            .SelectMany(type => type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            .Where(property => property.SetMethod is { IsPublic: true } setter
                && !setter.ReturnParameter.GetRequiredCustomModifiers().Contains(typeof(IsExternalInit)))
            .Select(property => $"{property.DeclaringType!.Name}.{property.Name}")
            .ToList();

        AssertThat(string.Join(", ", offenders)).IsEqual("");
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void NoDefinitionHasAPublicVoidMethod()
    {
        var offenders = Definitions
            .SelectMany(type => type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
            .Where(method => method.ReturnType == typeof(void) && !method.IsSpecialName && method.Name != "Deconstruct")
            .Select(method => $"{method.DeclaringType!.Name}.{method.Name}")
            .ToList();

        AssertThat(string.Join(", ", offenders)).IsEqual("");
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void EveryCollectionPropertyIsAReadOnlyInterface()
    {
        var offenders = Definitions
            .SelectMany(type => type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            .Where(property => IsMutableCollection(property.PropertyType))
            .Select(property => $"{property.DeclaringType!.Name}.{property.Name}")
            .ToList();

        AssertThat(string.Join(", ", offenders)).IsEqual("");
    }

    private static bool IsMutableCollection(Type type)
    {
        if (type.IsArray)
            return true;

        var interfaces = type.IsInterface ? type.GetInterfaces().Append(type) : type.GetInterfaces();
        return interfaces.Any(candidate => candidate.IsGenericType
            && (candidate.GetGenericTypeDefinition() == typeof(ICollection<>)
                || candidate.GetGenericTypeDefinition() == typeof(ISet<>)));
    }
}
