namespace Test;

public class RedundantThrow
{
    [Throws(typeof(InvalidOperationException))]
    [Throws(typeof(ObjectDisposedException))]
    public void Foo()
    {
    }
}