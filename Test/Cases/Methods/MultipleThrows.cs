namespace Test.Cases.Methods;

public class MultipleThrows
{
    [Throws(
        typeof(NullReferenceException),
        typeof(ArgumentException))]
    public void ProcessData1()
    {
        var fetcher = new DataFetcher2();

        fetcher.FetchData();
    }

    [Throws(typeof(NullReferenceException))]
    public void ProcessData2()
    {
        var fetcher = new DataFetcher2();

        fetcher.FetchData();
    }

    [Throws(typeof(Exception))]
    public void ProcessData3()
    {
        var fetcher = new DataFetcher2();

        fetcher.FetchData();
    }

    [Throws(typeof(ArgumentException))]
    public void ProcessData4()
    {
        var fetcher = new DataFetcher2();

        fetcher.FetchData();
    }

    public void ProcessData5()
    {
        var fetcher = new DataFetcher2();

        try
        {
            fetcher.FetchData();
        }
        catch (ArgumentException ex)
        {
            Console.WriteLine("Handled exception: " + ex.Message);
        }
        catch (NullReferenceException ex)
        {
            Console.WriteLine("Handled exception: " + ex.Message);
        }
    }

    [Throws(typeof(IOException))]
    public void ProcessData6()
    {
        var fetcher = new DataFetcher2();

        try
        {
            fetcher.FetchData();
        }
        catch (ArgumentException ex)
        {
            Console.WriteLine("Handled exception: " + ex.Message);
        }
    }
}