using System;
using CardCleaner.Scripts.Core.DependencyInjection;
using CardCleaner.Tests.Mocking;
using GdUnit4;

namespace CardCleaner.Tests.Core.DependencyInjection;

// Test interfaces for DI testing

[TestSuite]
public class ServiceContainerTest
{
    private ServiceContainer _container = null!;

    [BeforeTest]
    public void Setup()
    {
        _container = new ServiceContainer();
    }

    [TestCase]
    public void TestRegisterAndResolveSingleton()
    {
        var instance = new TestServiceImpl();
        _container.RegisterSingleton<ITestService>(instance);

        var resolved = _container.Resolve<ITestService>();

        Assertions.AssertThat(resolved).IsEqual(instance);
        Assertions.AssertThat(resolved.GetValue()).IsEqual("test_value");
    }

    [TestCase]
    public void TestRegisterSingletonWithGenericTypes()
    {
        _container.RegisterSingleton<ITestService, TestServiceImpl>();

        var resolved1 = _container.Resolve<ITestService>();
        var resolved2 = _container.Resolve<ITestService>();

        Assertions.AssertThat(resolved1).IsEqual(resolved2); // Same instance
        Assertions.AssertThat(resolved1.GetValue()).IsEqual("test_value");
    }

    [TestCase]
    public void TestIsRegistered()
    {
        Assertions.AssertBool(_container.IsRegistered<ITestService>()).IsFalse();

        _container.RegisterSingleton<ITestService, TestServiceImpl>();

        Assertions.AssertBool(_container.IsRegistered<ITestService>()).IsTrue();
        Assertions.AssertBool(_container.IsRegistered(typeof(ITestService))).IsTrue();
    }

    [TestCase]
    public void TestResolveUnregisteredServiceThrows()
    {
        Assertions.AssertThrown(() => _container.Resolve<ITestService>())
            .IsInstanceOf<InvalidOperationException>()
            .HasMessage("Service ITestService not registered");
    }
}