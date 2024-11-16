namespace Test.Cases;

public class ThrowingGeneralException
{
    public void Foo0()
    {
        throw new Exception("Data source is null.");
    }

    [Throws(typeof(Exception))]
    public void Foo1()
    {
        throw new Exception("Data source is null.");
    }

    [Throws(typeof(Exception))]
    public void Foo2()
    {
        Foo1();
    }
}