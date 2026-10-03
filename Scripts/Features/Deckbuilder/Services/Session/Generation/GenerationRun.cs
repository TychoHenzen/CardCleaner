using System.Threading;

namespace CardCleaner.Scripts.Features.Deckbuilder.Services.Session.Generation;

/// <summary>
/// Identifies one map generation attempt and the token that cancels it.
/// </summary>
internal sealed record GenerationRun(long GenerationId, CancellationTokenSource Cts);
