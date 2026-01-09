# Irregular Grid Mesh Implementation Plan

**Branch**: `feature/irregular-grid-mesh`
**Status**: Planning
**Based on**: `Scripts/Tools/irregular_grid_poc.py` proof-of-concept

## Overview

Replace the current regular TileMapLayer grid with an irregular quad mesh for terrain rendering while maintaining full functionality for navigation, visibility, structure placement, and movement.

### Goals
- Organic, non-grid terrain appearance
- Preserve dual-grid auto-tiling concept (vertices = data, quads = visuals)
- Maintain pathfinding, visibility, and exploration systems
- Keep structure/decoration placement working

### Key Insight
The dual-grid concept transfers directly:
- **Regular grid**: Data at cells, visuals at cell corners (offset by 0.5)
- **Irregular mesh**: Data at vertices, visuals on quads (which sample corner vertices)

---

## Phase 1: Core Mesh Infrastructure

**Goal**: Port Python POC to C# and create foundational data structures.

### 1.1 Data Structures

Create `Scripts/Features/Worldgen/IrregularMesh/` folder with:

```
IrregularMesh/
├── MeshVertex.cs        # Position + terrain data
├── MeshQuad.cs          # 4 vertices + computed properties
├── IrregularMesh.cs     # Collection of vertices and quads
├── MeshAdjacency.cs     # Precomputed neighbor relationships
└── MeshGenerator.cs     # Hex → triangles → quads → subdivide → relax
```

**MeshVertex.cs**
```csharp
public class MeshVertex
{
    public int Id { get; }
    public Vector2 Position { get; set; }
    public int TerrainType { get; set; }  // Tile ID or terrain enum
    public bool HasStructure { get; set; }

    // Computed during mesh building
    public List<int> AdjacentQuadIds { get; } = new();
    public List<int> AdjacentVertexIds { get; } = new();
}
```

**MeshQuad.cs**
```csharp
public class MeshQuad
{
    public int Id { get; }
    public int[] VertexIds { get; }  // Always 4, CCW order

    // Computed properties
    public Vector2 Centroid { get; }
    public float Area { get; }
    public int Corner16Bitmask { get; }  // Computed from vertex terrain

    // Adjacency (quads sharing an edge)
    public List<int> AdjacentQuadIds { get; } = new();
}
```

**IrregularMesh.cs**
```csharp
public class IrregularMesh
{
    public List<MeshVertex> Vertices { get; }
    public List<MeshQuad> Quads { get; }

    // Spatial lookup
    public MeshQuad? GetQuadAtPosition(Vector2 worldPos);
    public MeshVertex? GetNearestVertex(Vector2 worldPos);

    // Adjacency queries
    public IEnumerable<MeshQuad> GetAdjacentQuads(int quadId);
    public IEnumerable<MeshVertex> GetAdjacentVertices(int vertexId);
}
```

### 1.2 Mesh Generation Algorithm

Port from POC (`irregular_grid_poc.py`):

1. **Generate hex grid as triangle mesh** (lines 111-187)
   - Axial coordinates for hex centers
   - Create triangles connecting adjacent hex centers
   - Result: Connected triangle mesh

2. **Merge triangles into quads** (lines 194-295)
   - Build edge-to-faces adjacency
   - Randomly merge adjacent triangles (70% probability)
   - Preserve shared vertices

3. **Subdivide all faces** (lines 302-398)
   - Quads → 4 smaller quads
   - Remaining triangles → 3 quads
   - Use vertex cache for shared edge midpoints

4. **Lloyd relaxation** (lines 405-536)
   - Pin boundary vertices
   - Hybrid mode: Lloyd + area correction
   - 15 iterations for good equalization

### 1.3 Tasks

- [ ] Create `MeshVertex`, `MeshQuad`, `IrregularMesh` classes
- [ ] Port `generate_hex_grid()` to C#
- [ ] Port `merge_adjacent_triangles()` to C#
- [ ] Port `subdivide_all_faces()` with `VertexCache`
- [ ] Port `lloyd_relaxation()` with boundary pinning
- [ ] Build adjacency graph after mesh generation
- [ ] Add spatial lookup (quad-tree or simple bounds check)
- [ ] Unit tests for mesh generation

### 1.4 Validation

- Generate mesh, verify vertex/quad counts match POC
- Verify all quads have exactly 4 vertices
- Verify adjacency is symmetric (if A adjacent to B, B adjacent to A)
- Verify no degenerate quads (zero area)

---

## Phase 2: Map Data Abstraction

**Goal**: Create abstraction layer that works for both regular grid and irregular mesh.

### 2.1 Interface Design

Create `Scripts/Core/Interfaces/IMapData.cs`:

```csharp
public interface IMapData
{
    // Spatial queries
    int GetCellCount();
    bool IsValidCell(int cellId);
    Vector2 GetCellCenter(int cellId);
    float GetCellArea(int cellId);

    // Terrain data
    int GetTerrainType(int cellId);
    bool IsPassable(int cellId);
    bool IsTransparent(int cellId);

    // Adjacency
    IEnumerable<int> GetAdjacentCells(int cellId);
    float GetMovementCost(int fromCell, int toCell);

    // Spatial lookup
    int? GetCellAtPosition(Vector2 worldPos);
    IEnumerable<int> GetCellsInRadius(Vector2 center, float radius);

    // Structure/decoration
    bool HasStructure(int cellId);
    void SetStructure(int cellId, bool hasStructure);
}
```

### 2.2 Implementations

**RegularGridMapData** (adapter for existing `SimpleMapData`):
- Cell ID = y * width + x (linearized 2D index)
- GetCellCenter = (x + 0.5, y + 0.5) * tileSize
- GetAdjacentCells = 4 cardinal neighbors
- GetMovementCost = 1.0 (uniform)

**IrregularMeshMapData**:
- Cell ID = quad index
- GetCellCenter = quad centroid
- GetAdjacentCells = adjacent quads from mesh
- GetMovementCost = Euclidean distance between centroids

### 2.3 Tasks

- [ ] Define `IMapData` interface
- [ ] Create `RegularGridMapData` wrapping `SimpleMapData`
- [ ] Create `IrregularMeshMapData` wrapping `IrregularMesh`
- [ ] Update all map data consumers to use interface
- [ ] Verify existing functionality unchanged with adapter

---

## Phase 3: Pathfinding

**Goal**: Make pathfinding work with any `IMapData` implementation.

### 3.1 Generalize A*

Current `ExplorationAI.FindPath()` uses hardcoded 4-neighbor grid. Generalize:

```csharp
public class Pathfinder
{
    private readonly IMapData _mapData;

    public List<int> FindPath(int startCell, int goalCell)
    {
        // Standard A* but using:
        // - _mapData.GetAdjacentCells(cell) for neighbors
        // - _mapData.GetMovementCost(from, to) for g-cost
        // - Euclidean distance for heuristic (works for both grids)
    }

    private float Heuristic(int cellA, int cellB)
    {
        var posA = _mapData.GetCellCenter(cellA);
        var posB = _mapData.GetCellCenter(cellB);
        return posA.DistanceTo(posB);
    }
}
```

### 3.2 Update ExplorationAI

- Replace `Vector2I` position with `int cellId`
- Replace hardcoded neighbors with `_mapData.GetAdjacentCells()`
- Update path storage from `List<Vector2I>` to `List<int>`
- Convert cell IDs to world positions for rendering

### 3.3 Tasks

- [ ] Create generic `Pathfinder` class using `IMapData`
- [ ] Update `ExplorationAI` to use `Pathfinder`
- [ ] Update `FrontierExplorationBehavior` neighbor enumeration
- [ ] Update path rendering to use cell centers
- [ ] Test pathfinding on both regular and irregular maps

---

## Phase 4: Visibility System

**Goal**: Implement line-of-sight that works with irregular mesh.

### 4.1 Approach: Physics Raycast

Use Godot's physics for LOS checks:

```csharp
public class RaycastVisibilityChecker : IVisibilityChecker
{
    public bool CanSee(int fromCell, int toCell, IMapData mapData)
    {
        var from = mapData.GetCellCenter(fromCell);
        var to = mapData.GetCellCenter(toCell);

        var spaceState = GetWorld2D().DirectSpaceState;
        var query = PhysicsRayQueryParameters2D.Create(from, to);
        query.CollisionMask = OpaqueTerrainLayer;

        var result = spaceState.IntersectRay(query);
        return result.Count == 0;
    }
}
```

### 4.2 Collision Shape Generation

For irregular mesh, generate collision shapes at map creation:

```csharp
void GenerateCollisionShapes(IrregularMesh mesh, CollisionObject2D parent)
{
    foreach (var quad in mesh.Quads)
    {
        if (!IsTransparent(quad))
        {
            var shape = new ConvexPolygonShape2D();
            shape.Points = quad.Vertices.Select(v => v.Position).ToArray();

            var owner = parent.CreateShapeOwner(parent);
            parent.ShapeOwnerAddShape(owner, shape);
        }
    }
}
```

### 4.3 Alternative: Mesh Traversal

If physics approach is too slow, implement mesh-based visibility:

```csharp
bool CanSee(int fromQuad, int toQuad, IrregularMesh mesh)
{
    // BFS from fromQuad toward toQuad
    // Stop if we hit an opaque quad
    // Use ray-quad intersection to determine traversal order
}
```

### 4.4 Tasks

- [ ] Create `IVisibilityChecker` interface (if not exists)
- [ ] Implement `RaycastVisibilityChecker` using physics
- [ ] Generate collision shapes for opaque quads
- [ ] Update `FrontierExplorationBehavior` to use new checker
- [ ] Implement vision range check (distance-based)
- [ ] Test visibility with various terrain configurations

---

## Phase 5: Terrain Rendering

**Goal**: Render irregular mesh terrain with proper auto-tile textures.

### 5.1 Approach: ArrayMesh

Replace TileMapLayer with custom mesh rendering:

```csharp
public class IrregularTerrainRenderer : Node2D
{
    private MeshInstance2D _meshInstance;
    private ShaderMaterial _terrainMaterial;

    public void RenderTerrain(IrregularMesh mesh, Texture2D atlas)
    {
        var surfaceTool = new SurfaceTool();
        surfaceTool.Begin(Mesh.PrimitiveType.Triangles);

        foreach (var quad in mesh.Quads)
        {
            var bitmask = quad.Corner16Bitmask;
            var uvRect = GetAtlasUVRect(bitmask);

            // Add quad as two triangles
            AddQuadTriangles(surfaceTool, quad, uvRect);
        }

        var arrayMesh = surfaceTool.Commit();
        _meshInstance.Mesh = arrayMesh;
        _terrainMaterial.SetShaderParameter("atlas", atlas);
    }
}
```

### 5.2 UV Mapping

Map Corner16 bitmask to atlas coordinates:

```csharp
Rect2 GetAtlasUVRect(int bitmask, TransitionMapData transitions)
{
    // Use existing transition_map.json data
    var coords = transitions.GetTileCoords(bitmask);
    var tileSize = 16f;
    var atlasSize = atlas.GetSize();

    return new Rect2(
        coords.X * tileSize / atlasSize.X,
        coords.Y * tileSize / atlasSize.Y,
        tileSize / atlasSize.X,
        tileSize / atlasSize.Y
    );
}
```

### 5.3 Quad Corner Ordering

Critical for correct UV mapping. Match POC's angle-based sorting:

```csharp
Vector2[] GetSortedCorners(MeshQuad quad, IrregularMesh mesh)
{
    var centroid = quad.Centroid;
    var corners = quad.VertexIds
        .Select(id => mesh.Vertices[id].Position)
        .OrderBy(p => Mathf.Atan2(p.Y - centroid.Y, p.X - centroid.X))
        .ToArray();

    // corners[0] = SW, [1] = SE, [2] = NE, [3] = NW (CCW from -X axis)
    return corners;
}
```

### 5.4 Tasks

- [ ] Create `IrregularTerrainRenderer` class
- [ ] Implement `SurfaceTool` mesh building
- [ ] Implement UV mapping from bitmask to atlas
- [ ] Handle multi-variant tiles (random selection)
- [ ] Add shader for proper texture sampling
- [ ] Test rendering matches POC output

---

## Phase 6: Structure & Decoration Layers

**Goal**: Place structures and decorations on irregular mesh.

### 6.1 Vertex-Based Structure Placement

Structures placed at mesh vertices (consistent with data grid concept):

```csharp
public class StructurePlacement
{
    public void PlaceStructure(int vertexId, StructureType type)
    {
        var vertex = _mesh.Vertices[vertexId];
        vertex.HasStructure = true;

        // Mark adjacent quads as blocked
        foreach (var quadId in vertex.AdjacentQuadIds)
        {
            _mesh.Quads[quadId].RecalculatePassability();
        }

        // Spawn visual at vertex position
        SpawnStructureVisual(vertex.Position, type);
    }
}
```

### 6.2 Decoration Rendering

Options:
1. **Sprite2D at quad centroids**: Simple, individual sprites
2. **Additional mesh layer**: Batch decorations into second ArrayMesh
3. **MultiMesh**: For many identical decorations (grass, rocks)

### 6.3 Tasks

- [ ] Implement vertex-based structure placement
- [ ] Update passability when structures placed
- [ ] Create decoration rendering system
- [ ] Ensure decorations respect terrain (no trees in water)
- [ ] Test structure blocking affects pathfinding

---

## Phase 7: Movement & Position Tracking

**Goal**: Update movement system to work with quad IDs.

### 7.1 Position Representation

Change from `Vector2I` grid position to `int` quad ID:

```csharp
// Before
public Vector2I CurrentPosition { get; private set; }

// After
public int CurrentQuadId { get; private set; }
public Vector2 CurrentWorldPosition => _mapData.GetCellCenter(CurrentQuadId);
```

### 7.2 Movement Events

Update event signatures:

```csharp
// Before
public event Action<Vector2I>? PlayerMoved;

// After
public event Action<int, Vector2>? PlayerMoved;  // (quadId, worldPos)
```

### 7.3 Visual Movement

Smooth movement between quad centroids:

```csharp
async Task MoveToQuad(int targetQuad)
{
    var startPos = CurrentWorldPosition;
    var endPos = _mapData.GetCellCenter(targetQuad);

    // Tween movement
    var tween = CreateTween();
    tween.TweenProperty(_sprite, "position", endPos, MoveDuration);
    await ToSignal(tween, Tween.SignalName.Finished);

    CurrentQuadId = targetQuad;
    PlayerMoved?.Invoke(CurrentQuadId, endPos);
}
```

### 7.4 Tasks

- [ ] Update `ExplorationAI` position tracking
- [ ] Update movement methods to use quad IDs
- [ ] Add smooth visual movement between quads
- [ ] Update all event handlers for new signatures
- [ ] Test movement along irregular paths

---

## Phase 8: Fog of War

**Goal**: Implement per-quad fog of war.

### 8.1 Approach

Track visibility state per quad:

```csharp
public enum FogState { Hidden, Revealed, Visible }

public class FogOfWar
{
    private Dictionary<int, FogState> _fogState;

    public void UpdateVisibility(int observerQuad, float visionRange)
    {
        // Mark quads within range and LOS as Visible
        // Previously visible quads become Revealed
        // Never-seen quads stay Hidden
    }
}
```

### 8.2 Rendering Options

1. **Per-quad alpha overlay**: Additional mesh layer with fog texture
2. **Shader-based**: Pass fog state to terrain shader
3. **Stencil-based**: Use stencil buffer to mask hidden areas

### 8.3 Tasks

- [ ] Create `FogOfWar` tracking system
- [ ] Implement visibility update logic
- [ ] Choose and implement fog rendering approach
- [ ] Integrate with exploration visibility updates

---

## Phase 9: WFC Integration

**Goal**: Connect WFC terrain generation to irregular mesh.

### 9.1 Approach

WFC operates on vertices (data grid), mesh is generated separately:

1. Generate irregular mesh (geometry only)
2. Run WFC on vertex positions
3. Assign WFC results to vertex terrain types
4. Compute quad bitmasks from vertex terrain

### 9.2 WFC Grid Adaptation

Create WFC grid from mesh vertices:

```csharp
public class MeshWfcGrid : IWfcGrid
{
    public IEnumerable<int> GetNeighbors(int vertexId)
    {
        return _mesh.Vertices[vertexId].AdjacentVertexIds;
    }

    public void SetTile(int vertexId, int tileId)
    {
        _mesh.Vertices[vertexId].TerrainType = tileId;
    }
}
```

### 9.3 Constraint Adaptation

`AutoTileGapConstraint` needs mesh-aware neighbor checking:
- Instead of 8-directional grid neighbors
- Use mesh vertex adjacency

### 9.4 Tasks

- [ ] Create `MeshWfcGrid` adapter
- [ ] Update constraints for mesh adjacency
- [ ] Test WFC produces valid terrain on mesh
- [ ] Verify biome distribution works

---

## Phase 10: Integration & Polish

**Goal**: Wire everything together and polish.

### 10.1 Scene Setup

Update `SimpleWorldMapScreen` or create `IrregularWorldMapScreen`:

```csharp
public partial class IrregularWorldMapScreen : Node2D
{
    private IrregularMesh _mesh;
    private IrregularMeshMapData _mapData;
    private IrregularTerrainRenderer _terrainRenderer;
    private Pathfinder _pathfinder;
    private RaycastVisibilityChecker _visibilityChecker;
    private FogOfWar _fogOfWar;

    public void Initialize(int rings, int seed)
    {
        _mesh = MeshGenerator.Generate(rings, seed);
        _mapData = new IrregularMeshMapData(_mesh);
        // ... wire up all systems
    }
}
```

### 10.2 Debug Visualization

Port POC debug renders:
- Mesh wireframe overlay
- Vertex terrain markers
- Quad bitmask display
- Adjacency graph visualization

### 10.3 Tasks

- [ ] Create integrated world map screen
- [ ] Wire up all subsystems
- [ ] Add debug visualization toggles
- [ ] Performance profiling
- [ ] Fix any integration bugs

---

## Testing Strategy

### Unit Tests
- Mesh generation produces valid geometry
- Adjacency relationships are symmetric
- Bitmask calculation matches expected values
- Pathfinding finds valid paths
- Visibility blocks correctly

### Integration Tests
- Full map generation pipeline
- Exploration with pathfinding and visibility
- Structure placement blocks movement
- Fog of war updates correctly

### Visual Tests
- Terrain renders without gaps
- Auto-tile transitions look correct
- Movement appears smooth
- Debug overlays show correct data

---

## Risk Mitigation

### Performance Risks
- **Quad lookup**: Use spatial hash or quad-tree if point-in-polygon is slow
- **Collision shapes**: Batch into compound shapes if too many
- **Mesh rendering**: Profile ArrayMesh vs alternatives

### Correctness Risks
- **UV mapping**: Verify corner ordering matches POC exactly
- **Adjacency**: Validate with small test meshes first
- **WFC integration**: Test on regular mesh before irregular

### Rollback Plan
- Keep `SimpleMapData` and regular grid code intact
- Use `IMapData` abstraction to switch implementations
- Branch allows easy return to master if needed

---

## File Checklist

### New Files to Create
```
Scripts/Features/Worldgen/IrregularMesh/
├── MeshVertex.cs
├── MeshQuad.cs
├── IrregularMesh.cs
├── MeshGenerator.cs
├── MeshAdjacency.cs
└── IrregularMeshMapData.cs

Scripts/Core/Interfaces/
├── IMapData.cs
└── IVisibilityChecker.cs (if not exists)

Scripts/Core/Services/
├── Pathfinder.cs
└── RaycastVisibilityChecker.cs

Scripts/Features/Deckbuilder/Models/
└── IrregularWorldMapScreen.cs

Scripts/Features/Deckbuilder/Rendering/
├── IrregularTerrainRenderer.cs
└── IrregularFogOfWar.cs
```

### Files to Modify
```
Scripts/Features/Deckbuilder/Services/ExplorationAI.cs
Scripts/Features/Deckbuilder/Services/FrontierExplorationBehavior.cs
Scripts/Features/Deckbuilder/Services/GameSessionService.cs
Scripts/Features/Worldgen/Wfc/WfcMapGenerator.cs (optional)
```

### Files to Keep Unchanged
```
Scripts/Features/Deckbuilder/Services/SimpleMapData.cs (keep for comparison)
Scripts/Features/Deckbuilder/Models/SimpleWorldMapScreen.cs (keep as fallback)
Scripts/Features/Player/Controllers/PlayerController.cs
Scripts/Features/Deckbuilder/Services/SimpleCombatSystem.cs
```

---

## Success Criteria

1. **Visual**: Terrain renders with organic, non-grid appearance
2. **Navigation**: Pathfinding works correctly on irregular mesh
3. **Visibility**: LOS blocks appropriately, fog of war functions
4. **Structures**: Can place structures that block movement
5. **Performance**: Comparable to or better than current implementation
6. **Maintainability**: Clean abstraction allows switching grid types

---

## Next Steps

1. Review and refine this plan
2. Start Phase 1: Core Mesh Infrastructure
3. Iterate through phases, validating each before proceeding
