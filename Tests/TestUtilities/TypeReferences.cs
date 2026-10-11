using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;

namespace CardCleaner.Tests.TestUtilities;

/// <summary>
/// Lists the types a compiled type names: its base type, interfaces and attributes, its declared members' signatures,
/// and every local, type, field and method token in its method bodies. Arrays, by-refs, pointers and constructed
/// generics are reduced to their element, definition and argument types; generic parameters name nothing.
/// </summary>
internal static class TypeReferences
{
    private const BindingFlags Declared = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance |
        BindingFlags.Static | BindingFlags.DeclaredOnly;

    private static readonly Dictionary<short, OpCode> OpCodesByValue = typeof(OpCodes)
        .GetFields(BindingFlags.Public | BindingFlags.Static)
        .Select(field => (OpCode)field.GetValue(null)!)
        .ToDictionary(code => code.Value);

    internal static IEnumerable<Type> Of(Type type) => Named(type).SelectMany(Unwrap).Distinct();

    private static List<Type> Named(Type type)
    {
        var named = new List<Type>();
        if (type.BaseType != null)
            named.Add(type.BaseType);
        named.AddRange(type.GetInterfaces());
        named.AddRange(type.CustomAttributes.Select(attribute => attribute.AttributeType));
        named.AddRange(type.GetFields(Declared).Select(field => field.FieldType));
        named.AddRange(type.GetProperties(Declared).Select(property => property.PropertyType));
        named.AddRange(type.GetEvents(Declared).Select(evt => evt.EventHandlerType!));
        foreach (var method in type.GetMethods(Declared).Cast<MethodBase>().Concat(type.GetConstructors(Declared)))
        {
            if (method is MethodInfo info)
                named.Add(info.ReturnType);
            named.AddRange(method.GetParameters().Select(parameter => parameter.ParameterType));
            named.AddRange(method.CustomAttributes.Select(attribute => attribute.AttributeType));
            named.AddRange(BodyTypes(method));
        }

        return named;
    }

    private static IEnumerable<Type> Unwrap(Type type)
    {
        if (type.IsGenericParameter)
            return Array.Empty<Type>();
        if (type.HasElementType)
            return Unwrap(type.GetElementType()!);
        if (type.IsGenericType && !type.IsGenericTypeDefinition)
            return type.GetGenericArguments().SelectMany(Unwrap).Prepend(type.GetGenericTypeDefinition());
        return new[] { type };
    }

    private static List<Type> BodyTypes(MethodBase method)
    {
        var body = method.GetMethodBody();
        var il = body?.GetILAsByteArray();
        if (body == null || il == null)
            return new List<Type>();

        var named = body.LocalVariables.Select(local => local.LocalType).ToList();
        var typeArguments = method.DeclaringType is { IsGenericType: true } owner
            ? owner.GetGenericArguments()
            : Type.EmptyTypes;
        var methodArguments = method is MethodInfo { IsGenericMethod: true }
            ? method.GetGenericArguments()
            : Type.EmptyTypes;
        var position = 0;
        while (position < il.Length)
        {
            var code = il[position] == 0xFE
                ? OpCodesByValue[unchecked((short)(0xFE00 | il[position + 1]))]
                : OpCodesByValue[il[position]];
            position += code.Size;
            if (code.OperandType is OperandType.InlineTok or OperandType.InlineType or OperandType.InlineMethod
                or OperandType.InlineField)
            {
                var token = BitConverter.ToInt32(il, position);
                named.AddRange(TokenTypes(method.Module.ResolveMember(token, typeArguments, methodArguments)));
            }

            position += OperandSize(code.OperandType, il, position);
        }

        return named;
    }

    private static int OperandSize(OperandType operand, byte[] il, int position) => operand switch
    {
        OperandType.InlineNone => 0,
        OperandType.ShortInlineBrTarget or OperandType.ShortInlineI or OperandType.ShortInlineVar => 1,
        OperandType.InlineVar => 2,
        OperandType.InlineI8 or OperandType.InlineR => 8,
        OperandType.InlineSwitch => 4 + 4 * BitConverter.ToInt32(il, position),
        _ => 4, // tokens, InlineBrTarget, InlineI, InlineString, InlineSig, ShortInlineR
    };

    private static IEnumerable<Type> TokenTypes(MemberInfo? member)
    {
        switch (member)
        {
            case Type type:
                return new[] { type };
            case FieldInfo field:
                return new[] { field.DeclaringType!, field.FieldType };
            case MethodBase target:
                var types = new List<Type> { target.DeclaringType! };
                if (target is MethodInfo info)
                {
                    types.Add(info.ReturnType);
                    if (info.IsGenericMethod)
                        types.AddRange(info.GetGenericArguments());
                }

                types.AddRange(target.GetParameters().Select(parameter => parameter.ParameterType));
                return types;
            default:
                return Array.Empty<Type>();
        }
    }
}
