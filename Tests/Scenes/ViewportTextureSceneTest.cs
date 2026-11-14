using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using CardCleaner.Tests.TestUtilities;
using Godot;

namespace CardCleaner.Tests.Scenes;

/// <summary>
///     A ViewportTexture finds its viewport by a path relative to the scene that owns it, and Godot resolves that path
///     only for a resource that is local to the scene. Without <c>resource_local_to_scene</c> the load logs "Path to
///     node is invalid" and the texture stays empty, which left the backoffice PC monitor magenta.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public partial class ViewportTextureSceneTest
{
    private static readonly string[] ScannedRoots = ["res://Scenes", "res://Data"];
    private const string PcScreenPath = "World/Markers/PcLocation/PcTerminal/ScreenQuad";

    [GeneratedRegex(@"^\[sub_resource type=""(?<type>\w+)"" id=""(?<id>[^""]+)""[^\]]*\]\s*$")]
    private static partial Regex SubResourceHeader();

    [GeneratedRegex(@"SubResource\(""(?<id>[^""]+)""\)")]
    private static partial Regex SubResourceReference();

    private sealed record Stanza(string Id, string Type, int Line, bool Local, List<string> References);

    /// <summary>
    ///     One "file:line" entry per sub-resource of <paramref name="text" /> that is a ViewportTexture, or holds one
    ///     directly or through other sub-resources, and is not local to the scene. Godot sets a local resource up for
    ///     its scene only when the resource holding it is local too, so the whole chain from the node down must be.
    /// </summary>
    public static IEnumerable<string> NonLocalViewportTextures(string file, string text)
    {
        var stanzas = Stanzas(text.Split('\n'));
        var holders = stanzas.Values.Where(s => s.Type == "ViewportTexture").Select(s => s.Id).ToHashSet();
        bool grew;
        do
        {
            var next = stanzas.Values.Where(s => s.References.Any(holders.Contains)).Select(s => s.Id).ToList();
            grew = next.Any(holders.Add);
        } while (grew);

        return stanzas.Values.Where(s => holders.Contains(s.Id) && !s.Local).OrderBy(s => s.Line)
            .Select(s => FormattableString.Invariant($"{file}:{s.Line}"));
    }

    private static Dictionary<string, Stanza> Stanzas(string[] lines)
    {
        var stanzas = new Dictionary<string, Stanza>(StringComparer.Ordinal);
        for (var i = 0; i < lines.Length; i++)
        {
            var header = SubResourceHeader().Match(lines[i]);
            if (!header.Success)
                continue;
            var body = lines.Skip(i + 1).TakeWhile(line => !line.StartsWith('[')).ToList();
            var references = body.SelectMany(line => SubResourceReference().Matches(line))
                .Select(m => m.Groups["id"].Value).ToList();
            var id = header.Groups["id"].Value;
            stanzas[id] = new Stanza(id, header.Groups["type"].Value, i + 1,
                body.Any(line => line.Trim() == "resource_local_to_scene = true"), references);
        }

        return stanzas;
    }

    [TestCase]
    [TestCategory("Unit")]
    public void TheCheckFlagsEveryNonLocalResourceOnTheWayToAViewportTexture()
    {
        const string text = "[sub_resource type=\"ViewportTexture\" id=\"T\"]\nresource_local_to_scene = true\n"
                            + "viewport_path = NodePath(\"V\")\n\n"
                            + "[sub_resource type=\"StandardMaterial3D\" id=\"M\"]\nalbedo_texture = SubResource(\"T\")\n\n"
                            + "[sub_resource type=\"QuadMesh\" id=\"Q\"]\nresource_local_to_scene = true\n"
                            + "material = SubResource(\"M\")\n\n"
                            + "[sub_resource type=\"StandardMaterial3D\" id=\"Plain\"]\nshading_mode = 0\n\n"
                            + "[sub_resource type=\"ViewportTexture\" id=\"Bare\"]\nviewport_path = NodePath(\"V\")\n";

        AssertThat(NonLocalViewportTextures("x.tscn", text).ToList()).ContainsExactly("x.tscn:5", "x.tscn:15");
    }

    [TestCase]
    [TestCategory("Unit")]
    public void EveryViewportTextureInASceneIsLocalToItsScene()
    {
        var files = ScannedRoots
            .SelectMany(root => Directory.EnumerateFiles(ProjectSettings.GlobalizePath(root), "*.*",
                SearchOption.AllDirectories))
            .Where(file => file.EndsWith(".tscn", StringComparison.Ordinal)
                           || file.EndsWith(".tres", StringComparison.Ordinal))
            .ToList();

        var offenders = files
            .SelectMany(file => NonLocalViewportTextures(SourceScan.RelativePath(file), File.ReadAllText(file)))
            .ToList();

        AssertThat(files.Count).IsGreater(0);
        AssertThat(offenders).IsEmpty();
    }

    [TestCase]
    [TestCategory("Unit")]
    public void ThePcMonitorShowsTheDesktopViewport()
    {
        var shop = ShopOrderingRig.LoadShopWithServices();
        AddNode(shop);
        try
        {
            var viewport = shop.GetNode<SubViewport>("PcDesktopViewport");
            var mesh = shop.GetNode<MeshInstance3D>(PcScreenPath).Mesh;
            var texture = (mesh.SurfaceGetMaterial(0) as StandardMaterial3D)?.AlbedoTexture as ViewportTexture;

            AssertObject(texture).IsNotNull();
            AssertThat(texture!.GetSize()).IsEqual((Vector2)viewport.Size);
        }
        finally
        {
            TestServices.Reset();
        }
    }
}
