namespace Test.Cases.Lambdas;

public class Lambdas
{
    public void Unhandled()
    {
        var fetcher = [Throws(typeof(NullReferenceException))] () =>
        {
            throw new NullReferenceException();
        };

        fetcher();
    }

    [Throws(typeof(NullReferenceException))]
    public void Unhandled2()
    {
        var fetcher = [Throws(typeof(NullReferenceException))] () =>
        {
            throw new NullReferenceException();
        };

        fetcher();
    }

    public void Handled()
    {
        try
        {
            var fetcher = [Throws(typeof(NullReferenceException))] () =>
            {
                throw new NullReferenceException();
            };

            fetcher();
        }
        catch (NullReferenceException ex)
        {
            Console.WriteLine("Handled exception: " + ex.Message);
        }
    }

    [Throws(typeof(InvalidOperationException))]
    public void Foo()
    {
        throw new InvalidOperationException();
    }

    public void Unhandled3()
    {
        var fetcher = [Throws(typeof(Exception))] () =>
        {
            Foo();
        };

        try
        {
            fetcher();
        }
        catch (InvalidOperationException)
        {

        }
    }

    public void Handled4()
    {
        var fetcher = Foo;

        try
        {
            fetcher();
        }
        catch (InvalidOperationException)
        {

        }
    }
}