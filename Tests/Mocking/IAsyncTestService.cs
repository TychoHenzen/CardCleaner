namespace CardCleaner.Tests.Mocking;

public interface IAsyncTestService
{
    string GetData();
}
public class AsyncTestServiceImpl : IAsyncTestService
{
    public string GetData() => "async_test_data";
}