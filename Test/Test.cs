namespace Test;

public class Test
{
    [Throws(typeof(InvalidOperationException))]
    public void Foo()
    {
        throw new NullReferenceException("Data source is null.");
    }

    public void TestMethod()
    {
        void LocalFunction()
        {
            // Should trigger THROW001
            throw new NotImplementedException();
        }
        LocalFunction();
    }

    public Task Foo2
    {
        [Throws(typeof(NullReferenceException))]
        get
        {
            return null!;
        }
    }

    public void Test2()
    {
        var x = Foo2;
        var x2 = this.Foo2;

        Foo();
        this.Foo();
    }
}
