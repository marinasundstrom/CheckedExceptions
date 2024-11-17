namespace Test.Cases;

public class AsyncTest
{
    [Throws(typeof(NullReferenceException))]
    public Task FooAsync()
    {
        throw new NullReferenceException("Data source is null.");
    }

    public async void TestMethod1()
    {
        FooAsync();
    }

    public async void TestMethod2()
    {
        await FooAsync();
    }

    [Throws(typeof(NullReferenceException))]
    public async void TestMethod3()
    {
        await FooAsync();
    }

    [Throws(typeof(InvalidOperationException))]
    [Throws(typeof(TaskCanceledException))]
    [Throws(typeof(HttpRequestException))]
    public async void TestMethod4()
    {
        try
        {
            var httpClient = new HttpClient();
            await httpClient.GetStringAsync("");
        }
        catch (UriFormatException)
        {

        }
    }

    public Task Foo
    {
        [Throws(typeof(NullReferenceException))]
        get
        {
            return null!;
        }
    }

    public async void TestMethod5()
    {
        var x = Foo;
        await Foo;
        var x2 = this.Foo;
    }
}
