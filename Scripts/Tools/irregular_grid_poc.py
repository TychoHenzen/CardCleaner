#!/usr/bin/env python3
"""
Proof-of-concept: Irregular quad mesh with dual-grid auto-tiling.

Algorithm:
1. Start with hexagonal grid
2. Subdivide hexagons into 6 triangles each
3. Randomly merge adjacent triangles into quads by removing shared edges
4. Subdivide quads into 4 smaller quads, remaining triangles into 3 quads
5. Run relaxation to equalize quad sizes
6. Assign terrain data to vertices (data grid)
7. Compute bitmasks for each quad using Corner16 (visual grid)
8. Render auto-tiles deformed onto the irregular quads
"""

import numpy as np
import matplotlib.pyplot as plt
import matplotlib.patches as patches
from matplotlib.collections import PatchCollection
from matplotlib.path import Path
import matplotlib.colors as mcolors
from dataclasses import dataclass, field
from typing import List, Tuple, Set, Dict, Optional
import random
from PIL import Image
import os

# ============================================================================
# Corner16 Bitmask Constants (matching NeighborBitmaskCorner.cs)
# ============================================================================
NE = 1  # 0b0001
SE = 2  # 0b0010
SW = 4  # 0b0100
NW = 8  # 0b1000

# Bitmask to visual representation for debugging
BITMASK_CHARS = {
    0: "○",   # Empty
    15: "■",  # Full
    1: "◢", 2: "◣", 4: "◤", 8: "◥",  # Single corners
    3: "▐", 6: "▄", 12: "▌", 9: "▀",  # Edges
    5: "◨", 10: "◧",  # Diagonals
    7: "◧", 11: "◨", 13: "◩", 14: "◪",  # Three corners
}

# ============================================================================
# Geometry Primitives
# ============================================================================

@dataclass
class Vertex:
    """A vertex in the mesh with position and terrain data."""
    pos: np.ndarray
    terrain: int = 0  # 0 = empty, 1 = filled

    def __hash__(self):
        return id(self)

@dataclass
class Edge:
    """An edge connecting two vertices."""
    v1: Vertex
    v2: Vertex

    def __hash__(self):
        return hash((id(self.v1), id(self.v2)))

    def __eq__(self, other):
        if not isinstance(other, Edge):
            return False
        return (self.v1 is other.v1 and self.v2 is other.v2) or \
               (self.v1 is other.v2 and self.v2 is other.v1)

@dataclass
class Face:
    """A face (triangle or quad) defined by vertices in CCW order."""
    vertices: List[Vertex]

    @property
    def is_triangle(self) -> bool:
        return len(self.vertices) == 3

    @property
    def is_quad(self) -> bool:
        return len(self.vertices) == 4

    @property
    def centroid(self) -> np.ndarray:
        return np.mean([v.pos for v in self.vertices], axis=0)

    def get_edges(self) -> List[Tuple[Vertex, Vertex]]:
        """Get edges as vertex pairs."""
        edges = []
        n = len(self.vertices)
        for i in range(n):
            edges.append((self.vertices[i], self.vertices[(i+1) % n]))
        return edges


@dataclass
class Mesh:
    """A mesh of vertices and faces."""
    vertices: List[Vertex] = field(default_factory=list)
    faces: List[Face] = field(default_factory=list)


# ============================================================================
# Step 1: Generate Hexagonal Grid as Connected Triangle Mesh
# ============================================================================

def generate_hex_grid(rings: int = 3, radius: float = 1.0) -> Mesh:
    """
    Generate a hexagonal grid as a CONNECTED triangle mesh.

    The key insight: we create triangles that tile the plane, where each
    triangle connects THREE hex centers (not center-to-corners).

    In a hex grid, the dual graph (connecting hex centers) forms a triangular lattice.
    We generate this triangular lattice directly.
    """
    mesh = Mesh()
    vertex_cache = {}  # (q, r) -> Vertex for hex center positions

    # Spacing for pointy-top hexagons
    # Horizontal distance between adjacent hex centers in same row
    horiz = radius * np.sqrt(3)
    # Vertical distance between rows
    vert = radius * 1.5

    def axial_to_cartesian(q: int, r: int) -> Tuple[float, float]:
        x = horiz * (q + r * 0.5)
        y = vert * r
        return x, y

    def get_or_create_vertex(q: int, r: int) -> Vertex:
        key = (q, r)
        if key not in vertex_cache:
            x, y = axial_to_cartesian(q, r)
            v = Vertex(pos=np.array([x, y]))
            vertex_cache[key] = v
            mesh.vertices.append(v)
        return vertex_cache[key]

    # Generate all hex centers within the ring bounds
    coords = []
    for q in range(-rings, rings + 1):
        for r in range(-rings, rings + 1):
            if abs(q + r) <= rings:
                coords.append((q, r))

    coord_set = set(coords)

    # Create triangles connecting adjacent hex centers
    # In axial coordinates, each hex center (q, r) has 6 neighbors:
    # (q+1, r), (q-1, r), (q, r+1), (q, r-1), (q+1, r-1), (q-1, r+1)
    #
    # We create two triangles per hex center (for the "upper-right" region):
    # Triangle 1: (q,r) - (q+1,r) - (q,r+1)  [if all exist]
    # Triangle 2: (q,r) - (q+1,r) - (q+1,r-1) [if all exist]

    created_triangles = set()

    for q, r in coords:
        # Triangle type 1: (q,r), (q+1,r), (q,r+1)
        if (q+1, r) in coord_set and (q, r+1) in coord_set:
            tri_key = tuple(sorted([(q,r), (q+1,r), (q,r+1)]))
            if tri_key not in created_triangles:
                v0 = get_or_create_vertex(q, r)
                v1 = get_or_create_vertex(q+1, r)
                v2 = get_or_create_vertex(q, r+1)
                mesh.faces.append(Face(vertices=[v0, v1, v2]))
                created_triangles.add(tri_key)

        # Triangle type 2: (q,r), (q+1,r-1), (q+1,r)
        if (q+1, r-1) in coord_set and (q+1, r) in coord_set:
            tri_key = tuple(sorted([(q,r), (q+1,r-1), (q+1,r)]))
            if tri_key not in created_triangles:
                v0 = get_or_create_vertex(q, r)
                v1 = get_or_create_vertex(q+1, r-1)
                v2 = get_or_create_vertex(q+1, r)
                mesh.faces.append(Face(vertices=[v0, v2, v1]))  # CCW order
                created_triangles.add(tri_key)

    print(f"   Generated {len(mesh.vertices)} vertices, {len(mesh.faces)} triangles")
    print(f"   All triangles connected in a continuous mesh")

    return mesh


# ============================================================================
# Step 2: Merge Triangles into Quads
# ============================================================================

def find_shared_edge(face1: Face, face2: Face) -> Optional[Tuple[Vertex, Vertex]]:
    """Find the shared edge between two faces, if any."""
    edges1 = set()
    for v1, v2 in face1.get_edges():
        edges1.add(frozenset([id(v1), id(v2)]))

    for v1, v2 in face2.get_edges():
        if frozenset([id(v1), id(v2)]) in edges1:
            return (v1, v2)
    return None


def merge_triangles_to_quad(tri1: Face, tri2: Face, shared_edge: Tuple[Vertex, Vertex]) -> Face:
    """Merge two triangles sharing an edge into a quad."""
    # Find vertices not on the shared edge
    v1, v2 = shared_edge

    def get_opposite_vertex(tri: Face) -> Vertex:
        for v in tri.vertices:
            if v is not v1 and v is not v2:
                return v
        raise ValueError("No opposite vertex found")

    opp1 = get_opposite_vertex(tri1)
    opp2 = get_opposite_vertex(tri2)

    # Order vertices as a quad (CCW)
    # Start from opp1, go through shared edge vertices, then opp2
    # Need to determine correct order based on triangle orientation

    # Find the order of shared edge in tri1
    edges1 = tri1.get_edges()
    for i, (a, b) in enumerate(edges1):
        if (a is v1 and b is v2) or (a is v2 and b is v1):
            # The edge goes from a to b in tri1
            if a is v1:
                # tri1 goes: ... -> v1 -> v2 -> ...
                quad_verts = [opp1, v1, opp2, v2]
            else:
                # tri1 goes: ... -> v2 -> v1 -> ...
                quad_verts = [opp1, v2, opp2, v1]
            break

    return Face(vertices=quad_verts)


def merge_adjacent_triangles(mesh: Mesh, merge_probability: float = 0.7) -> Mesh:
    """
    Randomly merge adjacent triangles into quads.
    Only merge if both faces are triangles and share exactly one edge.
    """
    # Build adjacency map
    edge_to_faces: Dict[frozenset, List[Face]] = {}

    for face in mesh.faces:
        for v1, v2 in face.get_edges():
            key = frozenset([id(v1), id(v2)])
            if key not in edge_to_faces:
                edge_to_faces[key] = []
            edge_to_faces[key].append(face)

    # Find mergeable pairs
    merged = set()  # Track merged faces by id
    new_faces = []

    # Randomize edge order for variety
    edges = list(edge_to_faces.keys())
    random.shuffle(edges)

    for edge_key in edges:
        faces = edge_to_faces[edge_key]
        if len(faces) != 2:
            continue

        f1, f2 = faces
        if id(f1) in merged or id(f2) in merged:
            continue

        if not f1.is_triangle or not f2.is_triangle:
            continue

        if random.random() > merge_probability:
            continue

        # Find actual shared edge
        shared = find_shared_edge(f1, f2)
        if shared is None:
            continue

        # Merge into quad
        quad = merge_triangles_to_quad(f1, f2, shared)
        new_faces.append(quad)
        merged.add(id(f1))
        merged.add(id(f2))

    # Keep unmerged triangles
    for face in mesh.faces:
        if id(face) not in merged:
            new_faces.append(face)

    mesh.faces = new_faces
    return mesh


# ============================================================================
# Step 3: Subdivide Quads and Triangles
# ============================================================================

class VertexCache:
    """Cache vertices by position to ensure shared edges use same vertex."""

    def __init__(self, mesh: Mesh, precision: int = 6):
        self.mesh = mesh
        self.precision = precision
        self.cache: Dict[Tuple[float, float], Vertex] = {}

        # Pre-populate with existing vertices
        for v in mesh.vertices:
            key = self._make_key(v.pos)
            self.cache[key] = v

    def _make_key(self, pos: np.ndarray) -> Tuple[float, float]:
        return (round(float(pos[0]), self.precision),
                round(float(pos[1]), self.precision))

    def get_or_create(self, pos: np.ndarray) -> Vertex:
        key = self._make_key(pos)
        if key not in self.cache:
            v = Vertex(pos=pos.copy())
            self.cache[key] = v
            self.mesh.vertices.append(v)
        return self.cache[key]

    def get_midpoint(self, v1: Vertex, v2: Vertex) -> Vertex:
        """Get or create vertex at midpoint of two vertices."""
        mid_pos = (v1.pos + v2.pos) / 2
        return self.get_or_create(mid_pos)

    def get_centroid(self, vertices: List[Vertex]) -> Vertex:
        """Get or create vertex at centroid of vertices."""
        centroid_pos = np.mean([v.pos for v in vertices], axis=0)
        return self.get_or_create(centroid_pos)


def subdivide_quad(quad: Face, cache: VertexCache) -> List[Face]:
    """Subdivide a quad into 4 smaller quads using shared midpoints."""
    v = quad.vertices

    # Get midpoints of each edge (shared with adjacent faces!)
    m01 = cache.get_midpoint(v[0], v[1])
    m12 = cache.get_midpoint(v[1], v[2])
    m23 = cache.get_midpoint(v[2], v[3])
    m30 = cache.get_midpoint(v[3], v[0])

    # Center point (unique to this quad)
    center = cache.get_centroid(v)

    # Create 4 sub-quads
    return [
        Face(vertices=[v[0], m01, center, m30]),
        Face(vertices=[m01, v[1], m12, center]),
        Face(vertices=[center, m12, v[2], m23]),
        Face(vertices=[m30, center, m23, v[3]]),
    ]


def subdivide_triangle(tri: Face, cache: VertexCache) -> List[Face]:
    """
    Subdivide a triangle into 3 quads using shared midpoints.
    Each quad connects: vertex -> edge midpoint -> centroid -> other edge midpoint
    """
    v = tri.vertices

    # Edge midpoints (shared with adjacent faces!)
    m01 = cache.get_midpoint(v[0], v[1])
    m12 = cache.get_midpoint(v[1], v[2])
    m20 = cache.get_midpoint(v[2], v[0])

    # Centroid (unique to this triangle)
    center = cache.get_centroid(v)

    # Create 3 quads (one per original vertex)
    return [
        Face(vertices=[v[0], m01, center, m20]),
        Face(vertices=[v[1], m12, center, m01]),
        Face(vertices=[v[2], m20, center, m12]),
    ]


def subdivide_all_faces(mesh: Mesh) -> Mesh:
    """Subdivide all faces: quads into 4 quads, triangles into 3 quads."""
    cache = VertexCache(mesh)
    new_faces = []

    for face in mesh.faces:
        if face.is_quad:
            new_faces.extend(subdivide_quad(face, cache))
        elif face.is_triangle:
            new_faces.extend(subdivide_triangle(face, cache))
        else:
            # Keep other faces as-is
            new_faces.append(face)

    mesh.faces = new_faces
    return mesh


# ============================================================================
# Step 4: Relaxation Algorithm
# ============================================================================

def find_boundary_vertices(mesh: Mesh) -> Set[int]:
    """
    Find vertices on the outer boundary of the mesh.
    These are vertices that belong to edges which are only part of ONE face.
    """
    # Count how many faces each edge belongs to
    edge_face_count: Dict[frozenset, int] = {}

    for face in mesh.faces:
        for v1, v2 in face.get_edges():
            edge_key = frozenset([id(v1), id(v2)])
            edge_face_count[edge_key] = edge_face_count.get(edge_key, 0) + 1

    # Boundary edges are those with only 1 face
    boundary_vertex_ids = set()
    for edge_key, count in edge_face_count.items():
        if count == 1:
            # This is a boundary edge - both vertices are on boundary
            boundary_vertex_ids.update(edge_key)

    return boundary_vertex_ids


def compute_face_area(face: Face) -> float:
    """Compute area of a face using shoelace formula."""
    verts = [v.pos for v in face.vertices]
    n = len(verts)
    if n < 3:
        return 0.0

    # Shoelace formula
    area = 0.0
    for i in range(n):
        j = (i + 1) % n
        area += verts[i][0] * verts[j][1]
        area -= verts[j][0] * verts[i][1]

    return abs(area) / 2.0


def lloyd_relaxation(mesh: Mesh, iterations: int = 10, pin_boundary: bool = True,
                     mode: str = "hybrid") -> Mesh:
    """
    Run relaxation to equalize quad sizes.

    Modes:
    - "edge": Classic Lloyd relaxation (equalizes edge lengths)
    - "hybrid": Lloyd + area correction (best for texture mapping)

    If pin_boundary=True, outer boundary vertices are pinned in place.
    """
    # Build vertex-to-faces adjacency
    vertex_faces: Dict[int, List[Face]] = {}
    for face in mesh.faces:
        for v in face.vertices:
            if id(v) not in vertex_faces:
                vertex_faces[id(v)] = []
            vertex_faces[id(v)].append(face)

    # Find boundary vertices to pin
    pinned_vertices = find_boundary_vertices(mesh) if pin_boundary else set()

    print(f"   Pinning {len(pinned_vertices)} boundary vertices")
    print(f"   Relaxation mode: {mode}")

    for iteration in range(iterations):
        new_positions = {}

        # Compute areas and target area
        face_areas = {id(f): compute_face_area(f) for f in mesh.faces}
        all_areas = [a for a in face_areas.values() if a > 0]
        target_area = np.mean(all_areas) if all_areas else 1.0

        for vertex in mesh.vertices:
            vid = id(vertex)

            # Skip pinned boundary vertices
            if vid in pinned_vertices:
                continue

            faces = vertex_faces.get(vid, [])
            if not faces:
                continue

            # Classic Lloyd: move toward centroid of adjacent faces
            lloyd_target = np.mean([f.centroid for f in faces], axis=0)

            if mode == "hybrid":
                # Add area correction: push vertex away from centroids of large faces,
                # pull toward centroids of small faces
                area_correction = np.zeros(2)

                for f in faces:
                    area = face_areas[id(f)]
                    if area < 1e-6:
                        continue

                    # How much this face deviates from target (positive = too large)
                    area_ratio = area / target_area
                    deviation = area_ratio - 1.0  # >0 means too large, <0 means too small

                    # Direction from face centroid to vertex
                    to_vertex = vertex.pos - f.centroid
                    dist = np.linalg.norm(to_vertex)
                    if dist > 1e-6:
                        direction = to_vertex / dist
                        # If face is too large, push vertex outward (expand face)
                        # If face is too small, pull vertex inward (shrink face)
                        # Wait, that's backwards - to shrink a large face, pull vertex toward centroid
                        # To expand a small face, push vertex away from centroid
                        area_correction -= direction * deviation * 0.1

                target = lloyd_target + area_correction
            else:
                target = lloyd_target

            # Move toward target
            new_positions[vid] = vertex.pos + (target - vertex.pos) * 0.5

        # Apply new positions
        for vertex in mesh.vertices:
            if id(vertex) in new_positions:
                vertex.pos = new_positions[id(vertex)]

        # Print area stats
        if iteration == 0 or iteration == iterations - 1:
            areas = [compute_face_area(f) for f in mesh.faces if f.is_quad]
            if areas:
                print(f"   Iteration {iteration}: area min={min(areas):.4f}, max={max(areas):.4f}, "
                      f"ratio={max(areas)/min(areas):.2f}x, std={np.std(areas):.4f}")

    return mesh


# ============================================================================
# Step 5: Assign Terrain Data
# ============================================================================

def assign_terrain_blob(mesh: Mesh, center: np.ndarray, radius: float):
    """Assign terrain=1 to vertices within radius of center."""
    for vertex in mesh.vertices:
        dist = np.linalg.norm(vertex.pos - center)
        if dist < radius:
            vertex.terrain = 1


def assign_terrain_noise(mesh: Mesh, threshold: float = 0.5, scale: float = 2.0):
    """Assign terrain based on simple noise pattern."""
    for vertex in mesh.vertices:
        # Simple 2D noise approximation using sin waves
        x, y = vertex.pos
        noise = (np.sin(x * scale) + np.sin(y * scale) +
                 np.sin((x + y) * scale * 0.7) +
                 np.sin((x - y) * scale * 0.5)) / 4
        noise = (noise + 1) / 2  # Normalize to 0-1
        vertex.terrain = 1 if noise > threshold else 0


def assign_terrain_islands(mesh: Mesh, num_islands: int = 3):
    """Create several island blobs."""
    # Find mesh bounds
    all_pos = np.array([v.pos for v in mesh.vertices])
    min_pos = all_pos.min(axis=0)
    max_pos = all_pos.max(axis=0)

    for _ in range(num_islands):
        center = np.array([
            random.uniform(min_pos[0], max_pos[0]),
            random.uniform(min_pos[1], max_pos[1])
        ])
        radius = random.uniform(1.0, 2.5)
        assign_terrain_blob(mesh, center, radius)


# ============================================================================
# Step 6: Compute Bitmasks for Each Quad
# ============================================================================

def compute_quad_bitmask(quad: Face) -> int:
    """
    Compute Corner16 bitmask for a quad based on its corner vertex terrain.

    Vertices are sorted by angle around centroid (CCW from -X axis),
    then mapped to corners:
    - Index 0 (angle ~180°, pointing left) → SW
    - Index 1 (angle ~270°, pointing down) → SE
    - Index 2 (angle ~0°, pointing right) → NE
    - Index 3 (angle ~90°, pointing up) → NW

    Corner16 format:
    - NE = bit 0 (1)
    - SE = bit 1 (2)
    - SW = bit 2 (4)
    - NW = bit 3 (8)
    """
    centroid = quad.centroid

    # Sort vertices by angle around centroid (CCW from +X axis)
    def vertex_angle(v):
        return np.arctan2(v.pos[1] - centroid[1], v.pos[0] - centroid[0])

    sorted_verts = sorted(quad.vertices, key=vertex_angle)

    # Now sorted_verts[0] has the smallest angle (closest to -π, i.e., pointing left/west)
    # Going CCW: index 0 is ~west, 1 is ~south, 2 is ~east, 3 is ~north
    # Map to corners: SW, SE, NE, NW
    corner_map = [
        ('SW', SW),  # index 0 - smallest angle (west-ish)
        ('SE', SE),  # index 1 - next CCW (south-ish)
        ('NE', NE),  # index 2 - next CCW (east-ish)
        ('NW', NW),  # index 3 - largest angle (north-ish)
    ]

    mask = 0
    for i, (corner_name, bit) in enumerate(corner_map):
        if i < len(sorted_verts) and sorted_verts[i].terrain:
            mask |= bit

    return mask


def get_quad_corner_info(quad: Face) -> List[Tuple[str, int, Vertex]]:
    """
    Get corner classification info for debugging.
    Returns list of (corner_name, bit_value, vertex) tuples.
    """
    centroid = quad.centroid

    def vertex_angle(v):
        return np.arctan2(v.pos[1] - centroid[1], v.pos[0] - centroid[0])

    sorted_verts = sorted(quad.vertices, key=vertex_angle)

    corner_map = [
        ('SW', SW),
        ('SE', SE),
        ('NE', NE),
        ('NW', NW),
    ]

    result = []
    for i, (corner_name, bit) in enumerate(corner_map):
        if i < len(sorted_verts):
            result.append((corner_name, bit, sorted_verts[i]))

    return result


# ============================================================================
# Step 7: Rendering
# ============================================================================

# Color scheme for different bitmasks
def get_bitmask_color(bitmask: int) -> str:
    """Get a color for the bitmask visualization."""
    # Use terrain amount for base color intensity
    terrain_count = bin(bitmask).count('1')

    if terrain_count == 0:
        return '#E8DCC4'  # Empty - sand/dirt color
    elif terrain_count == 4:
        return '#2D5016'  # Full - dark grass
    elif terrain_count == 1:
        return '#7CB342'  # One corner - light grass
    elif terrain_count == 2:
        return '#558B2F'  # Two corners - medium grass
    else:
        return '#33691E'  # Three corners - darker grass


def get_transition_color(bitmask: int) -> Tuple[str, str]:
    """
    Get inner and outer colors for transition rendering.
    Returns (grass_color, dirt_color) with alpha based on bitmask.
    """
    grass = '#4CAF50'  # Green
    dirt = '#8D6E63'   # Brown

    return grass, dirt


def render_mesh_simple(mesh: Mesh, output_path: str, title: str = "Irregular Quad Mesh"):
    """Simple rendering showing just the mesh structure and terrain."""
    fig, ax = plt.subplots(1, 1, figsize=(12, 12))

    # Draw faces
    for face in mesh.faces:
        if not face.is_quad:
            continue

        verts = [v.pos for v in face.vertices]
        verts.append(verts[0])  # Close the polygon

        bitmask = compute_quad_bitmask(face)
        color = get_bitmask_color(bitmask)

        polygon = plt.Polygon(verts[:-1], facecolor=color, edgecolor='#333333',
                             linewidth=0.5, alpha=0.9)
        ax.add_patch(polygon)

    # Draw vertices
    filled = [v for v in mesh.vertices if v.terrain == 1]
    empty = [v for v in mesh.vertices if v.terrain == 0]

    if filled:
        ax.scatter([v.pos[0] for v in filled], [v.pos[1] for v in filled],
                  c='darkgreen', s=20, zorder=5, marker='o')
    if empty:
        ax.scatter([v.pos[0] for v in empty], [v.pos[1] for v in empty],
                  c='tan', s=15, zorder=5, marker='o')

    ax.set_aspect('equal')
    ax.set_title(title)
    ax.axis('off')

    plt.tight_layout()
    plt.savefig(output_path, dpi=150, bbox_inches='tight',
                facecolor='white', edgecolor='none')
    plt.close()
    print(f"Saved: {output_path}")


def render_mesh_with_autotiles(mesh: Mesh, output_path: str):
    """
    Render mesh with flat-color auto-tile visualization (fallback).
    """
    fig, ax = plt.subplots(1, 1, figsize=(14, 14))
    ax.set_facecolor('#D7CCC8')

    all_pos = np.array([v.pos for v in mesh.vertices])
    min_pos = all_pos.min(axis=0)
    max_pos = all_pos.max(axis=0)
    padding = 0.5
    ax.set_xlim(min_pos[0] - padding, max_pos[0] + padding)
    ax.set_ylim(min_pos[1] - padding, max_pos[1] + padding)

    for face in mesh.faces:
        if not face.is_quad:
            continue

        bitmask = compute_quad_bitmask(face)
        verts = np.array([v.pos for v in face.vertices])
        centroid = face.centroid
        angles = np.arctan2(verts[:, 1] - centroid[1], verts[:, 0] - centroid[0])
        sorted_verts = verts[np.argsort(angles)]

        # Color by terrain coverage
        coverage = bin(bitmask).count('1') / 4.0
        grass = np.array([0.3, 0.69, 0.31])  # #4CAF50
        dirt = np.array([0.63, 0.53, 0.50])   # #A1887F
        color = dirt * (1 - coverage) + grass * coverage

        polygon = plt.Polygon(sorted_verts, facecolor=color,
                             edgecolor='#5D4037', linewidth=0.3, alpha=1.0)
        ax.add_patch(polygon)

    ax.set_aspect('equal')
    ax.set_title('Irregular Quad Mesh - Auto-tile Visualization\n(Flat color by terrain coverage)', fontsize=14)
    ax.axis('off')

    plt.tight_layout()
    plt.savefig(output_path, dpi=200, bbox_inches='tight', facecolor='#F5F5DC')
    plt.close()
    print(f"Saved: {output_path}")


def load_transition_tiles(atlas_path: str, transition_map_path: str,
                          transition_key: str = "grass3|base_grass1",
                          tile_size: int = 16) -> Dict[int, Image.Image]:
    """
    Load the 16 auto-tile variants for a transition from the atlas.
    Returns dict mapping bitmask (0-15) to PIL Image.
    """
    import json

    atlas = Image.open(atlas_path)

    # Parse transition map (just the portion we need)
    with open(transition_map_path, 'r') as f:
        data = json.load(f)

    if transition_key not in data['transitions']:
        print(f"Warning: {transition_key} not found in transition map")
        return {}

    transition = data['transitions'][transition_key]
    variants = transition['variants']

    tiles = {}
    for bitmask in range(16):
        if bitmask < len(variants) and variants[bitmask]:
            # Take first variant
            coords = variants[bitmask][0]
            x, y = coords['x'], coords['y']

            # Extract tile from atlas
            left = x * tile_size
            top = y * tile_size
            right = left + tile_size
            bottom = top + tile_size

            tile = atlas.crop((left, top, right, bottom))
            tiles[bitmask] = tile

    print(f"   Loaded {len(tiles)} tiles for '{transition_key}'")
    return tiles


def find_perspective_coeffs(src_coords, dst_coords):
    """
    Find coefficients for perspective transform.
    src_coords: 4 source points [(x0,y0), (x1,y1), (x2,y2), (x3,y3)]
    dst_coords: 4 destination points
    Returns 8 coefficients for PIL's PERSPECTIVE transform.
    """
    matrix = []
    for s, d in zip(src_coords, dst_coords):
        matrix.append([d[0], d[1], 1, 0, 0, 0, -s[0]*d[0], -s[0]*d[1]])
        matrix.append([0, 0, 0, d[0], d[1], 1, -s[1]*d[0], -s[1]*d[1]])

    A = np.matrix(matrix, dtype=np.float64)
    B = np.array(src_coords).reshape(8)

    res = np.dot(np.linalg.inv(A.T * A) * A.T, B)
    return np.array(res).reshape(8)


def get_quad_corners_sorted(face: Face) -> List[np.ndarray]:
    """
    Get quad corners sorted as: bottom-left, bottom-right, top-right, top-left
    (SW, SE, NE, NW) for consistent UV mapping.
    """
    verts = np.array([v.pos for v in face.vertices])
    centroid = face.centroid

    corners = {}
    for v in face.vertices:
        dx = v.pos[0] - centroid[0]
        dy = v.pos[1] - centroid[1]

        # Determine quadrant
        if dx <= 0 and dy <= 0:
            corners['SW'] = v.pos.copy()
        elif dx > 0 and dy <= 0:
            corners['SE'] = v.pos.copy()
        elif dx > 0 and dy > 0:
            corners['NE'] = v.pos.copy()
        else:
            corners['NW'] = v.pos.copy()

    # If we don't have all 4 corners (degenerate quad), use angle sorting
    if len(corners) != 4:
        angles = np.arctan2(verts[:, 1] - centroid[1], verts[:, 0] - centroid[0])
        sorted_indices = np.argsort(angles)
        sorted_verts = verts[sorted_indices]
        return [sorted_verts[i] for i in range(4)]

    # Return in order: SW, SE, NE, NW (CCW from bottom-left)
    return [corners['SW'], corners['SE'], corners['NE'], corners['NW']]


def render_mesh_with_real_tiles(mesh: Mesh, output_path: str,
                                 tiles: Dict[int, Image.Image],
                                 base_tile: Optional[Image.Image] = None):
    """
    Render mesh with actual tile textures perspective-warped onto irregular quads.
    """
    from PIL import Image

    # Calculate world bounds
    all_pos = np.array([v.pos for v in mesh.vertices])
    min_pos = all_pos.min(axis=0)
    max_pos = all_pos.max(axis=0)
    padding = 0.3
    world_min = min_pos - padding
    world_max = max_pos + padding
    world_size = world_max - world_min

    # Output image size (pixels)
    scale = 100  # pixels per world unit
    img_width = int(world_size[0] * scale)
    img_height = int(world_size[1] * scale)

    # Create output image with background color
    output = Image.new('RGBA', (img_width, img_height), (141, 110, 99, 255))  # dirt brown

    def world_to_pixel(pos):
        """Convert world coordinates to pixel coordinates."""
        px = (pos[0] - world_min[0]) * scale
        py = img_height - (pos[1] - world_min[1]) * scale  # Flip Y
        return (px, py)

    # Get tile size
    tile_size = 16
    if tiles:
        sample_tile = next(iter(tiles.values()))
        tile_size = sample_tile.size[0]

    # Sort faces by some criteria to handle overlap (optional)
    quads = [f for f in mesh.faces if f.is_quad]

    for face in quads:
        bitmask = compute_quad_bitmask(face)

        if bitmask not in tiles:
            continue

        tile = tiles[bitmask].convert('RGBA')

        # Get quad corners in world space: SW, SE, NE, NW
        world_corners = get_quad_corners_sorted(face)

        # Convert to pixel coordinates
        pixel_corners = [world_to_pixel(c) for c in world_corners]

        # Source corners (tile image): top-left, top-right, bottom-right, bottom-left
        # Note: PIL image origin is top-left, so we need to map correctly
        # Tile corners: (0,0)=TL, (w,0)=TR, (w,h)=BR, (0,h)=BL
        # World corners: SW=BL, SE=BR, NE=TR, NW=TL
        # So mapping: SW->BL, SE->BR, NE->TR, NW->TL
        src_corners = [
            (0, tile_size),      # BL (maps to SW)
            (tile_size, tile_size),  # BR (maps to SE)
            (tile_size, 0),      # TR (maps to NE)
            (0, 0),              # TL (maps to NW)
        ]

        # Destination corners in pixel space (SW, SE, NE, NW order)
        dst_corners = pixel_corners

        try:
            # Calculate perspective transform coefficients
            coeffs = find_perspective_coeffs(src_corners, dst_corners)

            # Calculate bounding box of destination quad
            dst_xs = [p[0] for p in dst_corners]
            dst_ys = [p[1] for p in dst_corners]
            dst_min_x, dst_max_x = int(min(dst_xs)), int(max(dst_xs)) + 1
            dst_min_y, dst_max_y = int(min(dst_ys)), int(max(dst_ys)) + 1

            # Clamp to image bounds
            dst_min_x = max(0, dst_min_x)
            dst_min_y = max(0, dst_min_y)
            dst_max_x = min(img_width, dst_max_x)
            dst_max_y = min(img_height, dst_max_y)

            if dst_max_x <= dst_min_x or dst_max_y <= dst_min_y:
                continue

            # Size of the region we're rendering to
            region_width = dst_max_x - dst_min_x
            region_height = dst_max_y - dst_min_y

            # Adjust destination corners relative to region
            adjusted_dst = [(p[0] - dst_min_x, p[1] - dst_min_y) for p in dst_corners]

            # Recalculate coefficients for the adjusted region
            coeffs = find_perspective_coeffs(src_corners, adjusted_dst)

            # Transform the tile
            warped = tile.transform(
                (region_width, region_height),
                Image.PERSPECTIVE,
                coeffs,
                Image.BILINEAR
            )

            # Paste onto output
            output.paste(warped, (dst_min_x, dst_min_y), warped)

        except Exception as e:
            # Skip problematic quads
            pass

    # Save version with edges
    from PIL import ImageDraw
    output_with_edges = output.copy()
    draw = ImageDraw.Draw(output_with_edges)

    for face in quads:
        world_corners = get_quad_corners_sorted(face)
        pixel_corners = [world_to_pixel(c) for c in world_corners]

        # Draw quad outline
        for i in range(4):
            p1 = pixel_corners[i]
            p2 = pixel_corners[(i + 1) % 4]
            draw.line([p1, p2], fill=(50, 50, 50, 80), width=1)

    output_with_edges.save(output_path)
    print(f"Saved: {output_path}")

    # Save clean version without edges
    clean_path = output_path.replace('.png', '_clean.png')
    output.save(clean_path)
    print(f"Saved: {clean_path}")

    # Also create matplotlib version (clean, no edges)
    fig, ax = plt.subplots(1, 1, figsize=(14, 14))
    ax.imshow(output, extent=[world_min[0], world_max[0], world_min[1], world_max[1]])
    ax.set_aspect('equal')
    ax.set_title('Irregular Quad Mesh - Perspective-Warped Tiles', fontsize=14)
    ax.axis('off')
    plt.tight_layout()
    plt.savefig(output_path.replace('.png', '_mpl.png'), dpi=150, bbox_inches='tight', facecolor='white')
    plt.close()


def render_dual_grid_debug(mesh: Mesh, output_path: str):
    """
    Debug visualization showing data grid vs visual grid relationship.

    - Visual grid (quads) drawn with light edges
    - Data grid shown as Voronoi cells around vertices
    - Each Voronoi cell colored by vertex terrain value
    - Vertex positions marked with terrain state
    """
    from scipy.spatial import Voronoi, voronoi_plot_2d

    fig, ax = plt.subplots(1, 1, figsize=(14, 14))
    ax.set_facecolor('#FAFAFA')

    all_pos = np.array([v.pos for v in mesh.vertices])
    min_pos = all_pos.min(axis=0)
    max_pos = all_pos.max(axis=0)
    padding = 1.0
    ax.set_xlim(min_pos[0] - padding, max_pos[0] + padding)
    ax.set_ylim(min_pos[1] - padding, max_pos[1] + padding)

    # Collect vertex positions and terrain values
    vertex_positions = np.array([v.pos for v in mesh.vertices])
    vertex_terrain = np.array([v.terrain for v in mesh.vertices])

    # Add boundary points for Voronoi (to close off edge regions)
    center = vertex_positions.mean(axis=0)
    radius = np.max(np.linalg.norm(vertex_positions - center, axis=1)) * 2
    n_boundary = 32
    boundary_angles = np.linspace(0, 2*np.pi, n_boundary, endpoint=False)
    boundary_points = np.column_stack([
        center[0] + radius * np.cos(boundary_angles),
        center[1] + radius * np.sin(boundary_angles)
    ])

    all_points = np.vstack([vertex_positions, boundary_points])

    # Compute Voronoi
    vor = Voronoi(all_points)

    # Draw Voronoi cells (data grid) colored by terrain
    for i, (vertex, terrain) in enumerate(zip(mesh.vertices, vertex_terrain)):
        region_idx = vor.point_region[i]
        region = vor.regions[region_idx]

        if -1 in region or len(region) == 0:
            continue

        polygon_verts = [vor.vertices[j] for j in region]

        if terrain == 1:
            color = '#4CAF50'  # Green for filled
            alpha = 0.6
        else:
            color = '#BCAAA4'  # Light brown for empty
            alpha = 0.4

        poly = plt.Polygon(polygon_verts, facecolor=color,
                          edgecolor='#795548', linewidth=1.5, alpha=alpha)
        ax.add_patch(poly)

    # Draw visual grid (quads) very lightly on top
    for face in mesh.faces:
        if not face.is_quad:
            continue

        verts = np.array([v.pos for v in face.vertices])
        centroid = face.centroid
        angles = np.arctan2(verts[:, 1] - centroid[1], verts[:, 0] - centroid[0])
        sorted_verts = verts[np.argsort(angles)]

        # Light quad outline
        poly = plt.Polygon(sorted_verts, facecolor='none',
                          edgecolor='#2196F3', linewidth=0.8, alpha=0.5,
                          linestyle='--')
        ax.add_patch(poly)

        # Mark quad center
        ax.plot(centroid[0], centroid[1], 'b+', markersize=6, alpha=0.3)

    # Draw vertices (data grid points) with terrain state
    for vertex in mesh.vertices:
        if vertex.terrain == 1:
            ax.plot(vertex.pos[0], vertex.pos[1], 'o',
                   color='#1B5E20', markersize=10, markeredgecolor='white',
                   markeredgewidth=2, zorder=10)
        else:
            ax.plot(vertex.pos[0], vertex.pos[1], 'o',
                   color='#8D6E63', markersize=8, markeredgecolor='white',
                   markeredgewidth=1.5, zorder=10)

    # Draw Voronoi edges (data grid cell boundaries)
    for simplex in vor.ridge_vertices:
        if -1 not in simplex:
            p1, p2 = vor.vertices[simplex]
            ax.plot([p1[0], p2[0]], [p1[1], p2[1]],
                   color='#5D4037', linewidth=1.0, alpha=0.7)

    ax.set_aspect('equal')
    ax.set_title('Dual Grid Debug View\n'
                 'Brown/Green cells = DATA GRID (Voronoi around vertices)\n'
                 'Blue dashed lines = VISUAL GRID (quads)', fontsize=12)
    ax.axis('off')

    # Legend
    from matplotlib.patches import Patch
    from matplotlib.lines import Line2D
    legend_elements = [
        Patch(facecolor='#4CAF50', alpha=0.6, edgecolor='#795548', label='Data: Terrain=1 (filled)'),
        Patch(facecolor='#BCAAA4', alpha=0.4, edgecolor='#795548', label='Data: Terrain=0 (empty)'),
        Line2D([0], [0], color='#2196F3', linestyle='--', label='Visual grid (quads)'),
        Line2D([0], [0], marker='o', color='w', markerfacecolor='#1B5E20',
               markersize=10, label='Vertex: filled'),
        Line2D([0], [0], marker='o', color='w', markerfacecolor='#8D6E63',
               markersize=8, label='Vertex: empty'),
    ]
    ax.legend(handles=legend_elements, loc='upper right', fontsize=9)

    plt.tight_layout()
    plt.savefig(output_path, dpi=150, bbox_inches='tight', facecolor='white')
    plt.close()
    print(f"Saved: {output_path}")


def render_bitmask_debug(mesh: Mesh, output_path: str):
    """
    Debug visualization showing bitmask calculation for each quad.
    Shows which corners are classified as NE/SE/SW/NW and their terrain values.
    Uses the actual corner classification from get_quad_corner_info().
    """
    fig, ax = plt.subplots(1, 1, figsize=(16, 16))
    ax.set_facecolor('#FAFAFA')

    all_pos = np.array([v.pos for v in mesh.vertices])
    min_pos = all_pos.min(axis=0)
    max_pos = all_pos.max(axis=0)
    padding = 0.5
    ax.set_xlim(min_pos[0] - padding, max_pos[0] + padding)
    ax.set_ylim(min_pos[1] - padding, max_pos[1] + padding)

    for face in mesh.faces:
        if not face.is_quad:
            continue

        verts = np.array([v.pos for v in face.vertices])
        centroid = face.centroid

        # Draw quad
        angles = np.arctan2(verts[:, 1] - centroid[1], verts[:, 0] - centroid[0])
        sorted_verts = verts[np.argsort(angles)]

        bitmask = compute_quad_bitmask(face)

        poly = plt.Polygon(sorted_verts, facecolor='#E3F2FD',
                          edgecolor='#1565C0', linewidth=1, alpha=0.7)
        ax.add_patch(poly)

        # Show bitmask at center
        ax.text(centroid[0], centroid[1], f'{bitmask}',
               fontsize=8, ha='center', va='center', fontweight='bold',
               color='#0D47A1')

        # Get actual corner classification
        corner_info = get_quad_corner_info(face)

        for corner_name, bit, vertex in corner_info:
            dx = vertex.pos[0] - centroid[0]
            dy = vertex.pos[1] - centroid[1]

            # Color by terrain
            if vertex.terrain == 1:
                color = '#2E7D32'
                marker = 's'
            else:
                color = '#A1887F'
                marker = 'o'

            ax.plot(vertex.pos[0], vertex.pos[1], marker,
                   color=color, markersize=12, markeredgecolor='white',
                   markeredgewidth=1, zorder=10)

            # Label with corner name and bit value
            offset_x = 0.08 * np.sign(dx) if dx != 0 else 0
            offset_y = 0.08 * np.sign(dy) if dy != 0 else 0.08
            ax.text(vertex.pos[0] + offset_x, vertex.pos[1] + offset_y,
                   f'{corner_name}\n(bit {bit})', fontsize=5, ha='center', va='center',
                   color='#333')

    ax.set_aspect('equal')
    ax.set_title('Bitmask Debug: Corner Classification (Angle-based)\n'
                 'Square=filled(1), Circle=empty(0), Number=bitmask value', fontsize=12)
    ax.axis('off')

    plt.tight_layout()
    plt.savefig(output_path, dpi=150, bbox_inches='tight', facecolor='white')
    plt.close()
    print(f"Saved: {output_path}")


def render_comparison(mesh: Mesh, output_path: str):
    """Render side-by-side: mesh structure + auto-tile visualization."""
    fig, axes = plt.subplots(1, 2, figsize=(20, 10))

    # Calculate bounds for proper view
    all_pos = np.array([v.pos for v in mesh.vertices])
    min_pos = all_pos.min(axis=0)
    max_pos = all_pos.max(axis=0)
    padding = 0.5

    # Left: Mesh structure with vertices
    ax = axes[0]
    ax.set_facecolor('#F5F5DC')
    ax.set_xlim(min_pos[0] - padding, max_pos[0] + padding)
    ax.set_ylim(min_pos[1] - padding, max_pos[1] + padding)

    for face in mesh.faces:
        if not face.is_quad:
            continue

        verts = [v.pos for v in face.vertices]
        bitmask = compute_quad_bitmask(face)
        color = get_bitmask_color(bitmask)

        # Sort vertices
        centroid = face.centroid
        verts_arr = np.array(verts)
        angles = np.arctan2(verts_arr[:, 1] - centroid[1], verts_arr[:, 0] - centroid[0])
        sorted_verts = verts_arr[np.argsort(angles)]

        polygon = plt.Polygon(sorted_verts, facecolor=color, edgecolor='#333333',
                             linewidth=0.5, alpha=0.9)
        ax.add_patch(polygon)

        # Add bitmask label
        ax.text(centroid[0], centroid[1], str(bitmask), fontsize=6,
               ha='center', va='center', color='white', fontweight='bold')

    # Draw vertices
    filled = [v for v in mesh.vertices if v.terrain == 1]
    empty = [v for v in mesh.vertices if v.terrain == 0]

    if filled:
        ax.scatter([v.pos[0] for v in filled], [v.pos[1] for v in filled],
                  c='darkgreen', s=30, zorder=5, marker='o', edgecolors='white', linewidths=1)
    if empty:
        ax.scatter([v.pos[0] for v in empty], [v.pos[1] for v in empty],
                  c='tan', s=25, zorder=5, marker='o', edgecolors='#666', linewidths=0.5)

    ax.set_aspect('equal')
    ax.set_title('Data Grid (vertices = terrain data)\nNumbers show bitmask values', fontsize=12)
    ax.axis('off')

    # Right: Auto-tile visualization
    ax = axes[1]
    ax.set_facecolor('#D7CCC8')
    ax.set_xlim(min_pos[0] - padding, max_pos[0] + padding)
    ax.set_ylim(min_pos[1] - padding, max_pos[1] + padding)

    for face in mesh.faces:
        if not face.is_quad:
            continue

        bitmask = compute_quad_bitmask(face)
        verts = np.array([v.pos for v in face.vertices])
        centroid = face.centroid

        angles = np.arctan2(verts[:, 1] - centroid[1], verts[:, 0] - centroid[0])
        sorted_indices = np.argsort(angles)
        sorted_verts = verts[sorted_indices]

        # Base
        base_poly = plt.Polygon(sorted_verts, facecolor='#A1887F',
                               edgecolor='#6D4C41', linewidth=0.2, alpha=1.0)
        ax.add_patch(base_poly)

        # Terrain overlay
        if bitmask == 15:
            grass_poly = plt.Polygon(sorted_verts, facecolor='#4CAF50',
                                    edgecolor='#2E7D32', linewidth=0.3, alpha=1.0)
            ax.add_patch(grass_poly)
        elif bitmask > 0:
            corners = {}
            for i, idx in enumerate(sorted_indices):
                v = face.vertices[idx]
                dx = v.pos[0] - centroid[0]
                dy = v.pos[1] - centroid[1]

                if dx >= 0 and dy >= 0:
                    corners['NE'] = (i, v)
                elif dx < 0 and dy >= 0:
                    corners['NW'] = (i, v)
                elif dx < 0 and dy < 0:
                    corners['SW'] = (i, v)
                else:
                    corners['SE'] = (i, v)

            for corner_name, bit in [('NE', NE), ('SE', SE), ('SW', SW), ('NW', NW)]:
                if bitmask & bit:
                    if corner_name in corners:
                        idx, v = corners[corner_name]
                        prev_idx = (idx - 1) % 4
                        next_idx = (idx + 1) % 4

                        corner_pos = sorted_verts[idx]
                        mid_to_next = (sorted_verts[idx] + sorted_verts[next_idx]) / 2
                        mid_to_prev = (sorted_verts[idx] + sorted_verts[prev_idx]) / 2

                        corner_poly = plt.Polygon([corner_pos, mid_to_next, centroid, mid_to_prev],
                                                 facecolor='#4CAF50', edgecolor='none', alpha=0.95)
                        ax.add_patch(corner_poly)

    ax.set_aspect('equal')
    ax.set_title('Visual Grid (quads = auto-tiles)\nGrass transitions on dirt', fontsize=12)
    ax.axis('off')

    plt.tight_layout()
    plt.savefig(output_path, dpi=200, bbox_inches='tight',
                facecolor='white', edgecolor='none')
    plt.close()
    print(f"Saved: {output_path}")


# ============================================================================
# Main
# ============================================================================

def render_mesh_geometry(mesh: Mesh, output_path: str, title: str, show_vertices: bool = True):
    """Render just the mesh geometry - faces, edges, vertices."""
    fig, ax = plt.subplots(1, 1, figsize=(10, 10))
    ax.set_facecolor('#FAFAFA')

    # Calculate bounds
    all_pos = np.array([v.pos for v in mesh.vertices])
    min_pos = all_pos.min(axis=0)
    max_pos = all_pos.max(axis=0)
    padding = 0.5
    ax.set_xlim(min_pos[0] - padding, max_pos[0] + padding)
    ax.set_ylim(min_pos[1] - padding, max_pos[1] + padding)

    # Color faces by type
    for face in mesh.faces:
        verts = np.array([v.pos for v in face.vertices])
        centroid = face.centroid
        angles = np.arctan2(verts[:, 1] - centroid[1], verts[:, 0] - centroid[0])
        sorted_verts = verts[np.argsort(angles)]

        if face.is_triangle:
            color = '#FFCDD2'  # Light red for triangles
            edge_color = '#C62828'
        else:
            color = '#C8E6C9'  # Light green for quads
            edge_color = '#2E7D32'

        polygon = plt.Polygon(sorted_verts, facecolor=color, edgecolor=edge_color,
                             linewidth=1.0, alpha=0.8)
        ax.add_patch(polygon)

    # Draw vertices
    if show_vertices:
        ax.scatter([v.pos[0] for v in mesh.vertices], [v.pos[1] for v in mesh.vertices],
                  c='#333333', s=25, zorder=5, marker='o', edgecolors='white', linewidths=0.5)

    # Stats
    num_tris = sum(1 for f in mesh.faces if f.is_triangle)
    num_quads = sum(1 for f in mesh.faces if f.is_quad)
    stats = f"Vertices: {len(mesh.vertices)} | Triangles: {num_tris} | Quads: {num_quads}"

    ax.set_aspect('equal')
    ax.set_title(f"{title}\n{stats}", fontsize=12)
    ax.axis('off')

    # Legend
    from matplotlib.patches import Patch
    legend_elements = [
        Patch(facecolor='#FFCDD2', edgecolor='#C62828', label='Triangle'),
        Patch(facecolor='#C8E6C9', edgecolor='#2E7D32', label='Quad'),
    ]
    ax.legend(handles=legend_elements, loc='upper right')

    plt.tight_layout()
    plt.savefig(output_path, dpi=150, bbox_inches='tight', facecolor='white')
    plt.close()
    print(f"   Saved: {output_path}")


def render_all_steps(output_dir: str):
    """Generate images for each step of the algorithm."""
    import copy
    import time

    print("=" * 60)
    print("Irregular Quad Mesh - Step-by-Step Visualization")
    print("=" * 60)

    seed = int(time.time() * 1000) % (2**32)
    random.seed(seed)
    print(f"   Random seed: {seed}")

    # Step 1: Initial triangle mesh
    print("\n1. Generating hexagonal grid as connected triangle mesh...")
    mesh = generate_hex_grid(rings=10, radius=1.0)
    render_mesh_geometry(mesh, os.path.join(output_dir, "step1_triangles.png"),
                        "Step 1: Initial Triangle Mesh (Hex Centers)")

    # Step 2: After merging
    print("\n2. Merging adjacent triangles into quads...")
    mesh = merge_adjacent_triangles(mesh, merge_probability=0.7)
    num_tris = sum(1 for f in mesh.faces if f.is_triangle)
    num_quads = sum(1 for f in mesh.faces if f.is_quad)
    print(f"   Result: {num_quads} quads, {num_tris} remaining triangles")
    render_mesh_geometry(mesh, os.path.join(output_dir, "step2_merged.png"),
                        "Step 2: After Triangle→Quad Merging")

    # Step 3: After subdivision
    print("\n3. Subdividing all faces...")
    mesh = subdivide_all_faces(mesh)
    num_quads = sum(1 for f in mesh.faces if f.is_quad)
    print(f"   Result: {num_quads} quads, {len(mesh.vertices)} vertices")
    render_mesh_geometry(mesh, os.path.join(output_dir, "step3_subdivided.png"),
                        "Step 3: After Subdivision (Quads→4, Tris→3)")

    # Step 4: After relaxation
    print("\n4. Running Lloyd relaxation (15 iterations, boundary pinned)...")
    mesh = lloyd_relaxation(mesh, iterations=15, pin_boundary=True)
    print("   Relaxation complete")
    render_mesh_geometry(mesh, os.path.join(output_dir, "step4_relaxed.png"),
                        "Step 4: After Lloyd Relaxation")

    # Step 5: With terrain
    print("\n5. Assigning terrain data...")
    assign_terrain_islands(mesh, num_islands=14)
    filled = sum(1 for v in mesh.vertices if v.terrain == 1)
    print(f"   {filled}/{len(mesh.vertices)} vertices filled")

    # Final renders
    print("\n6. Rendering final auto-tile visualization...")
    render_mesh_with_autotiles(mesh, os.path.join(output_dir, "step5_autotile.png"))
    render_comparison(mesh, os.path.join(output_dir, "step6_comparison.png"))

    # Debug renders
    # print("\n6b. Rendering dual-grid debug visualization...")
    # render_dual_grid_debug(mesh, os.path.join(output_dir, "debug_dual_grid.png"))
    # render_bitmask_debug(mesh, os.path.join(output_dir, "debug_bitmask.png"))

    # Step 7: Load and render real tiles
    print("\n7. Loading real tile textures...")
    # output_dir is Scripts/Tools, go up to project root (cardcleaner)
    project_root = os.path.dirname(os.path.dirname(output_dir))
    atlas_path = os.path.join(project_root, "Data/CompiledAtlas/terrain_atlas.png")
    transition_map_path = os.path.join(project_root, "Data/CompiledAtlas/transition_map.json")
    print(f"   Looking for atlas at: {atlas_path}")

    if os.path.exists(atlas_path) and os.path.exists(transition_map_path):
        tiles = load_transition_tiles(atlas_path, transition_map_path,
                                      transition_key="fdr_sand2|rocks1",
                                      tile_size=16)
        if tiles:
            print("\n8. Rendering with real tile textures...")
            render_mesh_with_real_tiles(mesh, os.path.join(output_dir, "step7_real_tiles.png"), tiles)
    else:
        print(f"   Atlas not found at {atlas_path}")

    print("\n" + "=" * 60)
    print("COMPLETE! Generated step-by-step images:")
    for i, name in enumerate([
        "step1_triangles.png",
        "step2_merged.png",
        "step3_subdivided.png",
        "step4_relaxed.png",
        "step5_autotile.png",
        "step6_comparison.png",
        "step7_real_tiles.png"
    ], 1):
        path = os.path.join(output_dir, name)
        if os.path.exists(path):
            print(f"  {i}. {path}")
    print("=" * 60)

    return mesh


def main():
    output_dir = os.path.dirname(os.path.abspath(__file__))
    render_all_steps(output_dir)


if __name__ == "__main__":
    main()
