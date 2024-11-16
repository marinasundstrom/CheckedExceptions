namespace Test.Cases;

public class RedundantDuplicateAttributes
{
    [Throws(typeof(NullReferenceException))]
    [Throws(typeof(NullReferenceException))]
    public void Foo()
    {
        throw new NullReferenceException("Data source is null.");
    }

    public void Foo2()
    {
        var fetcher = [Throws(typeof(NullReferenceException))][Throws(typeof(NullReferenceException))] () =>
        {
            throw new NullReferenceException();
        };

        fetcher();
    }

    public void Foo3()
    {
        Foo();

        [Throws(typeof(NullReferenceException))]
        [Throws(typeof(NullReferenceException))]
        static void Foo()
        {
            throw new NullReferenceException();
        };
    }
}
