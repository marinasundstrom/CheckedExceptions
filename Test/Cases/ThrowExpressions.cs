namespace Test.Cases;

public class ThrowExpressions
{
    public void FooBar1(int? x)
    {
        var z = x ?? throw new Exception(nameof(x));
    }

    [Throws(typeof(Exception))]
    public void FooBar12(int? x)
    {
        var z = x ?? throw new Exception(nameof(x));
    }

    public void FooBar2(int? x)
    {
        var z = x ?? throw new ArgumentNullException(nameof(x));
    }

    [Throws(typeof(ArgumentNullException))]
    public void FooBar21(int? x)
    {
        var z = x ?? throw new ArgumentNullException(nameof(x));
    }

    [Throws(typeof(Exception))]
    public void FooBar22(int? x)
    {
        var z = x ?? throw new ArgumentNullException(nameof(x));
    }

    public void FooBar3(int? x)
    {
        try
        {
            var z = x ?? throw new ArgumentNullException(nameof(x));
            var y = x ?? throw new ArgumentNullException(nameof(x));
            var f = x ?? throw new InvalidOperationException("Foo");

        }
        catch
        {
            throw;
        }
    }

    [Throws(
        typeof(ArgumentNullException),
        typeof(InvalidOperationException))]
    public void FooBar31(int? x)
    {
        try
        {
            var z = x ?? throw new ArgumentNullException(nameof(x));
            var y = x ?? throw new ArgumentNullException(nameof(x));
            var f = x ?? throw new InvalidOperationException("Foo");

        }
        catch
        {
            throw;
        }
    }

    [Throws(typeof(ArgumentNullException),
        typeof(FormatException))]
    public void FooBar32(int? x)
    {
        try
        {
            var z = x ?? throw new ArgumentNullException(nameof(x));
            var y = x ?? throw new ArgumentNullException(nameof(x));
            var f = x ?? throw new InvalidOperationException("Foo");
        }
        catch (InvalidOperationException)
        {
            throw new FormatException();
        }
        catch
        {
            throw;
        }
    }
}