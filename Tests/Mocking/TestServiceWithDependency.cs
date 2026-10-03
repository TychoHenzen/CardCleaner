namespace CardCleaner.Tests.Mocking;

public class TestServiceWithDependency : ITestService
{
    private readonly ITestService _dependency;

    public TestServiceWithDependency(ITestService dependency)
    {
        _dependency = dependency;
    }

    public string GetValue()
    {
        return $"wrapped_{_dependency.GetValue()}";
    }
}
