#if TOOLS
using Godot;

namespace CardCleaner.Addons.TileEditor;

/// <summary>
/// The hovered cell, the selected cell and the selection block size shown by the atlas picker.
/// </summary>
internal readonly record struct AtlasPickerSelection(Vector2I Hovered, Vector2I Selected, Vector2I Size);
#endif
