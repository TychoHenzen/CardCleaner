namespace CardCleaner.Tests.Core;

public class TestServiceWithDependency : ITestService
{
    private readonly ITestService _dependency;
    
    public TestServiceWithDependency(ITestService dependency)
    {
        _dependency = dependency;
    }
    
    public string GetValue() => $"wrapped_{_dependency.GetValue()}";
}