namespace Test;

public class DataFetcher3
{
    [Throws(typeof(InvalidOperationException))]
    public DataFetcher3()
    {
        throw new InvalidOperationException("Constructor exception.");
    }
}

public class Example
{
    public void ProcessData()
    {
        var fetcher = new DataFetcher3();
    }

    public void ProcessData2()
    {
        try
        {
            var fetcher = new DataFetcher3();
        }
        catch (Exception ex)
        {
            Console.WriteLine("Handled exception: " + ex.Message);
        }
    }
}