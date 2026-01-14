using System;
using System.Collections.Generic;
using System.Linq;
using CardCleaner.Scripts.Core.Devices;
using CardCleaner.Scripts.Features.Card.Models;
using Godot;

namespace CardCleaner.Scripts.Features.ModularDevices;

public partial class CardSlotDevice : Node3D, IDevice
{
    private readonly List<IJack> _jacks = [];
    private readonly Jack<CardSignatureList> _cardOutput;
    private readonly Jack<CardSignatureList> _chainInput;
    private readonly Jack<Trigger> _clearInput;

    private CardSignature? _heldCard;
    private CardSignatureList _chainedSignatures = CardSignatureList.Empty;

    public string UniqueId { get; set; } = Guid.NewGuid().ToString();
    public Node3D WorldNode => this;
    public IReadOnlyList<IJack> Jacks => _jacks;

    public IOutputJack<CardSignatureList> CardOutput => _cardOutput;
    public IInputJack<CardSignatureList> ChainInput => _chainInput;
    public IInputJack<Trigger> ClearInput => _clearInput;

    public CardSignature? HeldCard => _heldCard;
    public bool HasCard => _heldCard != null;

    public CardSlotDevice()
    {
        _cardOutput = new Jack<CardSignatureList>("CardOutput", JackDirection.Output);
        _chainInput = new Jack<CardSignatureList>("ChainInput", JackDirection.Input);
        _clearInput = new Jack<Trigger>("ClearInput", JackDirection.Input);

        _chainInput.DataReceived += OnChainReceived;
        _clearInput.DataReceived += _ => ClearCard();

        _jacks.Add(_cardOutput);
        _jacks.Add(_chainInput);
        _jacks.Add(_clearInput);
    }

    public IJack? GetJack(string name) => _jacks.FirstOrDefault(j => j.Name == name);

    public void InsertCard(CardSignature card)
    {
        _heldCard = card;
        EmitSignatures();
    }

    public void ClearCard()
    {
        _heldCard = null;
        EmitSignatures();
    }

    private void OnChainReceived(CardSignatureList signatures)
    {
        _chainedSignatures = signatures;
        EmitSignatures();
    }

    private void EmitSignatures()
    {
        var signatures = _chainedSignatures.Signatures.ToList();
        if (_heldCard != null)
            signatures.Add(_heldCard);
        _cardOutput.Emit(new CardSignatureList(signatures));
    }
}
