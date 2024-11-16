namespace Test.Cases;

public class GeneralExceptionThrow
{
    [Throws(typeof(Exception))]
    public void Foo()
    {
        throw new NullReferenceException("Data source is null.");
    }

    public void Foo2()
    {
        var fetcher = [Throws(typeof(Exception))] () =>
        {
            throw new NullReferenceException();
        };

        fetcher();
    }

    public void Foo3()
    {
        Foo();

        [Throws(typeof(Exception))]
        static void Foo()
        {
            throw new NullReferenceException();
        };
    }
}