using System;
using System.Collections.Generic;
using System.Linq;
using CardCleaner.Scripts.Core.DependencyInjection;
using CardCleaner.Scripts.Core.Enumeration;
using CardCleaner.Scripts.Core.Interfaces;
using CardCleaner.Scripts.Features.Card.Models;
using Godot;
using CardCleaner.Scripts.Features.Card.Models.Effects;

namespace CardCleaner.Scripts.Features.Card.Debug;

/// <summary>
///     Lays out one real card for every rarity (columns, common on the left) and intensity tier (rows, dormant at the
///     top), each with a label below it, and frames them in the window whatever its size, so the art-region effects
///     can be compared side by side. The cards come from the normal spawning service, so they take the same path as
///     cards in the shop. Run <c>Scenes/Debug/CardEffectComparison.tscn</c> with F6; it is not the main scene.
///     <para>
///         To watch the cost of the effects, key 2 adds 10 cards and key 3 adds 100 (rows below the grid, cycling
///         through every cell), key 0 removes them, and key E switches every card's effects off and on. The readout
///         shows the frame time and the GPU time of the view.
///     </para>
/// </summary>
public partial class CardEffectComparison : Node3D
{
    private const float LabelPixelSize = 0.001f;
    private const int LabelFontSize = 44;
    private const double ReadoutInterval = 0.5;

    // The cards stand upright facing the camera, which looks down the -Z axis.
    private static readonly Basis StandingCard = Basis.FromEuler(new Vector3(Mathf.Pi / 2f, 0f, 0f));

    private readonly Dictionary<ShaderMaterial, SavedEffects> _switchedOff = new();
    private ICardSpawningService? _spawner;
    private int _batchCount;
    private double _readoutElapsed;
    private int _readoutFrames;

    [Export] public Node3D CardParent { get; set; } = null!;
    [Export] public Node3D LabelParent { get; set; } = null!;
    [Export] public Node3D BatchParent { get; set; } = null!;
    [Export] public Camera3D Camera { get; set; } = null!;
    [Export] public Label? Readout { get; set; }

    public bool EffectsEnabled { get; private set; } = true;

    public override void _Ready()
    {
        GetViewport().SizeChanged += FitCamera;
        RenderingServer.ViewportSetMeasureRenderTime(GetViewport().GetViewportRid(), true);
        FitCamera();
        ServiceLocator.Get<ICardSpawningService>(SpawnGrid);
    }

    public override void _ExitTree()
    {
        GetViewport().SizeChanged -= FitCamera;
    }

    public override void _UnhandledKeyInput(InputEvent @event)
    {
        if (@event is not InputEventKey { Pressed: true, Echo: false } key) return;

        switch (key.Keycode)
        {
            case Key.Key2: SpawnBatch(10); break;
            case Key.Key3: SpawnBatch(100); break;
            case Key.Key0: ClearBatch(); break;
            case Key.E: SetEffectsEnabled(!EffectsEnabled); break;
            default: return;
        }

        GetViewport().SetInputAsHandled();
    }

    public override void _Process(double delta)
    {
        if (Readout == null) return;

        _readoutElapsed += delta;
        _readoutFrames++;
        if (_readoutElapsed < ReadoutInterval) return;

        var frameMs = _readoutElapsed * 1000.0 / _readoutFrames;
        var gpuMs = RenderingServer.ViewportGetMeasuredRenderTimeGpu(GetViewport().GetViewportRid());
        Readout.Text = $"{CardParent.GetChildCount() + _batchCount} cards, effects {(EffectsEnabled ? "on" : "off")}\n"
                       + $"frame {frameMs:F1} ms (capped by vsync), GPU {gpuMs:F2} ms\n"
                       + "2: +10 cards   3: +100 cards   0: remove them   E: effects on/off";
        _readoutElapsed = 0;
        _readoutFrames = 0;
    }

    /// <summary>Adds <paramref name="count" /> frozen cards in rows below the grid, with their effects on.</summary>
    public void SpawnBatch(int count)
    {
        if (_spawner == null) return;

        SetEffectsEnabled(true);
        for (var i = 0; i < count; i++, _batchCount++)
            SpawnFrozen(CardEffectComparisonLayout.BatchSignature(_batchCount),
                CardEffectComparisonLayout.BatchCenter(_batchCount), BatchParent, $"Batch{_batchCount}");
        FitCamera();
    }

    public void ClearBatch()
    {
        foreach (var card in BatchParent.GetChildren())
            card.QueueFree();
        _batchCount = 0;
        FitCamera();
    }

    /// <summary>Switches the art effects of every card off, or back to what each card was given.</summary>
    public void SetEffectsEnabled(bool enabled)
    {
        if (enabled == EffectsEnabled) return;

        EffectsEnabled = enabled;
        if (enabled)
        {
            foreach (var (material, saved) in _switchedOff)
            {
                material.SetShaderParameter("rarity_effect", saved.Rarity);
                material.SetShaderParameter("condition_effect", saved.Condition);
            }

            _switchedOff.Clear();
            return;
        }

        foreach (var material in CardMaterials())
        {
            _switchedOff[material] = new SavedEffects(
                (int)material.GetShaderParameter("rarity_effect"),
                (int)material.GetShaderParameter("condition_effect"));
            material.SetShaderParameter("rarity_effect", (int)RarityEffect.None);
            material.SetShaderParameter("condition_effect", (int)ConditionEffect.None);
        }
    }

    private IEnumerable<ShaderMaterial> CardMaterials()
    {
        return CardParent.GetChildren().Concat(BatchParent.GetChildren())
            .Select(card => card.GetNodeOrNull<MeshInstance3D>("OuterBox_Baked")?.MaterialOverride)
            .OfType<ShaderMaterial>();
    }

    private void FitCamera()
    {
        var size = GetViewport().GetVisibleRect().Size;
        if (size.Y <= 0f) return;

        var bounds = CardEffectComparisonLayout.Bounds(_batchCount);
        var distance = CardEffectComparisonLayout.CameraDistance(bounds.Size, Camera.Fov, size.X / size.Y);
        var center = bounds.GetCenter();
        Camera.Transform = new Transform3D(Basis.Identity, new Vector3(center.X, center.Y, distance));
    }

    private void SpawnGrid(ICardSpawningService spawner)
    {
        _spawner = spawner;
        foreach (var rarity in Enum.GetValues<CardRarity>())
        foreach (var tier in Enum.GetValues<IntensityTier>())
        {
            var center = CardEffectComparisonLayout.CellCenter(rarity, tier);
            SpawnFrozen(CardEffectComparisonGrid.SignatureFor(rarity, tier), center, CardParent,
                CellName(rarity, tier));
            AddLabel(rarity, tier, center);
        }
    }

    private void SpawnFrozen(CardSignature signature, Vector2 center, Node3D parent, string name)
    {
        var place = GlobalTransform * new Transform3D(StandingCard, new Vector3(center.X, center.Y, 0f));
        var card = _spawner!.SpawnCard(signature, place, parent);
        if (card == null) return;

        card.Name = name;
        if (card is not RigidBody3D body) return;

        // Hold the card where it is laid out instead of letting it fall.
        body.FreezeMode = RigidBody3D.FreezeModeEnum.Static;
        body.Freeze = true;
    }

    private void AddLabel(CardRarity rarity, IntensityTier tier, Vector2 center)
    {
        var area = CardEffectComparisonLayout.LabelArea(center);
        var label = new Label3D
        {
            Name = CellName(rarity, tier),
            Text = $"{rarity}\n{tier}",
            FontSize = LabelFontSize,
            PixelSize = LabelPixelSize,
            Shaded = false,
            NoDepthTest = true,
            VerticalAlignment = VerticalAlignment.Top,
            Position = new Vector3(center.X, area.End.Y, 0f)
        };
        LabelParent.AddChild(label);
    }

    private static string CellName(CardRarity rarity, IntensityTier tier)
    {
        return $"{rarity}_{tier}";
    }

    private readonly record struct SavedEffects(int Rarity, int Condition);
}
