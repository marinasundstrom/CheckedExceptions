namespace Test;

public class RethrowTest
{
    [Throws(typeof(InvalidOperationException))]
    public void MethodThatThrows()
    {
        throw new InvalidOperationException();
    }

    //[Throws(typeof(InvalidOperationException))]
    public void Foo1()
    {
        try
        {
            MethodThatThrows();
        }
        catch (InvalidOperationException)
        {
            throw;
        }
    }

    [Throws(typeof(InvalidOperationException))]
    public void Foo12()
    {
        try
        {
            MethodThatThrows();
        }
        catch (InvalidOperationException)
        {
            throw;
        }
    }

    public void Foo2()
    {
        try
        {
            MethodThatThrows();
        }
        catch
        {
            throw;
        }
    }

    [Throws(typeof(Exception))]
    public void Foo22()
    {
        try
        {
            MethodThatThrows();
        }
        catch
        {
            throw;
        }
    }
}