namespace Test;

public class ThrowsAttributeTest
{
    [Throws(
        typeof(InvalidOperationException),
        typeof(FormatException),
        typeof(OverflowException))]
    [Throws(typeof(ArgumentException))]
    public void MethodThatThrows()
    {
        IEnumerable<string>? items = null;

        var x = items.First();

        throw new ArgumentOutOfRangeException();

        int.Parse("");
    }

    [Throws(
        typeof(InvalidOperationException),
        typeof(FormatException),
        typeof(OverflowException))]
    [Throws(typeof(ArgumentException))]
    public void MethodThatThrows2()
    {
        IEnumerable<string>? items = null;

        var x = items.First();

        throw new ArgumentOutOfRangeException();

        int.Parse("");
    }

    [Throws(
        typeof(InvalidOperationException),
        typeof(FormatException),
        typeof(OverflowException))]
    public void MethodThatThrows3()
    {
        try
        {
            IEnumerable<string>? items = null;

            var x = items.First();

            throw new ArgumentOutOfRangeException();

            int.Parse("");
        }
        catch (ArgumentException)
        {

        }
    }

    [Throws(
        typeof(InvalidOperationException),
        typeof(FormatException),
    typeof(OverflowException))]
    [Throws(typeof(ArgumentOutOfRangeException))]
    public void MethodThatThrows4()
    {

    }

    [Throws(
        typeof(InvalidOperationException),
        typeof(InvalidOperationException),
        typeof(OverflowException))]
    [Throws(typeof(InvalidOperationException))]
    public void MethodThatThrows5()
    {
        throw new OverflowException();
    }
}