namespace Test.Cases.Methods;

public class SingleThrow
{
    public void ProcessData1()
    {
        var fetcher = new DataFetcher();

        fetcher.FetchData();
    }

    [Throws(typeof(NullReferenceException))]
    public void ProcessData2()
    {
        var fetcher = new DataFetcher();

        fetcher.FetchData();
    }

    public void ProcessData3()
    {
        var fetcher = new DataFetcher();

        try
        {
            fetcher.FetchData();
        }
        catch (NullReferenceException ex)
        {
            Console.WriteLine("Handled exception: " + ex.Message);
        }
    }

    [Throws(typeof(Exception))]
    public void ProcessData4()
    {
        var fetcher = new DataFetcher();

        fetcher.FetchData();
    }
}