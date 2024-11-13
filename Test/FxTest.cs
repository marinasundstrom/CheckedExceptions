namespace Test;

public class FxTest
{
    [Throws(typeof(ArgumentNullException))]
    [Throws(typeof(FormatException))]
    [Throws(typeof(OverflowException))]
    public void Foo()
    {
        var x = int.Parse("f");
    }

    [Throws(typeof(Exception))]
    public void Foo2()
    {
        Foo();
    }
}