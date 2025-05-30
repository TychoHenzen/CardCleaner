using Godot;

namespace CardCleaner.Scripts.Core.Interfaces;

public interface ICardMaterialComponent
{
    void SetLayerTextures(Data.LayerData[] layers);
    void ApplyMaterial(MeshInstance3D target);
    void SetGemEmission(int index, Color color, float strength);
}