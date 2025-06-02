using Godot;

namespace CardCleaner.Scripts.Core.Interfaces;

public interface ICardMaterialComponent
{
    void SetLayerTextures(Data.LayerData[] layers);
    ShaderMaterial? ApplyMaterial(MeshInstance3D target);
    void SetGemEmission(int index, Color color, float strength);
}