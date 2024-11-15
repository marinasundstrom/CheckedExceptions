namespace Test;

public class Foo
{
    public void Bar()
    {

        try
        {
            throw new InvalidOperationException("Argument is null");

        }
        catch (InvalidOperationException e)
        {

        }
    }

    public void Bar2()
    {

        try
        {
            int? x = null;
            x = x ?? throw new InvalidOperationException("Argument is null");
        }
        catch (InvalidOperationException e)
        {

        }
    }

    [Throws(typeof(InvalidOperationException))]
    static double Process(string value)
    {
        try
        {
            var no = double.Parse(value);
            return no * 10;
        }
        catch (ArgumentNullException e)
        {
            throw new InvalidOperationException("Argument is null");
        }
        catch (FormatException e)
        {
            throw new InvalidOperationException("Format is invalid");
        }
        catch (OverflowException e)
        {
            throw new InvalidOperationException("Number exceeds values");
        }
    }
}