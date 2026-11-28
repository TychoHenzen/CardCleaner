using System;
using Godot;

namespace CardCleaner.Tests.Features.Packs.Components;

/// <summary>
///     Pins the committed booster, pack and box meshes under res://Data/Packs/Meshes to
///     <see cref="ContainerMeshRecipe" />. See "Container Meshes and Opening" in docs/CONTAINERS_AND_HAND.md
///     for the regenerate command.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class ContainerMeshBakeTest
{
    private const string MeshFolder = "res://Data/Packs/Meshes";
    private const string RegenerateSwitch = "CARDCLEANER_REGENERATE_CONTAINER_MESHES";

    // The recipe of each committed mesh, with its vertex count as observed by RegenerateCommittedMeshes and then
    // pinned, so an empty or reshaped mesh fails here before the byte comparison.
    private static readonly (string Name, Func<ArrayMesh> Build, int Vertices)[] Meshes =
    [
        ("BoosterBody", ContainerMeshRecipe.BoosterBody, 2063),
        ("BoosterTearStrip", ContainerMeshRecipe.BoosterTearStrip, 250),
        ("PackBody", ContainerMeshRecipe.PackBody, 56),
        ("PackLid", ContainerMeshRecipe.PackLid, 24),
        ("CartonBody", ContainerMeshRecipe.CartonBody, 56),
        ("CartonMajorFlap", ContainerMeshRecipe.CartonMajorFlap, 24),
        ("CartonMinorFlap", ContainerMeshRecipe.CartonMinorFlap, 24)
    ];

    private static string PathOf(string name) => $"{MeshFolder}/{name}.tres";

    [TestCase]
    public static void CommittedMeshesMatchTheRecipe()
    {
        foreach (var (name, build, vertices) in Meshes)
        {
            var committed = ResourceLoader.Load<ArrayMesh>(PathOf(name));
            Assertions.AssertThat(committed).OverrideFailureMessage($"{PathOf(name)} is missing").IsNotNull();
            var fresh = build();
            Assertions.AssertInt(fresh.GetSurfaceCount()).IsEqual(1);
            Assertions.AssertInt(VertexCount(fresh)).OverrideFailureMessage($"{name} vertex count")
                .IsEqual(vertices);
            Assertions.AssertThat(fresh.GetAabb()).OverrideFailureMessage($"{name} bounds")
                .IsEqual(committed!.GetAabb());
            Assertions.AssertString(GD.VarToStr(fresh.Get("_surfaces")))
                .OverrideFailureMessage($"{PathOf(name)} differs from ContainerMeshRecipe.{name}")
                .IsEqual(GD.VarToStr(committed.Get("_surfaces")));
        }
    }

    [TestCase]
    public static void ClosedContainersHaveTheirStatedSize()
    {
        AssertSize(ContainerMeshRecipe.BoosterBody().GetAabb().Merge(ContainerMeshRecipe.BoosterTearStrip().GetAabb()),
            new Vector3(ContainerMeshRecipe.BoosterWidth, ContainerMeshRecipe.BoosterThickness,
                ContainerMeshRecipe.BoosterLength));

        var pack = ContainerMeshRecipe.PackBody().GetAabb();
        AssertSize(pack, new Vector3(ContainerMeshRecipe.PackWidth,
            ContainerMeshRecipe.PackHeight - ContainerMeshRecipe.PackWall, ContainerMeshRecipe.PackDepth));

        var carton = ContainerMeshRecipe.CartonBody().GetAabb();
        AssertSize(carton, new Vector3(ContainerMeshRecipe.CartonWidth,
            ContainerMeshRecipe.CartonHeight - 2f * ContainerMeshRecipe.CartonWall, ContainerMeshRecipe.CartonDepth));
    }

    // Rewrites every mesh under Data/Packs/Meshes from the recipe and prints its vertex count, to pin above. Without
    // the switch it does nothing, so normal runs pass it.
    [TestCase]
    public static void RegenerateCommittedMeshes()
    {
        if (OS.GetEnvironment(RegenerateSwitch) != "1") return;

        DirAccess.MakeDirRecursiveAbsolute(ProjectSettings.GlobalizePath(MeshFolder));
        foreach (var (name, build, _) in Meshes)
        {
            var mesh = build();
            GD.Print($"container mesh {name}: vertices={VertexCount(mesh)} aabb={mesh.GetAabb()}");
            Assertions.AssertInt((int)ResourceSaver.Save(mesh, PathOf(name))).IsEqual((int)Error.Ok);
        }
    }

    private static int VertexCount(ArrayMesh mesh) =>
        mesh.SurfaceGetArrays(0)[(int)Mesh.ArrayType.Vertex].AsVector3Array().Length;

    private static void AssertSize(Aabb bounds, Vector3 size)
    {
        Assertions.AssertThat(bounds.Size.IsEqualApprox(size)).OverrideFailureMessage($"{bounds.Size} is not {size}")
            .IsTrue();
        Assertions.AssertFloat(bounds.Position.Y).IsEqualApprox(0f, 1e-5f);
        Assertions.AssertFloat(bounds.GetCenter().X).IsEqualApprox(0f, 1e-5f);
        Assertions.AssertFloat(bounds.GetCenter().Z).IsEqualApprox(0f, 1e-5f);
    }
}
