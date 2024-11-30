namespace Test.Cases.Methods;

public class DataFetcher2
{
    [Throws(
        typeof(NullReferenceException),
        typeof(ArgumentException))]
    public void FetchData()
    {
        throw new NullReferenceException("Data source is null.");
    }
}