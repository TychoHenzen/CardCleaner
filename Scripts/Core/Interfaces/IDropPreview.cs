using Godot;

namespace CardCleaner.Scripts.Core.Interfaces;

public interface IDropPreview
{
    void ShowPreview(bool show);
    void UpdatePreview(Vector3 origin, Vector3 direction);
}