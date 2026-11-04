using System;
using System.Collections.Generic;
using CardCleaner.Scripts.Core.Interfaces;
using CardCleaner.Scripts.Features.Shop.Models;
using Godot;

namespace CardCleaner.Scripts.Features.Shop.Ui;

/// <summary>
///     The catalog half of the Stock app: one card per catalog item in a scrolling grid. A card shows a
///     thumbnail (or a placeholder tile), the name, the price and an Add button that reads Level or Unavailable
///     while the item is locked.
/// </summary>
internal sealed class StockCardGrid
{
    private const int CardColumns = 3;
    private const int CardWidth = 170;
    private const int TileHeight = 96;

    private readonly GridContainer _grid = new() { Columns = CardColumns };
    private readonly List<CardView> _cards = [];
    private readonly Label _empty = new() { Visible = false };

    internal int Count => _cards.Count;

    /// <summary>The line shown in place of the cards while there is nothing to order, or empty.</summary>
    internal string EmptyText => _empty.Text;

    /// <summary>Adds the scrolling grid to <paramref name="column" />.</summary>
    public void AddTo(VBoxContainer column)
    {
        var scroll = new ScrollContainer
        {
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled
        };
        var content = new VBoxContainer();
        content.AddChild(_empty);
        content.AddChild(_grid);
        scroll.AddChild(content);
        column.AddChild(scroll);
    }

    /// <summary>
    ///     Replaces the cards with one per item. <paramref name="entryFor" /> describes an item and
    ///     <paramref name="onAdd" /> runs when an unlocked card's Add button is pressed.
    /// </summary>
    public void Rebuild(
        IEnumerable<OrderItem> items, Func<OrderItem, StockEntry> entryFor, int level, Action<StockEntry> onAdd)
    {
        // Free() detaches and frees at once; no card button raises a signal that rebuilds the cards.
        foreach (var card in _cards)
            card.Panel.Free();
        _cards.Clear();

        var any = false;
        foreach (var item in items)
        {
            AddCard(item, entryFor(item), level, onAdd);
            any = true;
        }

        _empty.Text = any ? string.Empty : "Nothing to order.";
        _empty.Visible = !any;
    }

    /// <summary>Presses the n-th card's button, as a click would.</summary>
    public void PressAdd(int index) => _cards[index].Button.EmitSignal(BaseButton.SignalName.Pressed);

    public bool IsLocked(int index) => _cards[index].Button.Disabled;

    public string LockText(int index) => _cards[index].Button.Text;

    public bool HasPlaceholder(int index) => _cards[index].Placeholder;

    private void AddCard(OrderItem item, StockEntry entry, int level, Action<StockEntry> onAdd)
    {
        var panel = new PanelContainer { CustomMinimumSize = new Vector2(CardWidth, 0) };
        var column = new VBoxContainer();
        panel.AddChild(column);
        column.AddChild(BuildThumbnail(item));
        column.AddChild(new Label { Text = item.DisplayName });
        column.AddChild(new Label { Text = $"{item.Price}" });
        var button = BuildButton(item, entry, level, onAdd);
        column.AddChild(button);

        _grid.AddChild(panel);
        _cards.Add(new CardView(panel, button, item.Thumbnail == null));
    }

    private static Button BuildButton(OrderItem item, StockEntry entry, int level, Action<StockEntry> onAdd)
    {
        if (!IsSellable(item))
        {
            ILog.Warning($"Stock item '{item.Id}' has no scene or no positive price and is shown unavailable.");
            return new Button { Text = LockLabel(null), Disabled = true };
        }

        if (entry.IsLocked(level))
            return new Button { Text = LockLabel(entry.RequiredLevel), Disabled = true };

        var button = new Button { Text = "Add" };
        button.Pressed += () => onAdd(entry);
        return button;
    }

    private static bool IsSellable(OrderItem item) => item.Scene != null && item.Price > 0;

    private static string LockLabel(int? requiredLevel) =>
        requiredLevel is { } level ? $"Level {level}" : "Unavailable";

    private static Control BuildThumbnail(OrderItem item)
    {
        if (item.Thumbnail != null)
        {
            return new TextureRect
            {
                Texture = item.Thumbnail,
                ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
                CustomMinimumSize = new Vector2(0, TileHeight)
            };
        }

        var tile = new ColorRect
        {
            Color = new Color(0.25f, 0.3f, 0.4f),
            CustomMinimumSize = new Vector2(0, TileHeight)
        };
        var name = item.DisplayName ?? string.Empty;
        var initial = name.Length == 0 ? "?" : name[..1].ToUpperInvariant();
        var label = new Label
        {
            Text = initial,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };
        label.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        tile.AddChild(label);
        return tile;
    }

    private readonly record struct CardView(PanelContainer Panel, Button Button, bool Placeholder);
}
