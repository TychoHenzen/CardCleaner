using System;
using System.Collections.Generic;
using System.Globalization;
using CardCleaner.Scripts.Features.SaveSlots.Models;
using Godot;

namespace CardCleaner.Scripts.Features.Menu.Controllers;

/// <summary>
///     Lists the save slots as rows of name, saved time, a Load button and a Delete button. A slot that cannot be
///     read is tagged Unreadable and its Load button is disabled; its Delete button stays enabled. The menu handles
///     both requests.
/// </summary>
public partial class SlotListController : VBoxContainer
{
    public event Action<SaveSlotInfo>? LoadRequested;

    public event Action<SaveSlotInfo>? DeleteRequested;

    /// <summary>Replaces the rows with one row per slot, in the order given.</summary>
    public void Show(IReadOnlyList<SaveSlotInfo> slots)
    {
        foreach (Node child in GetChildren())
        {
            RemoveChild(child);
            child.Free();
        }

        for (var index = 0; index < slots.Count; index++)
            AddChild(BuildRow(slots[index], index));
    }

    private HBoxContainer BuildRow(SaveSlotInfo info, int index)
    {
        var row = new HBoxContainer { Name = $"Row{index}" };

        var label = new Label
        {
            Name = "Label",
            Text = Describe(info),
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
        };

        var load = new Button { Name = "Load", Text = "Load", Disabled = !info.IsReadable };
        load.Pressed += () => LoadRequested?.Invoke(info);

        var delete = new Button { Name = "Delete", Text = "Delete" };
        delete.Pressed += () => DeleteRequested?.Invoke(info);

        row.AddChild(label);
        row.AddChild(load);
        row.AddChild(delete);
        return row;
    }

    private static string Describe(SaveSlotInfo info)
    {
        var saved = info.SavedAtUtc is { } utc
            ? utc.ToLocalTime().ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture)
            : "unknown";
        var tag = info.IsReadable ? string.Empty : "  Unreadable";
        return $"{info.Name.Value}  {saved}{tag}";
    }
}
