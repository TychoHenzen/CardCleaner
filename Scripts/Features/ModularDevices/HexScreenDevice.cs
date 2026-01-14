using System;
using System.Collections.Generic;
using System.Linq;
using CardCleaner.Scripts.Core.Devices;
using CardCleaner.Scripts.Features.Card.Models;
using CardCleaner.Scripts.Features.Worldgen.IrregularMesh;
using Godot;

namespace CardCleaner.Scripts.Features.ModularDevices;

/// <summary>
/// Device wrapper for IrregularWorldMapScreen (hex-based irregular mesh map).
/// Receives card signatures and trigger to initialize the screen.
/// </summary>
public partial class HexScreenDevice : Node3D, IDevice
{
    private readonly List<IJack> _jacks = [];
    private readonly Jack<CardSignatureList> _mapSeedInput;
    private readonly Jack<CardSignatureList> _abilityInput;
    private readonly Jack<Trigger> _triggerInput;
    private readonly Jack<Trigger> _clearOutput;

    private CardSignatureList _currentMapSeeds = CardSignatureList.Empty;
    private CardSignatureList _currentAbilities = CardSignatureList.Empty;
    private readonly RandomNumberGenerator _rng = new();

    [Export] public IrregularWorldMapScreen? Screen { get; set; }

    public string UniqueId { get; set; } = Guid.NewGuid().ToString();
    public Node3D WorldNode => this;
    public IReadOnlyList<IJack> Jacks => _jacks;

    public IInputJack<CardSignatureList> MapSeedInput => _mapSeedInput;
    public IInputJack<CardSignatureList> AbilityInput => _abilityInput;
    public IInputJack<Trigger> TriggerInput => _triggerInput;
    public IOutputJack<Trigger> ClearOutput => _clearOutput;

    public HexScreenDevice()
    {
        _mapSeedInput = new Jack<CardSignatureList>("MapSeedInput", JackDirection.Input);
        _abilityInput = new Jack<CardSignatureList>("AbilityInput", JackDirection.Input);
        _triggerInput = new Jack<Trigger>("TriggerInput", JackDirection.Input);
        _clearOutput = new Jack<Trigger>("ClearOutput", JackDirection.Output);

        _mapSeedInput.DataReceived += sigs => _currentMapSeeds = sigs;
        _abilityInput.DataReceived += sigs => _currentAbilities = sigs;
        _triggerInput.DataReceived += _ => OnTriggerReceived();

        _jacks.Add(_mapSeedInput);
        _jacks.Add(_abilityInput);
        _jacks.Add(_triggerInput);
        _jacks.Add(_clearOutput);
    }

    public IJack? GetJack(string name) => _jacks.FirstOrDefault(j => j.Name == name);

    private void OnTriggerReceived()
    {
        if (Screen == null)
        {
            GD.PrintErr("HexScreenDevice: No screen assigned!");
            return;
        }

        // Generate seed from card signatures or use random
        var seed = _currentMapSeeds.Signatures.Count > 0
            ? GenerateSeedFromSignatures(_currentMapSeeds.Signatures)
            : (int)_rng.Randi();

        // Generate the map
        Screen.GenerateMap(
            seed,
            _currentMapSeeds.Signatures.ToArray(),
            _currentAbilities.Signatures.ToArray()
        );

        // Signal card slots to clear their cards
        _clearOutput.Emit(Trigger.Instance);
    }

    private static int GenerateSeedFromSignatures(IReadOnlyList<CardSignature> signatures)
    {
        // Combine signature elements into a seed
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

    /// <summary>
    /// Resets the screen to initial state.
    /// </summary>
    public void Reset()
    {
        _currentMapSeeds = CardSignatureList.Empty;
        _currentAbilities = CardSignatureList.Empty;
        // IrregularWorldMapScreen doesn't have a Reset method yet
    }
}
