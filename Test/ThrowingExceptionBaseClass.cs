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
