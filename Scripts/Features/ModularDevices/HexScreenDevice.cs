using System.Linq;
using CardCleaner.Scripts.Features.Worldgen.IrregularMesh;
using Godot;

namespace CardCleaner.Scripts.Features.ModularDevices;

/// <summary>
/// Device wrapper for IrregularWorldMapScreen (hex-based irregular mesh map).
/// Receives card signatures and trigger to initialize the screen.
/// </summary>
public partial class HexScreenDevice : ScreenDeviceBase
{
    [Export] public IrregularWorldMapScreen? Screen { get; set; }

    protected override bool ValidateScreen()
    {
        if (Screen != null) return true;

        GD.PrintErr("HexScreenDevice: No screen assigned!");
        return false;
    }

    protected override void InitializeScreen()
    {
        var seed = CurrentMapSeeds.Signatures.Count > 0
            ? GenerateSeedFromSignatures(CurrentMapSeeds.Signatures)
            : (int)Rng.Randi();

        Screen!.GenerateMap(
            seed,
            CurrentMapSeeds.Signatures.ToArray(),
            CurrentAbilities.Signatures.ToArray()
        );
    }
}
