using System.Text;

namespace Test.Cases;

public class FxTest
{
    [Throws(typeof(FormatException))]
    [Throws(typeof(OverflowException))]
    public void Foo()
    {
        var x = int.Parse("42");
    }

    [Throws(typeof(ArgumentNullException))]
    [Throws(typeof(OverflowException))]
    public void Foo2()
    {
        try
        {
            var x = int.Parse("42");
        }
        catch (FormatException)
        {

        }
    }

    [Throws(typeof(Exception))]
    public void Foo3()
    {
        Foo();
    }

    public void Foo4()
    {
        StringBuilder stringBuilder = new StringBuilder();

        var x = stringBuilder.Length;

        stringBuilder.Length = 2;

        stringBuilder.AppendLine("2");
    }

    /// <summary>
    /// 
    /// </summary>
    /// <value></value>
    /// <exception cref="ArgumentNullException">
    /// The value provided that is set is null.
    /// </exception>
    public string Value
    {
        get;
        set;
    }

    public void Foo5()
    {
        Value = null;
    }


    /// <summary>
    /// 
    /// </summary>
    /// <value></value>
    /// <exception cref="ArgumentNullException">
    /// The value provided that is set is null.
    /// </exception>
    public string? Value2
    {
        get;
        set;
    }

    public void Foo6()
    {
        Value2 = null;
    }

    /// <summary>
    /// 
    /// </summary>
    /// <value></value>
    public string? Value3
    {
        get;
        set;
    }

    public void Foo7()
    {
        Value3 = null;
    }
}