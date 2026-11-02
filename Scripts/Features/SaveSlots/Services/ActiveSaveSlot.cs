using System;
using CardCleaner.Scripts.Core.DependencyInjection;
using CardCleaner.Scripts.Features.SaveSlots.Models;

namespace CardCleaner.Scripts.Features.SaveSlots.Services;

[Service(typeof(IActiveSaveSlot))]
internal sealed class ActiveSaveSlot : IActiveSaveSlot
{
    private static readonly SaveSlotName DefaultName = CreateDefaultName();

    private SaveSlotName _current = DefaultName;

    public SaveSlotName Current => _current;

    public event Action<SaveSlotName>? Changed;

    public void Activate(SaveSlotName name)
    {
        _current = name;
        Changed?.Invoke(name);
    }

    private static SaveSlotName CreateDefaultName()
    {
        SaveSlotName.TryCreate("default", out var name, out _);
        return name;
    }
}
