namespace CardCleaner.Tests.Mocking;

public interface ITestService
{
    string GetValue();
}

public class TestServiceImpl : ITestService
{
    public string GetValue()
    {
        return "test_value";
    }
}

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