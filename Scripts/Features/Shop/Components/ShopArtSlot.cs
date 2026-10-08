using System.Collections.Generic;
using System.Linq;
using CardCleaner.Scripts.Features.Shop.Models;
using Godot;

namespace CardCleaner.Scripts.Features.Shop.Components;

/// <summary>
///     Loads one licensed pack mesh at runtime and hides its graybox placeholder.
///     The pack files live in the private Assets submodule (see tools/sync-assets.ps1), so a missing or
///     unimported file leaves the placeholder visible instead of breaking the scene.
///     The pack FBX files carry no usable texture and are single-sided slabs, so a mesh from a known pack
///     gets that pack's colour atlas on a double-sided material (see <see cref="PackMaterialCatalog" />).
///     The script is a [Tool] so the editor viewport shows the real art instead of the placeholder. The
///     placeholder's Visible flag is an ordinary property that Godot would save into the scene as false, so it is
///     restored around every editor save (the loaded Art child has no owner and is never saved).
/// </summary>
[Tool]
public partial class ShopArtSlot : Node3D
{
    private const float DefaultArtScale = 1.0f;
    private const float PackMaterialRoughness = 0.9f;
    private const string ArtNodeName = "Art";

    // One material per atlas, shared by every slot of that pack.
    private static readonly Dictionary<string, StandardMaterial3D> PackMaterials = [];

    private string _artPath = string.Empty;
    private float _artScale = DefaultArtScale;

    /// <summary>Resource path of the imported pack mesh, for example an .fbx under Assets/Synty.</summary>
    [Export(PropertyHint.File, "*.fbx,*.tscn")]
    public string ArtPath
    {
        get => _artPath;
        set
        {
            _artPath = value;
            ReloadInEditor();
        }
    }

    /// <summary>Uniform scale applied to the imported mesh (the pack FBX files import very small).</summary>
    [Export]
    public float ArtScale
    {
        get => _artScale;
        set
        {
            _artScale = value;
            ReloadInEditor();
        }
    }

    /// <summary>Graybox stand-in that is hidden once the pack mesh is loaded.</summary>
    [Export]
    public Node3D? Placeholder { get; set; }

    /// <summary>True when the pack mesh was found and added under this slot.</summary>
    public bool ArtLoaded { get; private set; }

    /// <summary>True when the mesh also got its pack's colour atlas on a double-sided material.</summary>
    public bool ArtTextured { get; private set; }

    public override void _Ready()
    {
        ShowArt();
    }

    public override void _Notification(int what)
    {
        // Only the editor sends these. Saving with the placeholder hidden would store visible = false in the scene.
        if (what == NotificationEditorPreSave)
            SetPlaceholderVisible(true);
        else if (what == NotificationEditorPostSave)
            SetPlaceholderVisible(!ArtLoaded);
    }

    private void ShowArt()
    {
        ArtLoaded = TryLoadArt();
        SetPlaceholderVisible(!ArtLoaded);
    }

    // Inspector edits of ArtPath or ArtScale refresh the preview. While a scene loads, Godot sets the exports
    // before the node is in the tree, so nothing reloads then and _Ready does the first load.
    private void ReloadInEditor()
    {
        if (!Engine.IsEditorHint() || !IsInsideTree())
            return;

        if (GetNodeOrNull(ArtNodeName) is { } old)
        {
            RemoveChild(old);
            old.QueueFree();
        }

        ArtTextured = false;
        ShowArt();
    }

    private void SetPlaceholderVisible(bool visible)
    {
        if (Placeholder != null)
            Placeholder.Visible = visible;
    }

    private static StandardMaterial3D? PackMaterial(string atlasPath)
    {
        if (PackMaterials.TryGetValue(atlasPath, out var cached) && IsInstanceValid(cached))
            return cached;

        if (!ResourceLoader.Exists(atlasPath) || ResourceLoader.Load<Texture2D>(atlasPath) is not { } atlas)
            return null;

        var material = new StandardMaterial3D
        {
            AlbedoTexture = atlas,
            CullMode = BaseMaterial3D.CullModeEnum.Disabled,
            TextureFilter = BaseMaterial3D.TextureFilterEnum.NearestWithMipmaps,
            Roughness = PackMaterialRoughness
        };
        PackMaterials[atlasPath] = material;
        return material;
    }

    private bool ApplyPackMaterial(Node3D art)
    {
        var atlasPath = PackMaterialCatalog.AtlasPathFor(ArtPath);
        if (atlasPath == null || PackMaterial(atlasPath) is not { } material)
            return false;

        var textured = false;
        var meshes = art.FindChildren("*", nameof(MeshInstance3D), true, false).OfType<MeshInstance3D>();
        foreach (var mesh in art is MeshInstance3D root ? meshes.Append(root) : meshes)
        {
            mesh.MaterialOverride = material;
            textured = true;
        }
        return textured;
    }

    private bool TryLoadArt()
    {
        if (string.IsNullOrEmpty(ArtPath) || !ResourceLoader.Exists(ArtPath))
            return false;

        var scene = ResourceLoader.Load<PackedScene>(ArtPath);
        if (scene == null)
            return false;

        var art = scene.Instantiate<Node3D>();
        art.Name = ArtNodeName;
        art.Scale = Vector3.One * ArtScale;
        ArtTextured = ApplyPackMaterial(art);
        AddChild(art);
        return true;
    }
}
