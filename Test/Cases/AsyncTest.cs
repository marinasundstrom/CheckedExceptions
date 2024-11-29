namespace Test.Cases;

public class AsyncTest
{
    [Throws(typeof(InvalidOperationException))]
    public Task FooAsync()
    {
        throw new InvalidOperationException("Not valid.");
    }

    public async void TestMethod1()
    {
        FooAsync();
    }

    public async void TestMethod2()
    {
        await FooAsync();
    }

    [Throws(typeof(InvalidOperationException))]
    public async void TestMethod3()
    {
        await FooAsync();
    }

    [Throws(typeof(InvalidOperationException),
        typeof(TaskCanceledException),
        typeof(HttpRequestException))]
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
        [Throws(typeof(InvalidOperationException))]
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
