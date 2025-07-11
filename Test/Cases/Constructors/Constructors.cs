namespace Test.Cases.Constructors;

public class DataFetcher3
{
    [Throws(typeof(InvalidOperationException))]
    public DataFetcher3()
    {
        throw new InvalidOperationException("Constructor exception.");
    }
}

public class DataFetcher3_2
{
    [Throws(typeof(InvalidOperationException))]
    public void Foo()
    {
        throw new InvalidOperationException();
    }

    [Throws(typeof(InvalidOperationException))]
    public DataFetcher3_2()
    {
        Foo();
    }
}

public class Example
{
    public void ProcessData()
    {
        var fetcher = new DataFetcher3();
    }

    [Throws(typeof(InvalidOperationException))]
    public void ProcessData1()
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

    public void ProcessData3()
    {
        DataFetcher3 fetcher = new();
    }

    public void ProcessData4()
    {
        try
        {
            DataFetcher3 fetcher = new();
        }
        catch (Exception ex)
        {
            Console.WriteLine("Handled exception: " + ex.Message);
        }
    }
}