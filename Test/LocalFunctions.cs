namespace Test;

public class LocalFunctions
{
    public void Unhandled()
    {
        Foo();

        [Throws(typeof(NullReferenceException))]
        static void Foo()
        {
            throw new NullReferenceException();
        };
    }

    public void Handled()
    {
        try
        {
            Foo();
        }
        catch (NullReferenceException ex)
        {
            Console.WriteLine("Handled exception: " + ex.Message);
        };

        [Throws(typeof(NullReferenceException))]
        static void Foo()
        {
            throw new NullReferenceException();
        };
    }

    [Throws(typeof(InvalidOperationException))]
    public static void Foo2()
    {
        throw new InvalidOperationException();
    }

    public void Unhandled2()
    {
        Foo();

        [Throws(typeof(InvalidOperationException))]
        static void Foo()
        {
            Foo2();
        };
    }
}