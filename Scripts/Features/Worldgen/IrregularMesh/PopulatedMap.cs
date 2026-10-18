using System;
using CardCleaner.Scripts.Core.Interfaces;
using CardCleaner.Scripts.Core.Services;

namespace CardCleaner.Scripts.Features.Worldgen.IrregularMesh;

/// <summary>The map data of an irregular mesh, its placement and the callback that removes a defeated enemy.</summary>
internal readonly record struct PopulatedMap(IMapData Map, MapPlacement Placement, Func<int, bool> RemoveEnemy);
