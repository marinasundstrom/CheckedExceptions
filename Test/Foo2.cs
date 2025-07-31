public class FooBar2
{
    [Throws(typeof(ArgumentNullException))]
    public static int NewMethod()
    {
        var x = new[] { 1, 2, 3 };
        return x.Where(x => x > 2).First();
    }


    /// <exception cref="System.InvalidOperationException">Occurs when getting </exception>
    public static int Foo1
    {
        get
        {
            return 0;
        }
    }

    /// <exception cref="System.InvalidOperationException" />
    public static int Foo2
    {
        set
        {

        }
    }

    /// <exception cref="InvalidOperationException" />
    public static int Foo3 => 0;
}