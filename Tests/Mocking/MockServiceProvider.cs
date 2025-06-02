using CardCleaner.Scripts.Core.Interfaces;
using Godot;

namespace CardCleaner.Tests.Mocking;

public partial class MockServiceProvider : Node, IServiceProvider
{
    public bool RegisterServicesCalled { get; private set; }
    
    public void RegisterServices(IServiceContainer container)
    {
        RegisterServicesCalled = true;
        // Register a test service
        container.RegisterSingleton<IAsyncTestService, AsyncTestServiceImpl>();
    }
}