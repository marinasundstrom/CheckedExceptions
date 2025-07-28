namespace Test;

public class RedundantThrow
{
    [Throws(typeof(InvalidOperationException))]
    [Throws(typeof(ObjectDisposedException))]
    public void Foo()
    {
    }

    [Throws(typeof(InvalidOperationException), typeof(ObjectDisposedException))]
    public void Foo2()
    {
    }

    public void Foo3()
    {
        var f1 = [Throws(typeof(InvalidOperationException))][Throws(typeof(ObjectDisposedException))] () => { };

        var f2 = [Throws(typeof(InvalidOperationException), typeof(ObjectDisposedException))] () => { };
    }

    public void Foo4()
    {
        [Throws(typeof(InvalidOperationException))]
        [Throws(typeof(ObjectDisposedException))]
        static void F1()
        {

        }

        [Throws(typeof(InvalidOperationException), typeof(ObjectDisposedException))]
        static void F2()
        {

        }
    }
}