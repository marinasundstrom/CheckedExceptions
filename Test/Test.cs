namespace Test;

public class Test
{
    [Throws(typeof(InvalidOperationException))]
    public void Foo()
    {
        throw new NullReferenceException("Data source is null.");
    }
}