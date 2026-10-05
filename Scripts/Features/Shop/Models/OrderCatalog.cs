using Godot;

namespace CardCleaner.Scripts.Features.Shop.Models;

/// <summary>
///     A list of orderable items. Each ordering terminal takes its own catalog, so a second terminal
///     (for example the workshop terminal) can offer different items without code changes.
/// </summary>
[GlobalClass]
public partial class OrderCatalog : Resource
{
    [Export]
    public OrderItem[] Items { get; set; } = [];
}
