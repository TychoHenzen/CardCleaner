namespace CardCleaner.Tests.Mocking;

public interface IAsyncTestService
{
    string GetData();
}

public class AsyncTestServiceImpl : IAsyncTestService
{
    public string GetData()
    {
        return "async_test_data";
    }
}