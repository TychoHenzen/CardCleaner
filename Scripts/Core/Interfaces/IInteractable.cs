using Godot;

namespace CardCleaner.Scripts.Core.Interfaces;

/// <summary>
/// Interface for any object that can be interacted with in the world.
/// </summary>
public interface IInteractable
{
    /// <summary>
    /// Whether this object can currently be interacted with.
    /// </summary>
    bool CanInteract { get; }
    
    /// <summary>
    /// The collision body for detection (RigidBody3D, StaticBody3D, etc.)
    /// </summary>
    Node3D InteractionBody { get; }
    
    /// <summary>
    /// Called when the player interacts with this object.
    /// </summary>
    void Interact();
    
    /// <summary>
    /// Called when the object should be highlighted (player looking at it).
    /// </summary>
    void Highlight();
    
    /// <summary>
    /// Called when the object should stop being highlighted.
    /// </summary>
    void ClearHighlight();
    
    /// <summary>
    /// Maximum distance from which this object can be interacted with.
    /// </summary>
    float InteractionRange { get; }
}