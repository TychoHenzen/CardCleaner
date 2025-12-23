using CardCleaner.Scripts.Features.Deckbuilder.Services;
using Godot;

namespace CardCleaner.Scripts.Core.Interfaces;

public interface IVisibilityChecker
{
    bool CanSee(Vector2I from, Vector2I to, SimpleMapData mapData);
}
