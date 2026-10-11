using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using CardCleaner.Tests.TestUtilities;

namespace CardCleaner.Tests.Core.Interfaces;

/// <summary>
/// Scripts/Core/Interfaces is the bottom layer, so none of its files may name a feature namespace (Scripts/Features,
/// the CardCleaner.Features spelling, or a partly qualified Features. name) or the service locator. Comments and
/// literals do not count; method bodies do. The one feature type a contract may name is CardSignature, by its full
/// name (#167, decision Q1 A).
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class ContractsLayerBoundaryTest
{
    private const string InterfacesRoot = "res://Scripts/Core/Interfaces";
    private const string KernelType = "CardCleaner.Scripts.Features.Card.Models.CardSignature";

    // Blanked to spaces of the same length before the scan, so line numbers and the rest of the line are unchanged.
    private static readonly Regex KernelName = new(@"\b" + Regex.Escape(KernelType) + @"\b", RegexOptions.Compiled);

    // Feature namespaces in the full and partly qualified spellings, then the service locator and its namespace.
    private static readonly Regex ForbiddenName = new(
        @"\b(?:CardCleaner\.)?(?:Scripts\.)?Features\.[\w.]*|\bServiceLocator\b|\bCore\.DependencyInjection\b",
        RegexOptions.Compiled);

    [TestCase]
    [TestCategory("Unit")]
    public static void NoContractFileNamesAFeatureTypeOrTheServiceLocator()
    {
        var files = SourceScan.CsFiles(InterfacesRoot);
        var relativePaths = files.Select(SourceScan.RelativePath).ToList();
        var hits = files
            .SelectMany(path => ContractHits(SourceScan.RelativePath(path), File.ReadAllText(path)))
            .ToList();

        // A scan that sees too few files passes trivially, so it must cover the whole folder, including the two files
        // that name a feature type today.
        AssertInt(files.Count).IsGreater(24);
        AssertThat(relativePaths).Contains("Scripts/Core/Interfaces/ILog.cs");
        AssertThat(relativePaths).Contains("Scripts/Core/Interfaces/MapGeneration/MapGenerationConfig.cs");

        // Joined into one string so a failure names every offending line, not just how many there are.
        AssertThat(string.Join(System.Environment.NewLine, hits)).IsEmpty();
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void UsingAFeatureNamespaceIsReported()
    {
        const string source = "using CardCleaner.Scripts.Features.Card.Models;\npublic interface IThing {}\n";

        var hits = ContractHits("Scripts/Core/Interfaces/IThing.cs", source).ToList();

        AssertThat(hits).HasSize(1);
        AssertThat(hits[0]).IsEqual("Scripts/Core/Interfaces/IThing.cs:1 CardCleaner.Scripts.Features.Card.Models");
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void UsingTheOtherFeatureRootIsReported()
    {
        const string source = "using CardCleaner.Features.Deckbuilder.Tiles;\npublic interface IThing {}\n";

        AssertThat(ContractHits("Core/Interfaces/IThing.cs", source).ToList()).HasSize(1);
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void PartlyQualifiedFeatureNameIsReported()
    {
        const string source = "public interface IThing { Features.Card.Models.CardTemplate Template { get; } }\n";

        AssertThat(ContractHits("Core/Interfaces/IThing.cs", source).ToList()).HasSize(1);
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void ServiceLocatorInAMethodBodyIsReported()
    {
        const string source = "public static class Helper { static void M() { if (ServiceLocator.Has<ILog>()) {} } }\n";

        AssertThat(ContractHits("Core/Interfaces/Helper.cs", source).ToList()).HasSize(1);
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void AllowedKernelNameCommentsAndLiteralsAreNotReported()
    {
        const string source = "CardCleaner.Scripts.Features.Card.Models.CardSignature x; // Features.Card\n" +
            "var name = \"ServiceLocator\";\n";

        AssertThat(ContractHits("Core/Interfaces/Helper.cs", source).ToList()).IsEmpty();
    }

    // One "relative/path.cs:line name" per feature or service-locator name in a source. Comments and literals are
    // blanked first, so only code is scanned, and the allowed kernel name is blanked before matching.
    internal static IEnumerable<string> ContractHits(string relativePath, string source)
    {
        var blanked = TestSuiteNamingTest.BlankCommentsAndStrings(source);
        var code = KernelName.Replace(blanked, match => new string(' ', match.Length));
        return ForbiddenName.Matches(code).Select(match => SourceScan.FormatHit(relativePath, code, match)).ToList();
    }
}
