using CardCleaner.Scripts.Features.Card.Models;
using Godot;

namespace CardCleaner.Scripts.Core.Services;

/// <summary>
/// One persisted card read back from save data, ready to be spawned again.
/// </summary>
internal sealed record SavedCardRecord(
    CardSignature Signature,
    Vector3 Position,
    Vector3 Rotation,
    bool IsHeld,
    string ParentPath);
