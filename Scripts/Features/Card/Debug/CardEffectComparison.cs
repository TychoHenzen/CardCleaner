using System;
using CardCleaner.Scripts.Core.DependencyInjection;
using CardCleaner.Scripts.Core.Enumeration;
using CardCleaner.Scripts.Core.Interfaces;
using CardCleaner.Scripts.Features.Card.Models;
using Godot;

namespace CardCleaner.Scripts.Features.Card.Debug;

/// <summary>
///     Lays out one real card for every rarity (rows, common at the top) and intensity tier (columns, dormant to
///     intense), each with a label below it, so the art-region effects can be compared side by side. The cards come
///     from the normal spawning service, so they take the same path as cards in the shop. Run
///     <c>Scenes/Debug/CardEffectComparison.tscn</c> with F6; it is not the main scene.
/// </summary>
public partial class CardEffectComparison : Node3D
{
    private const float ColumnSpacing = 0.42f;
    private const float RowSpacing = 0.64f;
    private const float GridCenterHeight = 1.6f;
    private const float CardHalfHeight = 0.225f;
    private const float LabelGap = 0.02f;
    private const float LabelPixelSize = 0.001f;
    private const int LabelFontSize = 64;

    // The cards stand upright facing the camera, which looks down the -Z axis.
    private static readonly Basis StandingCard = Basis.FromEuler(new Vector3(Mathf.Pi / 2f, 0f, 0f));

    [Export] public Node3D CardParent { get; set; } = null!;
    [Export] public Node3D LabelParent { get; set; } = null!;

    public override void _Ready()
    {
        ServiceLocator.Get<ICardSpawningService>(SpawnGrid);
    }

    private void SpawnGrid(ICardSpawningService spawner)
    {
        var rarities = Enum.GetValues<CardRarity>();
        var tiers = Enum.GetValues<IntensityTier>();

        foreach (var rarity in rarities)
        foreach (var tier in tiers)
        {
            var center = new Vector3(
                ((int)tier - (tiers.Length - 1) / 2f) * ColumnSpacing,
                GridCenterHeight + ((rarities.Length - 1) / 2f - (int)rarity) * RowSpacing,
                0f);

            SpawnCard(spawner, rarity, tier, center);
            AddLabel(rarity, tier, center);
        }
    }

    private void SpawnCard(ICardSpawningService spawner, CardRarity rarity, IntensityTier tier, Vector3 center)
    {
        var signature = CardEffectComparisonGrid.SignatureFor(rarity, tier);
        var card = spawner.SpawnCard(signature, new Transform3D(StandingCard, center), CardParent);
        if (card == null) return;

        card.Name = CellName(rarity, tier);
        if (card is not RigidBody3D body) return;

        // Hold the card where it is laid out instead of letting it fall.
        body.FreezeMode = RigidBody3D.FreezeModeEnum.Static;
        body.Freeze = true;
    }

    private void AddLabel(CardRarity rarity, IntensityTier tier, Vector3 center)
    {
        var label = new Label3D
        {
            Name = CellName(rarity, tier),
            Text = $"{rarity}\n{tier}",
            FontSize = LabelFontSize,
            PixelSize = LabelPixelSize,
            Shaded = false,
            NoDepthTest = true,
            VerticalAlignment = VerticalAlignment.Top,
            Position = center + new Vector3(0f, -(CardHalfHeight + LabelGap), 0f)
        };
        LabelParent.AddChild(label);
    }

    private static string CellName(CardRarity rarity, IntensityTier tier)
    {
        return $"{rarity}_{tier}";
    }
}
