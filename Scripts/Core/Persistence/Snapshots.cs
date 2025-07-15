using System.Collections.Generic;
using CardCleaner.Scripts.Features.Card.Models;
using Godot;

[System.Serializable]
public class WorldSnapshot
{
    public List<CardInWorld> WorldCards { get; set; } = new();
    public PlayerState Player { get; set; } = new();
    public DeckbuilderState Deckbuilder { get; set; } = new();
    public GameSessionState Session { get; set; } = new();
    public long Timestamp { get; set; }
}

[System.Serializable]
public class CardInWorld
{
    public CardSignature Signature { get; set; }
    public Vector3 Position { get; set; }
    public Vector3 Rotation { get; set; }
    public bool IsHeld { get; set; }
}

[System.Serializable]
public class PlayerState
{
    public Vector3 Position { get; set; }
    public Vector3 Rotation { get; set; }
    public List<CardSignature> HeldCards { get; set; } = new();
}

[System.Serializable]
public class DeckbuilderState
{
    public List<CardSignature> AbilitySlotCards { get; set; } = new();
    public CardSignature? MapSeedCard { get; set; }
}

[System.Serializable]
public class GameSessionState
{
    public string CurrentState { get; set; } = "WaitingForCards";
    public long SessionStartTime { get; set; }
}