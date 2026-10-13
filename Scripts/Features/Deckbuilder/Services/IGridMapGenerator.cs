using CardCleaner.Scripts.Core.Interfaces;

namespace CardCleaner.Scripts.Features.Deckbuilder.Services;

/// <summary>
/// The grid map's generator. The composition root registers it; the cabinet resolves it for the grid kind and hands
/// it to the session.
/// </summary>
public interface IGridMapGenerator : IMapGenerator
{
}
