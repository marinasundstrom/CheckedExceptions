namespace Test;

public class DataFetcher
{
    [Throws(typeof(NullReferenceException))]
    public void FetchData()
    {
        // Simulate code that may throw a NullReferenceException
        throw new NullReferenceException("Data source is null.");
    }
}
