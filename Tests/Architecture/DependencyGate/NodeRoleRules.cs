using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;

namespace CardCleaner.Tests.Architecture.DependencyGate;

/// <summary>Role rules of #240 requirement 3, ported from the prototype extractor.</summary>
internal static class NodeRoleRules
{
    private static readonly HashSet<string> ValueMethodNames =
        new(StringComparer.Ordinal) { "ToString", "Equals", "GetHashCode", "Deconstruct", "PrintMembers" };

    internal static bool DerivesFromGodotObject(INamedTypeSymbol type)
    {
        for (var baseType = type.BaseType; baseType != null; baseType = baseType.BaseType)
        {
            if (baseType.ToDisplayString() == "Godot.GodotObject") return true;
        }

        return false;
    }

    /// <summary>
    /// The first role that matches, in precedence order: interface or delegate, abstract, data, static, concrete.
    /// </summary>
    internal static NodeRole Role(INamedTypeSymbol type, bool isGodot)
    {
        if (type.TypeKind is TypeKind.Interface or TypeKind.Delegate) return NodeRole.Interface;
        if (type.IsAbstract && type.TypeKind == TypeKind.Class) return NodeRole.Abstract;
        if (type.TypeKind == TypeKind.Enum || IsMethodFree(type, isGodot) || IsImmutableValue(type, isGodot))
            return NodeRole.Data;
        if (type.IsStatic) return IsPureStatic(type) ? NodeRole.Function : NodeRole.Static;
        return NodeRole.Concrete;
    }

    // A struct, record or class with no ordinary methods of its own is plain data.
    private static bool IsMethodFree(INamedTypeSymbol type, bool isGodot) =>
        type.TypeKind is TypeKind.Class or TypeKind.Struct && !type.IsAbstract && !type.IsStatic && !isGodot
        && !type.GetMembers().OfType<IMethodSymbol>().Any(m => !m.IsImplicitlyDeclared
            && m.MethodKind == MethodKind.Ordinary && !ValueMethodNames.Contains(m.Name));

    // An immutable value object (struct or record, no settable state) is plain data even with pure methods on it.
    private static bool IsImmutableValue(INamedTypeSymbol type, bool isGodot)
    {
        if (isGodot || !(type.TypeKind == TypeKind.Struct || type.IsRecord)) return false;
        var members = type.GetMembers();
        var fieldsReadOnly = members.OfType<IFieldSymbol>()
            .Where(f => !f.IsStatic && !f.IsConst && !f.IsImplicitlyDeclared).All(f => f.IsReadOnly);
        var propertiesFixed = members.OfType<IPropertySymbol>().Where(p => !p.IsStatic && !p.IsImplicitlyDeclared)
            .All(p => p.SetMethod == null || p.SetMethod.IsInitOnly);
        var backingFixed = members.OfType<IFieldSymbol>().Where(f => f.IsImplicitlyDeclared && !f.IsStatic)
            .All(f => f.IsReadOnly
                      || f.AssociatedSymbol is IPropertySymbol { SetMethod: null or { IsInitOnly: true } });
        return fieldsReadOnly && propertiesFixed && backingFixed;
    }

    // A static class with no writable static state is a function library: users depend on its signatures only.
    private static bool IsPureStatic(INamedTypeSymbol type)
    {
        var members = type.GetMembers();
        return members.OfType<IFieldSymbol>().Where(f => f.IsStatic && !f.IsConst).All(f => f.IsReadOnly)
               && members.OfType<IPropertySymbol>().Where(p => p.IsStatic).All(p => p.SetMethod == null);
    }
}
