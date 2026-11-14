using System.Text.Json.Nodes;
using CardCleaner.Scripts.Features.Automation;
using Godot;

namespace CardCleaner.Tests.Features.Automation;

/// <summary>The automation wire format, launch argument and aim maths, without a scene.</summary>
[TestSuite]
[RequireGodotRuntime]
public class AutomationProtocolTest
{
    [TestCase]
    [TestCategory("Unit")]
    public void ParseReadsIdCommandAndArgs()
    {
        var request = AutomationProtocol.Parse("{\"id\": 7, \"cmd\": \"walk_to\", \"args\": {\"stop\": 1.5}}");

        AssertThat(request.Id!.GetValue<int>()).IsEqual(7);
        AssertThat(request.Command).IsEqual("walk_to");
        AssertThat(request.Args.Number("stop", 0)).IsEqual(1.5);
    }

    [TestCase]
    [TestCategory("Unit")]
    public void ParseWithoutArgsGivesAnEmptyObject() =>
        AssertThat(AutomationProtocol.Parse("{\"cmd\": \"ping\"}").Args.Count).IsEqual(0);

    [TestCase("not json", "not JSON")]
    [TestCase("[1, 2]", "must be a JSON object")]
    [TestCase("{\"id\": 1}", "needs a \"cmd\" string")]
    [TestCase("{\"cmd\": \"ping\", \"args\": 3}", "\"args\" must be an object")]
    [TestCategory("Unit")]
    public void ParseRejectsMalformedLines(string line, string message)
    {
        AutomationException? thrown = null;
        try
        {
            AutomationProtocol.Parse(line);
        }
        catch (AutomationException e)
        {
            thrown = e;
        }

        AssertObject(thrown).IsNotNull();
        AssertThat(thrown!.Message).Contains(message);
    }

    [TestCase]
    [TestCategory("Unit")]
    public void RepliesEchoTheIdAndCarryResultOrError()
    {
        var ok = JsonNode.Parse(AutomationProtocol.Success(JsonValue.Create(3), new JsonObject { ["x"] = 1 }))!;
        var failed = JsonNode.Parse(AutomationProtocol.Failure(JsonValue.Create("a"), "no player"))!;

        AssertThat(ok["id"]!.GetValue<int>()).IsEqual(3);
        AssertBool(ok["ok"]!.GetValue<bool>()).IsTrue();
        AssertThat(ok["result"]!["x"]!.GetValue<int>()).IsEqual(1);
        AssertBool(failed["ok"]!.GetValue<bool>()).IsFalse();
        AssertThat(failed["error"]!.GetValue<string>()).IsEqual("no player");
    }

    [TestCase]
    [TestCategory("Unit")]
    public void PortArgumentIsReadOnlyWhenValid()
    {
        AssertThat(AutomationLaunch.ParsePort(["--other", "--automation-port=4711"])).IsEqual(4711);
        AssertThat(AutomationLaunch.ParsePort(["--automation-port=0"])).IsNull();
        AssertThat(AutomationLaunch.ParsePort(["--automation-port=70000"])).IsNull();
        AssertThat(AutomationLaunch.ParsePort(["--automation-port=-5"])).IsNull();
        AssertThat(AutomationLaunch.ParsePort([])).IsNull();
    }

    [TestCase]
    [TestCategory("Unit")]
    public void OutsideAutomationTheWantedMouseModeIsKept()
    {
        // The test run has no --automation-port, so capture passes through unchanged.
        AssertBool(AutomationLaunch.Requested).IsFalse();
        AssertThat(AutomationLaunch.SafeMouseMode(Input.MouseModeEnum.Captured))
            .IsEqual(Input.MouseModeEnum.Captured);
    }

    [TestCase]
    [TestCategory("Unit")]
    public void YawAndPitchFollowTheCameraConvention()
    {
        AssertVector(AutomationView.YawPitch(Vector3.Forward)).IsEqualApprox(new Vector2(0, 0), new Vector2(0.01f, 0.01f));
        AssertVector(AutomationView.YawPitch(Vector3.Left)).IsEqualApprox(new Vector2(90, 0), new Vector2(0.01f, 0.01f));
        AssertVector(AutomationView.YawPitch(new Vector3(0, 1, -1)))
            .IsEqualApprox(new Vector2(0, 45), new Vector2(0.01f, 0.01f));
        AssertFloat(AutomationView.AngleDelta(170, -170)).IsEqualApprox(20, 0.001f);
    }

    [TestCase]
    [TestCategory("Unit")]
    public void AimLearnsTheGainFromMeasuredTurns()
    {
        var aim = new ViewAim();
        aim.Learn(new Vector2(100, 50), new Vector2(-20, 5));

        AssertVector(aim.Gain).IsEqualApprox(new Vector2(-0.2f, 0.1f), new Vector2(0.0001f, 0.0001f));
        AssertVector(aim.PixelsFor(new Vector2(10, 1))).IsEqualApprox(new Vector2(-50, 10), new Vector2(0.01f, 0.01f));

        aim.Learn(new Vector2(0.2f, 30), new Vector2(5, 0));
        AssertVector(aim.Gain).IsEqualApprox(new Vector2(-0.2f, 0.1f), new Vector2(0.0001f, 0.0001f));
    }

    [TestCase]
    [TestCategory("Unit")]
    public void VariantJsonRoundTripsVectorsByTheTargetType()
    {
        var json = VariantJson.ToJson(new Vector3(1, 2.5f, -3));
        var back = VariantJson.FromJson(json, Variant.Type.Vector3);

        AssertThat(json!.ToJsonString()).IsEqual("[1,2.5,-3]");
        AssertThat(back.AsVector3()).IsEqual(new Vector3(1, 2.5f, -3));
        AssertThat(VariantJson.FromJson(JsonValue.Create(4.0), Variant.Type.Int).AsInt64()).IsEqual(4L);
    }
}
