namespace CardCleaner.Scripts.Features.SaveSlots.Models;

/// <summary>
///     Where each slot keeps its files. A slot is one folder under <see cref="Root" />, named by its folder key.
/// </summary>
public static class SaveSlotLayout
{
    public const string Root = "user://saves";

    public static string FolderFor(SaveSlotName name) => $"{Root}/{name.Folder}";

    public static string FileFor(SaveSlotName name) => $"{FolderFor(name)}/save.json";

    public static string TempFor(SaveSlotName name) => $"{FolderFor(name)}/save.json.tmp";
}
