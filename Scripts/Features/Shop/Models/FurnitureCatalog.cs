using System;
using Godot;

namespace CardCleaner.Scripts.Features.Shop.Models;

/// <summary>The furniture the shop knows about, looked up by item id.</summary>
[GlobalClass]
public partial class FurnitureCatalog : Resource
{
    [Export]
    public FurnitureItem[] Items { get; set; } = [];

    /// <summary>The first item with this id (ordinal comparison), or null when none has it.</summary>
    public FurnitureItem? Find(string id)
    {
        foreach (var item in Items)
        {
            if (string.Equals(item.Id, id, StringComparison.Ordinal))
                return item;
        }

        return null;
    }
}
