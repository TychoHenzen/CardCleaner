using System;
using System.Collections.Generic;
using Godot;

namespace CardCleaner.Tests.Features.Packs.Components;

/// <summary>
///     The source of the booster, pack and box meshes under res://Data/Packs/Meshes. Test code only: nothing at
///     runtime builds these meshes; ContainerMeshBakeTest regenerates the committed files from this recipe and fails
///     when they drift from it.
///     <para>
///         Every mesh puts its origin where its scene expects it: a container body at its bottom centre, a lid or
///         flap on its hinge line. The vertex colour's red channel tells the shader which part of the print a face
///         shows (see <see cref="PrintFace" />); a booster's red channel is its crimp weight instead.
///     </para>
/// </summary>
public static class ContainerMeshRecipe
{
    // A booster is a foil pouch around 8 cards of 0.32 x 0.45 m, sealed by a crimp at each short end. Its torn
    // end, the one the cards come out of, is at -Z, where the top of its print is (as on a card).
    public const float BoosterWidth = 0.35f;
    public const float BoosterLength = 0.53f;
    public const float BoosterThickness = 0.045f;
    public const float BoosterCrimp = 0.04f;

    // A pack is a printed box of 8 boosters with a lid hinged at its back (-Z) edge. It is shallower than 8 boosters
    // would need, so 8 packs fit a carton standing on end (CartonSpace).
    public const float PackWidth = 0.38f;
    public const float PackHeight = 0.20f;
    public const float PackDepth = 0.55f;
    public const float PackWall = 0.006f;

    // A box is a corrugated shipping carton of 8 packs, closed by two major flaps that meet over the middle and two
    // minor flaps under them. It is tall enough inside (0.645 m) for four stacks of 128 cards (CartonSpace).
    public const float CartonWidth = 0.80f;
    public const float CartonHeight = 0.669f;
    public const float CartonDepth = 0.86f;
    public const float CartonWall = 0.008f;

    private const float BoosterPinch = 0.05f;
    private const float CrimpThickness = 0.003f;
    private const float PillowFlatness = 6f;
    private const int BoosterAcross = 24;
    private const int PinchSegments = 8;
    private const int BodySegments = 20;
    private const int CrimpSegments = 3;

    /// <summary>The part of a printed box a face shows; the shader reads it from the vertex colour.</summary>
    public enum PrintFace
    {
        Top = 0,
        Front = 1,
        Back = 2,
        Side = 3,
        Bottom = 4,
        Inside = 5,
        Edge = 6
    }

    /// <summary>The pouch from the tear line to its bottom crimp. Open at the tear line, so cards can come out.</summary>
    public static ArrayMesh BoosterBody()
    {
        var start = -BoosterLength / 2f + BoosterCrimp;
        var zs = new List<float>();
        AddRange(zs, start, start + BoosterPinch, PinchSegments, true);
        AddRange(zs, start + BoosterPinch, BoosterLength / 2f - BoosterCrimp - BoosterPinch, BodySegments, false);
        AddRange(zs, BoosterLength / 2f - BoosterCrimp - BoosterPinch, BoosterLength / 2f - BoosterCrimp,
            PinchSegments, false);
        AddRange(zs, BoosterLength / 2f - BoosterCrimp, BoosterLength / 2f, CrimpSegments, false);
        return Pouch(zs, false, true);
    }

    /// <summary>The top crimp, which tears off when the booster is opened. Origin on the booster's origin.</summary>
    public static ArrayMesh BoosterTearStrip()
    {
        var zs = new List<float>();
        AddRange(zs, -BoosterLength / 2f, -BoosterLength / 2f + BoosterCrimp, CrimpSegments, true);
        return Pouch(zs, true, true);
    }

    /// <summary>The pack's box without its lid: four printed walls and a bottom, open at the top.</summary>
    public static ArrayMesh PackBody() =>
        OpenBox(PackWidth, PackHeight - PackWall, PackDepth, PackWall, PrintFace.Side);

    /// <summary>The pack's lid, hinged along X at its origin and reaching to +Z.</summary>
    public static ArrayMesh PackLid()
    {
        var sheet = new MeshSheet(false);
        Slab(sheet, new Vector3(-PackWidth / 2f, 0f, 0f), new Vector3(PackWidth / 2f, PackWall, PackDepth),
            PrintFace.Top, PrintFace.Inside);
        return sheet.Commit();
    }

    /// <summary>The carton without its flaps: four walls and a bottom, open at the top.</summary>
    public static ArrayMesh CartonBody() =>
        OpenBox(CartonWidth, CartonHeight - 2f * CartonWall, CartonDepth, CartonWall, PrintFace.Side);

    /// <summary>A major flap, hinged along Z at its origin and reaching to +X over half the carton's width.</summary>
    public static ArrayMesh CartonMajorFlap()
    {
        var sheet = new MeshSheet(false);
        Slab(sheet, new Vector3(0f, 0f, -CartonDepth / 2f), new Vector3(CartonWidth / 2f, CartonWall, CartonDepth / 2f),
            PrintFace.Top, PrintFace.Inside);
        return sheet.Commit();
    }

    /// <summary>A minor flap, hinged along X at its origin and reaching to +Z, between the walls.</summary>
    public static ArrayMesh CartonMinorFlap()
    {
        var sheet = new MeshSheet(false);
        var half = CartonWidth / 2f - CartonWall;
        Slab(sheet, new Vector3(-half, 0f, 0f), new Vector3(half, CartonWall, CartonWidth / 2f),
            PrintFace.Top, PrintFace.Inside);
        return sheet.Commit();
    }

    // Appends the z values of segments evenly spaced from start to end; the first value only when asked, so ranges
    // chain without repeating their shared end.
    private static void AddRange(List<float> zs, float start, float end, int segments, bool includeStart)
    {
        for (var i = includeStart ? 0 : 1; i <= segments; i++)
            zs.Add(start + (end - start) * i / segments);
    }

    // The thickness of the pouch at (x, z): a pillow that is flat over most of its width and thins to the crimp
    // thickness at its side folds, pinched over BoosterPinch into a flat crimp of BoosterCrimp at each end.
    private static float PouchThickness(float x, float z)
    {
        var across = Mathf.Pow(Mathf.Abs(2f * x / BoosterWidth), PillowFlatness);
        var full = Mathf.Max(CrimpThickness, BoosterThickness * Mathf.Sqrt(Mathf.Max(0f, 1f - across)));
        var fromEnd = BoosterLength / 2f - Mathf.Abs(z);
        if (fromEnd <= BoosterCrimp)
            return CrimpThickness;
        if (fromEnd >= BoosterCrimp + BoosterPinch)
            return full;

        var t = (fromEnd - BoosterCrimp) / BoosterPinch;
        return Mathf.Lerp(CrimpThickness, full, t * t * (3f - 2f * t));
    }

    private static float CrimpWeight(float z) =>
        BoosterLength / 2f - Mathf.Abs(z) <= BoosterCrimp + 1e-4f ? 1f : 0f;

    // Front print on the left half of the texture, back print mirrored on the right half (so it reads from below),
    // and v from the torn end (0) to the bottom crimp (1), as the card does.
    private static Vector2 PouchUv(float x, float z, bool top) =>
        new(top ? (x / BoosterWidth + 0.5f) * 0.5f : 0.5f + (0.5f - x / BoosterWidth) * 0.5f, z / BoosterLength + 0.5f);

    // A pouch section over the given z samples: a top and a bottom grid around the half-thickness line, joined by
    // side strips, with an end cap at the first and last sample when asked.
    private static ArrayMesh Pouch(IReadOnlyList<float> zs, bool capStart, bool capEnd)
    {
        var sheet = new MeshSheet(true);
        var xs = new float[BoosterAcross + 1];
        for (var i = 0; i <= BoosterAcross; i++)
            xs[i] = -BoosterWidth / 2f * Mathf.Cos(Mathf.Pi * i / BoosterAcross);

        var centre = BoosterThickness / 2f;
        Vector3 Point(int i, int j, bool top)
        {
            var half = PouchThickness(xs[i], zs[j]) / 2f;
            return new Vector3(xs[i], top ? centre + half : centre - half, zs[j]);
        }

        int Vertex(int i, int j, bool top) =>
            sheet.Vertex(Point(i, j, top), PouchUv(xs[i], zs[j], top), CrimpWeight(zs[j]));

        PouchGrid(sheet, xs.Length, zs.Count, (i, j) => Vertex(i, j, true), true);
        PouchGrid(sheet, xs.Length, zs.Count, (i, j) => Vertex(i, j, false), false);

        var last = xs.Length - 1;
        for (var j = 0; j + 1 < zs.Count; j++)
        {
            // Right fold, seen from +X (screen right is -Z); left fold, seen from -X (screen right is +Z).
            sheet.Quad(Corner(sheet, Point(last, j + 1, true), xs[last], zs[j + 1]),
                Corner(sheet, Point(last, j, true), xs[last], zs[j]),
                Corner(sheet, Point(last, j, false), xs[last], zs[j]),
                Corner(sheet, Point(last, j + 1, false), xs[last], zs[j + 1]));
            sheet.Quad(Corner(sheet, Point(0, j, true), xs[0], zs[j]),
                Corner(sheet, Point(0, j + 1, true), xs[0], zs[j + 1]),
                Corner(sheet, Point(0, j + 1, false), xs[0], zs[j + 1]),
                Corner(sheet, Point(0, j, false), xs[0], zs[j]));
        }

        for (var i = 0; i + 1 < xs.Length; i++)
        {
            // End caps: the -Z end seen from -Z (screen right is -X), the +Z end seen from +Z (screen right is +X).
            if (capStart)
                sheet.Quad(Corner(sheet, Point(i + 1, 0, true), xs[i + 1], zs[0]),
                    Corner(sheet, Point(i, 0, true), xs[i], zs[0]),
                    Corner(sheet, Point(i, 0, false), xs[i], zs[0]),
                    Corner(sheet, Point(i + 1, 0, false), xs[i + 1], zs[0]));
            var end = zs.Count - 1;
            if (capEnd)
                sheet.Quad(Corner(sheet, Point(i, end, true), xs[i], zs[end]),
                    Corner(sheet, Point(i + 1, end, true), xs[i + 1], zs[end]),
                    Corner(sheet, Point(i + 1, end, false), xs[i + 1], zs[end]),
                    Corner(sheet, Point(i, end, false), xs[i], zs[end]));
        }

        return sheet.Commit();
    }

    private static int Corner(MeshSheet sheet, Vector3 position, float x, float z) =>
        sheet.Vertex(position, PouchUv(x, z, true), CrimpWeight(z));

    // A grid of quads with shared vertices, so the pillow is smooth. The top is seen from +Y with -Z up the screen;
    // the bottom is seen from -Y, where +X is on the left.
    private static void PouchGrid(MeshSheet sheet, int across, int along, Func<int, int, int> vertex, bool top)
    {
        var index = new int[across, along];
        for (var j = 0; j < along; j++)
        for (var i = 0; i < across; i++)
            index[i, j] = vertex(i, j);

        for (var j = 0; j + 1 < along; j++)
        for (var i = 0; i + 1 < across; i++)
        {
            if (top)
                sheet.Quad(index[i, j], index[i + 1, j], index[i + 1, j + 1], index[i, j + 1]);
            else
                sheet.Quad(index[i + 1, j], index[i, j], index[i, j + 1], index[i + 1, j + 1]);
        }
    }

    // A box open at the top, origin at its bottom centre: outer walls printed Front (+Z), Back (-Z) and the given
    // side face (±X), a printed bottom, plain inner walls and floor, and the cut edge on top of each wall.
    private static ArrayMesh OpenBox(float width, float height, float depth, float wall, PrintFace side)
    {
        var sheet = new MeshSheet(false);
        var half = new Vector3(width / 2f, 0f, depth / 2f);
        var mid = height / 2f;
        sheet.Face(new Vector3(0f, mid, half.Z), Vector3.Back, Vector3.Up, width, height, PrintFace.Front);
        sheet.Face(new Vector3(0f, mid, -half.Z), Vector3.Forward, Vector3.Up, width, height, PrintFace.Back);
        sheet.Face(new Vector3(half.X, mid, 0f), Vector3.Right, Vector3.Up, depth, height, side);
        sheet.Face(new Vector3(-half.X, mid, 0f), Vector3.Left, Vector3.Up, depth, height, side);
        sheet.Face(Vector3.Zero, Vector3.Down, Vector3.Back, width, depth, PrintFace.Bottom);

        var innerMid = (height + wall) / 2f;
        var innerHeight = height - wall;
        var innerWidth = width - 2f * wall;
        var innerDepth = depth - 2f * wall;
        sheet.Face(new Vector3(0f, innerMid, half.Z - wall), Vector3.Forward, Vector3.Up, innerWidth, innerHeight,
            PrintFace.Inside);
        sheet.Face(new Vector3(0f, innerMid, -half.Z + wall), Vector3.Back, Vector3.Up, innerWidth, innerHeight,
            PrintFace.Inside);
        sheet.Face(new Vector3(half.X - wall, innerMid, 0f), Vector3.Left, Vector3.Up, innerDepth, innerHeight,
            PrintFace.Inside);
        sheet.Face(new Vector3(-half.X + wall, innerMid, 0f), Vector3.Right, Vector3.Up, innerDepth, innerHeight,
            PrintFace.Inside);
        sheet.Face(new Vector3(0f, wall, 0f), Vector3.Up, Vector3.Forward, innerWidth, innerDepth, PrintFace.Inside);

        sheet.Face(new Vector3(0f, height, half.Z - wall / 2f), Vector3.Up, Vector3.Forward, width, wall,
            PrintFace.Edge);
        sheet.Face(new Vector3(0f, height, -half.Z + wall / 2f), Vector3.Up, Vector3.Forward, width, wall,
            PrintFace.Edge);
        sheet.Face(new Vector3(half.X - wall / 2f, height, 0f), Vector3.Up, Vector3.Forward, wall, innerDepth,
            PrintFace.Edge);
        sheet.Face(new Vector3(-half.X + wall / 2f, height, 0f), Vector3.Up, Vector3.Forward, wall, innerDepth,
            PrintFace.Edge);
        return sheet.Commit();
    }

    // A thin panel between min and max: the top face printed as given with -Z up the print, the underside as given,
    // and cut edges all round.
    private static void Slab(MeshSheet sheet, Vector3 min, Vector3 max, PrintFace top, PrintFace bottom)
    {
        var size = max - min;
        var centre = (min + max) / 2f;
        sheet.Face(new Vector3(centre.X, max.Y, centre.Z), Vector3.Up, Vector3.Forward, size.X, size.Z, top);
        sheet.Face(new Vector3(centre.X, min.Y, centre.Z), Vector3.Down, Vector3.Forward, size.X, size.Z, bottom);
        sheet.Face(new Vector3(centre.X, centre.Y, max.Z), Vector3.Back, Vector3.Up, size.X, size.Y, PrintFace.Edge);
        sheet.Face(new Vector3(centre.X, centre.Y, min.Z), Vector3.Forward, Vector3.Up, size.X, size.Y,
            PrintFace.Edge);
        sheet.Face(new Vector3(max.X, centre.Y, centre.Z), Vector3.Right, Vector3.Up, size.Z, size.Y, PrintFace.Edge);
        sheet.Face(new Vector3(min.X, centre.Y, centre.Z), Vector3.Left, Vector3.Up, size.Z, size.Y, PrintFace.Edge);
    }

    /// <summary>
    ///     Collects triangles for one mesh surface. Godot treats clockwise triangles as front faces, so every quad is
    ///     given top-left, top-right, bottom-right, bottom-left as seen from the side it faces. A smooth sheet gets
    ///     normals averaged over the faces that share a position; a flat sheet takes each face's own normal.
    /// </summary>
    private sealed class MeshSheet
    {
        private readonly SurfaceTool _tool = new();
        private readonly bool _smooth;
        private int _count;

        public MeshSheet(bool smooth)
        {
            _smooth = smooth;
            _tool.Begin(Mesh.PrimitiveType.Triangles);
        }

        public int Vertex(Vector3 position, Vector2 uv, float red, Vector3? normal = null)
        {
            if (normal is { } given)
                _tool.SetNormal(given);
            _tool.SetUV(uv);
            _tool.SetColor(new Color(red, 0f, 0f));
            _tool.AddVertex(position);
            return _count++;
        }

        public void Quad(int topLeft, int topRight, int bottomRight, int bottomLeft)
        {
            foreach (var index in new[] { topLeft, topRight, bottomRight, topLeft, bottomRight, bottomLeft })
                _tool.AddIndex(index);
        }

        // A flat rectangle facing along normal, with up the top of its print; u runs left to right and v top to
        // bottom as seen from the front, and the face's print code goes into the vertex colour.
        public void Face(Vector3 centre, Vector3 normal, Vector3 up, float width, float height, PrintFace face)
        {
            var right = up.Cross(normal) * (width / 2f);
            var rise = up * (height / 2f);
            var code = ((int)face + 0.5f) / 8f;
            Quad(Vertex(centre - right + rise, new Vector2(0f, 0f), code, normal),
                Vertex(centre + right + rise, new Vector2(1f, 0f), code, normal),
                Vertex(centre + right - rise, new Vector2(1f, 1f), code, normal),
                Vertex(centre - right - rise, new Vector2(0f, 1f), code, normal));
        }

        public ArrayMesh Commit()
        {
            if (_smooth)
                _tool.GenerateNormals();
            _tool.GenerateTangents();
            return _tool.Commit();
        }
    }
}
