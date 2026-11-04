using System.Collections.Generic;
using CardCleaner.Scripts.Features.Shop.Models;
using Godot;

namespace CardCleaner.Tests.Features.Shop.Models;

[TestSuite]
[RequireGodotRuntime]
public class FurnitureSaveCodecTest
{
    [TestCase]
    [TestCategory("Unit")]
    public static void RoundTripKeepsEveryField()
    {
        var records = new List<FurnitureRecord>
        {
            new("shelf_unit", 8f, 6f, 1, false),
            new("shelf_unit", 3.5f, 2f, 0, true),
        };

        var decoded = FurnitureSaveCodec.Decode(FurnitureSaveCodec.Encode(records));

        AssertInt(decoded.Count).IsEqual(2);
        AssertString(decoded[0].Id).IsEqual("shelf_unit");
        AssertFloat(decoded[0].X).IsEqual(8f);
        AssertFloat(decoded[0].Z).IsEqual(6f);
        AssertInt(decoded[0].QuarterTurns).IsEqual(1);
        AssertBool(decoded[0].IsBox).IsFalse();
        AssertString(decoded[1].Id).IsEqual("shelf_unit");
        AssertFloat(decoded[1].X).IsEqual(3.5f);
        AssertFloat(decoded[1].Z).IsEqual(2f);
        AssertInt(decoded[1].QuarterTurns).IsEqual(0);
        AssertBool(decoded[1].IsBox).IsTrue();
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void NullAndGarbageDecodeToEmpty()
    {
        AssertInt(FurnitureSaveCodec.Decode(null).Count).IsEqual(0);
        AssertInt(FurnitureSaveCodec.Decode("").Count).IsEqual(0);
        AssertInt(FurnitureSaveCodec.Decode("{not json").Count).IsEqual(0);
    }
}
