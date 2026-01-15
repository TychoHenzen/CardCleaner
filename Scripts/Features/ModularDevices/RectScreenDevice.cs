using System.Linq;
using Godot;

namespace CardCleaner.Scripts.Features.ModularDevices;

/// <summary>
/// Device wrapper for SimpleWorldMapScreen (rectangular tile-based map).
/// Receives card signatures and trigger to initialize the screen.
/// </summary>
public partial class RectScreenDevice : ScreenDeviceBase
{
    [Export] public SimpleWorldMapScreen? Screen { get; set; }

    protected override bool ValidateScreen()
    {
        if (Screen != null) return true;

        GD.PrintErr("RectScreenDevice: No screen assigned!");
        return false;
    }

    protected override bool ValidateInputs()
    {
        if (CurrentMapSeeds.Signatures.Count != 0) return true;

        GD.PrintErr("RectScreenDevice: No map seeds received!");
        return false;
    }

    protected override void InitializeScreen()
    {
        Screen!.Initialize(
            CurrentMapSeeds.Signatures.ToArray(),
            CurrentAbilities.Signatures.ToArray()
        );
    }

    protected override void ResetScreen()
    {
        Screen?.ResetToInitialState();
    }
}
