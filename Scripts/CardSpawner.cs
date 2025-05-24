using Godot;

/// <summary>
/// Spawns a specified number of Card instances at runtime when pressing 1, 2, or 3.
/// Attach this script to a Node3D (e.g., "CardSpawner") in your scene.
/// </summary>
public partial class CardSpawner : Node3D
{
    [Export]
    public PackedScene CardScene { get; set; }

    [Export]
    public NodePath SpawnParentPath { get; set; }
    
    [Export]
    public Vector3 OffsetRange { get; set; } = Vector3.Zero;


    private Node3D _spawnParent;

    public override void _Ready()
    {
        // Cache the parent node where cards should be added
        _spawnParent = GetNode<Node3D>(SpawnParentPath);
    }

    public override void _Input(InputEvent @event)
    {
        if (@event is InputEventKey keyEvent && keyEvent.Pressed && !keyEvent.Echo)
        {
            switch (keyEvent.Keycode)
            {
                case Key.Key1:
                    SpawnCards(1);
                    break;
                case Key.Key2:
                    SpawnCards(10);
                    break;
                case Key.Key3:
                    SpawnCards(100);
                    break;
            }
        }
    }

    private void SpawnCards(int count)
    {
        for (int i = 0; i < count; i++)
        {
            var cardInstance = CardScene.Instantiate() as Node3D;
            _spawnParent.AddChild(cardInstance);

            // Compute a random offset within the specified range
            var randomOffset = new Vector3(
                (GD.Randf() * 2 - 1) * OffsetRange.X,
                (GD.Randf() * 2 - 1) * OffsetRange.Y,
                (GD.Randf() * 2 - 1) * OffsetRange.Z
            );

            // Apply offset relative to the spawn parent's global transform
            var spawnTransform = _spawnParent.GlobalTransform;
            spawnTransform.Origin += randomOffset;
            if (cardInstance != null) cardInstance.GlobalTransform = spawnTransform;
        }

        GD.Print($"Spawned {count} card(s) with random offset up to {OffsetRange}.");
    }
}