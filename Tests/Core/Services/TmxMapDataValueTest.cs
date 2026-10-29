using System.Linq;
using System.Reflection;
using CardCleaner.Scripts.Core.Services.TilesetLoading;
using GdUnit4;
using static GdUnit4.Assertions;

namespace CardCleaner.Tests.Core.Services;

/// <summary>
/// TmxMapData is an immutable value record: it is a record type, every property is get-only or init-only, and every
/// instance field is readonly. This pins what the dependency gate measures for it.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class TmxMapDataValueTest
{
    private const BindingFlags InstanceMembers = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

    [TestCase]
    [TestCategory("Unit")]
    public static void TmxMapDataIsAnImmutableRecord()
    {
        AssertThat(typeof(TmxMapData).GetMethod("<Clone>$", BindingFlags.Instance | BindingFlags.Public))
            .IsNotNull();

        foreach (var property in typeof(TmxMapData).GetProperties(InstanceMembers))
        {
            var setter = property.SetMethod;
            if (setter == null)
                continue;

            AssertBool(IsInitOnly(setter)).IsTrue();
        }

        foreach (var field in typeof(TmxMapData).GetFields(InstanceMembers))
            AssertBool(field.IsInitOnly).IsTrue();
    }

    private static bool IsInitOnly(MethodInfo setter)
        => setter.ReturnParameter.GetRequiredCustomModifiers()
            .Any(modifier => modifier.FullName == "System.Runtime.CompilerServices.IsExternalInit");
}
