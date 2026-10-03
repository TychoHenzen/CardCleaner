using CardCleaner.Scripts.Core.Interfaces;
using CardCleaner.Scripts.Features.Card.Models;
using CardCleaner.Scripts.Features.Deckbuilder.Components;
using CardCleaner.Scripts.Features.Deckbuilder.Models;
using Godot;

/// <summary>
/// Enhanced world map screen with visual feedback for exploration and combat.
/// Renders 2D tile-based maps via SubViewport onto a 3D mesh for world-space display.
/// The Godot-facing surface lives here; the session and rendering runtime is <see cref="WorldMapRuntime"/>.
/// </summary>
[GlobalClass]
public partial class SimpleWorldMapScreen : Node3D
{
    // Default values as constants
    private const bool DefaultShowBiomeOverlay = true;

    private WorldMapRuntime? _runtime;

    #region Export Properties

    [Export] public TileMapLayer? TerrainLayer { get; set; }
    [Export] public TileMapLayer? DecorationLayer { get; set; }
    [Export] public TileMapLayer? StructureLayer { get; set; }
    [Export] public TileMapLayer? EffectLayer { get; set; }
    [Export] public TileMapLayer? OverlayLayer { get; set; }
    [Export] public TileMapLayer? BiomeOverlayLayer { get; set; }
    [Export] public SubViewport? Viewport { get; set; }
    [Export] public MeshInstance3D? ScreenMesh { get; set; }
    [Export] public Label? StatusLabel { get; set; }
    [Export] public Sprite2D? PlayerSprite { get; set; }
    [Export] public Control? CombatUI { get; set; }
    [Export] public bool ShowBiomeOverlay { get; set; } = DefaultShowBiomeOverlay;

    #endregion

    #region Property Revert Support

    public override bool _PropertyCanRevert(StringName property)
    {
        return property.ToString() switch
        {
            nameof(ShowBiomeOverlay) => true,
            _ => base._PropertyCanRevert(property)
        };
    }

    public override Variant _PropertyGetRevert(StringName property)
    {
        return property.ToString() switch
        {
            nameof(ShowBiomeOverlay) => DefaultShowBiomeOverlay,
            _ => base._PropertyGetRevert(property)
        };
    }

    #endregion

    #region Lifecycle

    public override void _Ready()
    {
        _runtime = new WorldMapRuntime(this);
        CallDeferred(nameof(SetupScreenMesh));
        CallDeferred(nameof(SetupScreenMaterial));
        _runtime.Start();
    }

    public override void _ExitTree()
    {
        _runtime?.Stop();
    }

    #endregion

    #region Public API

    public void Initialize(CardSignature[] mapSeed, CardSignature[] abilities)
    {
        if (mapSeed.Length == 0)
        {
            ILog.Error("Initialize called with empty mapSeed array");
            return;
        }

        ILog.Print(
            $"Initializing simple map screen with {mapSeed.Length} seed card(s) " +
            $"and {abilities.Length} abilities");

        _runtime?.StartSession(mapSeed, abilities);
    }

    /// <summary>
    /// Updates the biome distribution preview based on the given card signatures.
    /// Called by DeckBuilderController when MapCardSlot cards change.
    /// </summary>
    public void UpdateBiomePreview(CardSignature[] cards) => _runtime?.UpdateBiomePreview(cards);

    /// <summary>
    /// Resets the map screen to its initial state.
    /// </summary>
    public void ResetToInitialState()
    {
        ILog.Print("Resetting SimpleWorldMapScreen to initial state");
        _runtime?.Reset();
        ILog.Print("SimpleWorldMapScreen reset complete");
    }

    #endregion

    #region Deferred Calls

    private void SetupScreenMesh()
    {
        if (ScreenMesh == null) return;
        ViewportScreenSetup.SetupScreenMesh(ScreenMesh);
    }

    private void SetupScreenMaterial()
    {
        if (Viewport == null || ScreenMesh == null) return;
        ViewportScreenSetup.SetupScreenMaterial(Viewport, ScreenMesh);
    }

    private void ConfigureCamera() => _runtime?.ConfigureCamera();

    private void EndCombat(bool playerWon) => _runtime?.EndCombat(playerWon);

    #endregion
}
