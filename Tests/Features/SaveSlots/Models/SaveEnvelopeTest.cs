using System;
using CardCleaner.Scripts.Features.SaveSlots.Models;
using CardCleaner.Tests.TestUtilities;

namespace CardCleaner.Tests.Features.SaveSlots.Models;

[TestSuite]
[RequireGodotRuntime]
public class SaveEnvelopeTest
{
    private static readonly DateTime SavedAt = new(2026, 11, 2, 7, 0, 0, DateTimeKind.Utc);

    [TestCase]
    [TestCategory("Unit")]
    public static void WriteThenReadRoundTripsPayloadAndVersion()
    {
        var json = SaveEnvelope.Write("{\"tree\":1}", SavedAt);

        var result = SaveEnvelope.Read(json);

        AssertThat(result.IsOk).IsTrue();
        AssertThat(result.Envelope!.Payload).IsEqual("{\"tree\":1}");
        AssertThat(result.Envelope.Version).IsEqual(SaveEnvelope.CurrentVersion);
    }

    [TestCase("")]
    [TestCase("  ")]
    [TestCategory("Unit")]
    public static void BlankTextIsRefusedAsEmpty(string json)
    {
        AssertThat(SaveEnvelope.Read(json).Refusal).IsEqual("empty");
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void BrokenJsonIsRefusedAsUnreadable()
    {
        AssertThat(SaveEnvelope.Read("{").Refusal).IsEqual("unreadable");
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void VersionTwoIsRefusedAsNewer()
    {
        var json = "{\"Version\":2,\"SavedAtUtc\":\"2026-11-02T07:00:00.0000000Z\",\"Payload\":\"{}\"}";

        AssertThat(SaveEnvelope.Read(json).Refusal).Contains("newer");
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void VersionZeroIsRefusedAsUnsupported()
    {
        var json = "{\"Version\":0,\"SavedAtUtc\":\"2026-11-02T07:00:00.0000000Z\",\"Payload\":\"{}\"}";

        AssertThat(SaveEnvelope.Read(json).Refusal).Contains("unsupported");
    }
}
