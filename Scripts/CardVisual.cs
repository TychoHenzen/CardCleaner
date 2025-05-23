using Godot;

[Tool]
public partial class CardVisual : MeshInstance3D {
    [Export] public Texture2D FrontArt;
    [Export] public Texture2D Border;
    [Export] public Texture2D SetSymbol;
    [Export] public Texture2D BackDesign;
    [Export] public Texture2D EmissiveMap;
    [Export] public bool IsShiny = false;
    [Export] public Vector4 SetSymbolUV = new Vector4(0.8f, 0.8f, 0.15f, 0.15f);

    private ShaderMaterial _shaderMat;

    public override void _Ready() {
        _shaderMat = MaterialOverride as ShaderMaterial;
        if (_shaderMat == null) {
            GD.PrintErr("CardVisual: MaterialOverride must be a ShaderMaterial with Card.shader.");
            return;
        }
        UpdateShaderParams();
    }

    public override void _Process(double delta) {
        if (Engine.IsEditorHint()) {
            UpdateShaderParams();
        }
    }

    private void UpdateShaderParams() {
        _shaderMat.SetShaderParameter("textureFrontArt",  FrontArt);
        _shaderMat.SetShaderParameter("textureBorder",    Border);
        _shaderMat.SetShaderParameter("textureSetSymbol", SetSymbol);
        _shaderMat.SetShaderParameter("textureBackDesign",BackDesign);
        _shaderMat.SetShaderParameter("textureEmissive",  EmissiveMap);
        _shaderMat.SetShaderParameter("is_shiny",         IsShiny);
        _shaderMat.SetShaderParameter("set_symbol_uv",    SetSymbolUV);
    }
}