using System;
using System.Collections.Generic;
using System.Linq;
using CardCleaner.Scripts.Core.Devices;
using CardCleaner.Scripts.Features.Card.Models;
using Godot;

namespace CardCleaner.Scripts.Features.ModularDevices;

/// <summary>
/// Base class for screen device wrappers that receive card signatures and a trigger to initialize.
/// Subclasses only need to implement screen initialization and reset logic.
/// </summary>
public abstract partial class ScreenDeviceBase : Node3D, IDevice
{
    private readonly List<IJack> _jacks = [];
    private readonly Jack<CardSignatureList> _mapSeedInput;
    private readonly Jack<CardSignatureList> _abilityInput;
    private readonly Jack<Trigger> _triggerInput;
    private readonly Jack<Trigger> _clearOutput;

    protected CardSignatureList CurrentMapSeeds { get; private set; } = CardSignatureList.Empty;
    protected CardSignatureList CurrentAbilities { get; private set; } = CardSignatureList.Empty;
    protected RandomNumberGenerator Rng { get; } = new();

    public string UniqueId { get; set; } = Guid.NewGuid().ToString();
    public Node3D WorldNode => this;
    public IReadOnlyList<IJack> Jacks => _jacks;

    public IInputJack<CardSignatureList> MapSeedInput => _mapSeedInput;
    public IInputJack<CardSignatureList> AbilityInput => _abilityInput;
    public IInputJack<Trigger> TriggerInput => _triggerInput;
    public IOutputJack<Trigger> ClearOutput => _clearOutput;

    protected ScreenDeviceBase()
    {
        _mapSeedInput = new Jack<CardSignatureList>("MapSeedInput", JackDirection.Input);
        _abilityInput = new Jack<CardSignatureList>("AbilityInput", JackDirection.Input);
        _triggerInput = new Jack<Trigger>("TriggerInput", JackDirection.Input);
        _clearOutput = new Jack<Trigger>("ClearOutput", JackDirection.Output);

        _mapSeedInput.DataReceived += sigs => CurrentMapSeeds = sigs;
        _abilityInput.DataReceived += sigs => CurrentAbilities = sigs;
        _triggerInput.DataReceived += _ => OnTriggerReceived();

        _jacks.Add(_mapSeedInput);
        _jacks.Add(_abilityInput);
        _jacks.Add(_triggerInput);
        _jacks.Add(_clearOutput);
    }

    public IJack? GetJack(string name) => _jacks.FirstOrDefault(j => j.Name == name);

    private void OnTriggerReceived()
    {
        if (!ValidateScreen())
            return;

        if (!ValidateInputs())
            return;

        InitializeScreen();

        _clearOutput.Emit(Trigger.Instance);
    }

    /// <summary>
    /// Validates the screen is assigned. Logs error and returns false if not.
    /// </summary>
    protected abstract bool ValidateScreen();

    /// <summary>
    /// Validates required inputs before initialization. Default returns true.
    /// Override to add validation (e.g., require map seeds).
    /// </summary>
    protected virtual bool ValidateInputs() => true;

    /// <summary>
    /// Initialize the screen with current card signatures.
    /// </summary>
    protected abstract void InitializeScreen();

    /// <summary>
    /// Resets the screen to initial state.
    /// </summary>
    public virtual void Reset()
    {
        CurrentMapSeeds = CardSignatureList.Empty;
        CurrentAbilities = CardSignatureList.Empty;
        ResetScreen();
    }

    /// <summary>
    /// Reset the screen implementation. Override if the screen supports reset.
    /// </summary>
    protected virtual void ResetScreen() { }

    /// <summary>
    /// Generates a deterministic seed from card signatures.
    /// </summary>
    protected static int GenerateSeedFromSignatures(IReadOnlyList<CardSignature> signatures)
    {
        var hash = 17;
        foreach (var sig in signatures)
        {
            foreach (var element in sig.Elements)
            {
                hash = hash * 31 + element.GetHashCode();
            }
        }
        return hash;
    }
}
