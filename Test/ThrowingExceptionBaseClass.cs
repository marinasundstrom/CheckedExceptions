namespace Test;

public class ThrowingExceptionBaseClass
{
    public void Foo0()
    {
        throw new Exception("Data source is null.");
    }

    [Throws(typeof(InvalidOperationException))]
    public void Foo1()
    {
        throw new Exception("Data source is null.");
    }

    [Throws(typeof(Exception))]
    public void Foo2()
    {
        throw new Exception("Data source is null.");
    }

    public void Test()
    {
        Foo2();
    }
}

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
}