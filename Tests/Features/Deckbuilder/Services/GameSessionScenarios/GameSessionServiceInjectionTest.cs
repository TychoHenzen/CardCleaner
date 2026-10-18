using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using CardCleaner.Scripts.Core.Interfaces;
using CardCleaner.Scripts.Features.Card.Models;
using CardCleaner.Scripts.Features.Deckbuilder.Services;
using CardCleaner.Tests.TestUtilities;

namespace CardCleaner.Tests.Features.Deckbuilder.Services.GameSessionScenarios;

/// <summary>
///     GameSessionService builds its parts when the root injects it and answers safe defaults before that.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class GameSessionServiceInjectionTest
{
    [TestCase]
    public void BeforeInjectionTheServiceIsWaitingAndStartsNothing()
    {
        var service = new GameSessionService();
        try
        {
            AssertThat(service.CurrentState).IsEqual(SessionState.WaitingForCards);
            AssertThat(service.CurrentMapData).IsNull();

            var seeds = new List<CardSignature> { new CardSignature() };
            service.StartSession(seeds, seeds, new NoGenerator());

            AssertThat(service.CurrentState).IsEqual(SessionState.WaitingForCards);
        }
        finally
        {
            service.Free();
        }
    }

    [TestCase]
    public void InjectingAgainKeepsTheLiveSession()
    {
        var service = new GameSessionService();
        try
        {
            TestServices.Inject(service);
            var events = 0;
            service.StateChanged += _ => events++;

            TestServices.Inject(service);

            AssertThat(service.CurrentState).IsEqual(SessionState.WaitingForCards);
            AssertThat(events).IsEqual(0);
        }
        finally
        {
            service.Free();
        }
    }

    private sealed class NoGenerator : IMapGenerator
    {
        public Task<IGeneratedMap> GenerateAsync(
            MapGenerationConfig config,
            IProgress<float>? progress = null,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
}
