namespace CardCleaner.Tests.Core;

public interface IAsyncTestService
{
    string GetData();
}
public class AsyncTestServiceImpl : IAsyncTestService
{
    public string GetData() => "async_test_data";
}