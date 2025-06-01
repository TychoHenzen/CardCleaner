namespace CardCleaner.Tests.Core;

public class AsyncTestServiceImpl : IAsyncTestService
{
    public string GetData() => "async_test_data";
}