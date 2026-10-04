namespace CardCleaner.Tests.Mocking;

public class AsyncTestServiceImpl : IAsyncTestService
{
    public string GetData()
    {
        return "async_test_data";
    }
}
