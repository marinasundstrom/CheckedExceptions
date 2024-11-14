namespace Test;

public class RethrowTest
{
    [Throws(typeof(InvalidOperationException))]
    public void MethodThatThrows()
    {
        throw new InvalidOperationException();
    }

    [Throws(typeof(ArgumentException))]
    public void MethodThatThrows2()
    {
        throw new ArgumentException();
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

    public void Foo31()
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

    public void Foo32()
    {
        try
        {
            MethodThatThrows2();
            MethodThatThrows();
        }
        catch
        {
            throw;
        }
    }

    [Throws(typeof(ArgumentException))]
    [Throws(typeof(InvalidOperationException))]
    public void Foo33()
    {
        try
        {
            MethodThatThrows2();
            MethodThatThrows();
        }
        catch (InvalidOperationException)
        {

        }
        catch
        {
            throw;
        }
    }

    public void Foo34()
    {
        try
        {
            MethodThatThrows();
            MethodThatThrows();
        }
        catch
        {
            throw;
        }
    }

    public void Foo4()
    {
        try
        {
            MethodThatThrows();
        }
        catch (InvalidOperationException exc)
        {

        }
    }
}