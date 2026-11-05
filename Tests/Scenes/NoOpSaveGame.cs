using CardCleaner.Scripts.Features.SaveSlots.Services;

namespace CardCleaner.Tests.Scenes;

/// <summary>Stands in for the slot writer so a day close writes no file during a scene test.</summary>
internal sealed class NoOpSaveGame : ISaveGameCommand
{
    public bool ManualSave() => true;
}
