#if TOOLS
using Godot;

namespace CardCleaner.Addons.TileEditor;

internal readonly record struct VariantPreviewInfo(
    Vector2I AtlasCoords,
    Vector2I Size,
    Vector2I Offset,
    bool IsValid);
#endif
