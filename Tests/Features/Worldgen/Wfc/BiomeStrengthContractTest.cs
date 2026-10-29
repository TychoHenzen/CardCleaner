using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using CardCleaner.Scripts.Features.Worldgen.Biomes;
using CardCleaner.Scripts.Features.Worldgen.Wfc;
using CardCleaner.Tests.TestUtilities;

namespace CardCleaner.Tests.Features.Worldgen.Wfc;

/// <summary>
/// Keeps the WFC constraints off the concrete BiomeStrengthField. Files under Scripts/Features/Worldgen/Wfc may
/// name only IBiomeStrengthField, the port the field implements; the producer that builds the field stays outside
/// Wfc and passes it in as the port. The check blanks comments and literals, then matches the name as a whole word,
/// so a comment or a string does not count and each real hit is reported as file:line.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class BiomeStrengthContractTest
{
    private const string WfcRoot = "res://Scripts/Features/Worldgen/Wfc";

    // The port file. The scan must see it as code, so an empty or blanked scan cannot pass the no-name guard.
    private const string PortFile = "IBiomeStrengthField.cs";
    private const string PortDeclaration = "interface IBiomeStrengthField";

    private static readonly Regex ForbiddenName = new(@"\bBiomeStrengthField\b", RegexOptions.Compiled);

    [TestCase]
    [TestCategory("Unit")]
    public static void WfcNamesNoBiomeStrengthField()
    {
        var files = SourceScan.CsFiles(WfcRoot);
        AssertThat(files.Count > 0)
            .OverrideFailureMessage($"No .cs files found under {WfcRoot}")
            .IsTrue();

        var hits = files
            .SelectMany(file => Violations(SourceScan.RelativePath(file), SourceScan.Blanked(file), ForbiddenName))
            .ToList();
        AssertThat(hits.Count == 0)
            .OverrideFailureMessage("Wfc names BiomeStrengthField; name IBiomeStrengthField instead:\n"
                + string.Join("\n", hits))
            .IsTrue();
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void WfcKeepsTheBiomeStrengthPort()
    {
        var files = SourceScan.CsFiles(WfcRoot);
        var port = files.FirstOrDefault(file => Path.GetFileName(file) == PortFile);
        AssertThat(port != null && SourceScan.Blanked(port).Contains(PortDeclaration))
            .OverrideFailureMessage($"The Wfc scan no longer reads {PortFile} as code, so the no-name guard "
                + "could pass on nothing")
            .IsTrue();
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void TheFieldImplementsThePort()
    {
        AssertThat(typeof(IBiomeStrengthField).IsAssignableFrom(typeof(BiomeStrengthField)))
            .OverrideFailureMessage("BiomeStrengthField does not implement IBiomeStrengthField")
            .IsTrue();
    }

    private static IEnumerable<string> Violations(string relative, string blanked, Regex pattern)
    {
        foreach (Match match in pattern.Matches(blanked))
        {
            yield return SourceScan.FormatHit(relative, blanked, match);
        }
    }
}
