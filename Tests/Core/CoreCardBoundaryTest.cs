using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using GdUnit4;
using Godot;

namespace CardCleaner.Tests.Core;

/// <summary>
/// Scripts/Core may not depend on the Card feature (#172). Its files outside Scripts/Core/Interfaces may not name the
/// Features.Card namespace or any type declared under Scripts/Features/Card. The interfaces stay outside this guard:
/// #167 owns those contracts.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class CoreCardBoundaryTest
{
    private const string ScriptsRoot = "res://Scripts";
    private const string CardFolder = "Features/Card/";
    private const string CoreFolder = "Core/";
    private const string InterfacesFolder = "Core/Interfaces/";

    private static readonly Regex CardNamespace = new(@"\bFeatures\.Card\b", RegexOptions.Compiled);

    // Keywords are lowercase, so requiring an uppercase first letter keeps "where T : struct" out of the names.
    private static readonly Regex TypeDeclaration = new(
        @"\b(?:class|struct|enum|interface|record(?:\s+struct)?)\s+([A-Z]\w*)",
        RegexOptions.Compiled);

    [TestCase]
    [TestCategory("Unit")]
    public static void CoreFilesOutsideInterfacesNameNoCardType()
    {
        var root = ProjectSettings.GlobalizePath(ScriptsRoot);
        var sources = Directory.EnumerateFiles(root, "*.cs", SearchOption.AllDirectories)
            .Select(path => (Folder: RelativePath(root, path), Source: File.ReadAllText(path)))
            .ToList();
        var cardTypes = sources
            .Where(file => file.Folder.StartsWith(CardFolder, StringComparison.Ordinal))
            .SelectMany(file => DeclaredTypeNames(file.Source))
            .ToList();
        var coreFiles = sources
            .Where(file => file.Folder.StartsWith(CoreFolder, StringComparison.Ordinal))
            .Where(file => !file.Folder.StartsWith(InterfacesFolder, StringComparison.Ordinal))
            .ToList();
        var violations = coreFiles.SelectMany(file => CardViolations(file.Folder, file.Source, cardTypes));

        // A scan that found no Core file or no Card type passes trivially, so the guard must have seen both.
        AssertThat(coreFiles).IsNotEmpty();
        AssertThat(cardTypes).Contains("CardController");

        // Joined into one string so a failure names every offending file, not just how many there are.
        AssertThat(string.Join(System.Environment.NewLine, violations)).IsEmpty();
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void UsingTheCardNamespaceInCoreIsReported()
    {
        const string source = "using CardCleaner.Scripts.Features.Card.Models;\npublic class Helper {}\n";

        AssertThat(CardViolations("Core/Helper.cs", source, new List<string>()).ToList()).HasSize(1);
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void NamingACardTypeInCoreIsReported()
    {
        const string source = "public class Helper\n{\n    private CardDesigner? _designer;\n}\n";

        var violations = CardViolations("Core/Helper.cs", source, new List<string> { "CardDesigner" }).ToList();

        AssertThat(violations).HasSize(1);
        AssertThat(violations[0]).IsEqual("Core/Helper.cs names the Card type CardDesigner");
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void CommentsAndStringsMentioningCardTypesAreIgnored()
    {
        const string source = "// CardDesigner and Features.Card are described here\nvar text = \"CardDesigner\";\n";

        AssertThat(CardViolations("Core/Helper.cs", source, new List<string> { "CardDesigner" }).ToList()).IsEmpty();
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void EveryKindOfCardTypeDeclarationIsCollected()
    {
        const string source = "public sealed record struct Pose(int X);\n" +
            "public readonly struct Mark {}\n" +
            "public partial class Card : Node {}\n" +
            "public enum Tone { A }\n" +
            "public interface ICardLike {}\n";

        AssertThat(string.Join(",", DeclaredTypeNames(source))).IsEqual("Pose,Mark,Card,Tone,ICardLike");
    }

    internal static IEnumerable<string> DeclaredTypeNames(string source)
    {
        var code = TestSuiteNamingTest.BlankCommentsAndStrings(source);
        return TypeDeclaration.Matches(code).Select(match => match.Groups[1].Value).ToList();
    }

    internal static IEnumerable<string> CardViolations(string folder, string source, IEnumerable<string> cardTypes)
    {
        var code = TestSuiteNamingTest.BlankCommentsAndStrings(source);
        if (CardNamespace.IsMatch(code))
            yield return $"{folder} names the Features.Card namespace";

        var names = cardTypes.Distinct().ToList();
        if (names.Count == 0)
            yield break;

        var typeUse = new Regex(@"\b(?:" + string.Join("|", names.Select(Regex.Escape)) + @")\b");
        foreach (var name in typeUse.Matches(code).Select(match => match.Value).Distinct())
            yield return $"{folder} names the Card type {name}";
    }

    private static string RelativePath(string root, string path) =>
        Path.GetRelativePath(root, path).Replace('\\', '/');
}
