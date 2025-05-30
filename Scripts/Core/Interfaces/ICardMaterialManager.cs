using Godot;

namespace CardCleaner.Scripts.Interfaces;

public interface ICardMaterialComponent
{
    void SetLayerTextures(LayerData[] layers);
    void ApplyMaterial(MeshInstance3D target);
    void SetGemEmission(int index, Color color, float strength);
}