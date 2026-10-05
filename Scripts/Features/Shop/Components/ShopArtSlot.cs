using Godot;

namespace CardCleaner.Scripts.Features.Shop.Components;

/// <summary>
///     Loads one licensed pack mesh at runtime and hides its graybox placeholder.
///     The pack files are gitignored (see tools/sync-assets.ps1), so a missing or unimported
///     file leaves the placeholder visible instead of breaking the scene.
/// </summary>
public partial class ShopArtSlot : Node3D
{
    private const float DefaultArtScale = 1.0f;

    /// <summary>Resource path of the imported pack mesh, for example an .fbx under Assets/Synty.</summary>
    [Export(PropertyHint.File, "*.fbx,*.tscn")]
    public string ArtPath { get; set; } = string.Empty;

    /// <summary>Uniform scale applied to the imported mesh (the pack FBX files import very small).</summary>
    [Export]
    public float ArtScale { get; set; } = DefaultArtScale;

    /// <summary>Graybox stand-in that is hidden once the pack mesh is loaded.</summary>
    [Export]
    public Node3D? Placeholder { get; set; }

    /// <summary>True when the pack mesh was found and added under this slot.</summary>
    public bool ArtLoaded { get; private set; }

    public override void _Ready()
    {
        ArtLoaded = TryLoadArt();
        if (ArtLoaded && Placeholder != null)
            Placeholder.Visible = false;
    }

    private bool TryLoadArt()
    {
        if (string.IsNullOrEmpty(ArtPath) || !ResourceLoader.Exists(ArtPath))
            return false;

        var scene = ResourceLoader.Load<PackedScene>(ArtPath);
        if (scene == null)
            return false;

        var art = scene.Instantiate<Node3D>();
        art.Name = "Art";
        art.Scale = Vector3.One * ArtScale;
        AddChild(art);
        return true;
    }
}
