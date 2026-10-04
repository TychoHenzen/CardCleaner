using CardCleaner.Scripts.Core.Interfaces;
using CardCleaner.Tests.Mocking;
using Godot;
using IServiceProvider = CardCleaner.Scripts.Core.Interfaces.IServiceProvider;

namespace CardCleaner.Tests.Core.DependencyInjection;

// Keep a simple test-specific service provider
public partial class TestServiceProvider : Node, IServiceProvider
{
    public bool RegisterServicesCalled { get; private set; }

    public void RegisterServices(IServiceContainer container)
    {
        RegisterServicesCalled = true;
        container.RegisterSingleton<IAsyncTestService, AsyncTestServiceImpl>();
    }
}
