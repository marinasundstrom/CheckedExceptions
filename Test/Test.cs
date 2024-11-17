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
}
