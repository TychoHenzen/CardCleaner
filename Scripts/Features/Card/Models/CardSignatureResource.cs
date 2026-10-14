using Godot;

namespace CardCleaner.Scripts.Features.Card.Models;

[Tool]
[GlobalClass]
public partial class CardSignatureResource : Resource
{
    [Export(PropertyHint.Range, "-1,1,0.01")] public float Solidum { get; set; }
    [Export(PropertyHint.Range, "-1,1,0.01")] public float Febris { get; set; }
    [Export(PropertyHint.Range, "-1,1,0.01")] public float Ordinem { get; set; }
    [Export(PropertyHint.Range, "-1,1,0.01")] public float Lumines { get; set; }
    [Export(PropertyHint.Range, "-1,1,0.01")] public float Varias { get; set; }
    [Export(PropertyHint.Range, "-1,1,0.01")] public float Inertiae { get; set; }
    [Export(PropertyHint.Range, "-1,1,0.01")] public float Subsidium { get; set; }
    [Export(PropertyHint.Range, "-1,1,0.01")] public float Spatium { get; set; }

    public CardSignature ToSignature() => new(
        Solidum,
        Febris,
        Ordinem,
        Lumines,
        Varias,
        Inertiae,
        Subsidium,
        Spatium);
}
