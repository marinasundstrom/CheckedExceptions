namespace Test;

public class ThrowsTest
{
    [Throws(typeof(InvalidOperationException))]
    public void Foo()
    {
        throw new InvalidOperationException();
    }

    public void Foo2()
    {
        try
        {
            throw new NullReferenceException("Data source is null.");
        }
        catch (NullReferenceException exc)
        {
            throw new InvalidCastException();
        }
        catch (FormatException exc)
        {
            // The cath(FormatException) block should be marked as inapplicable since no throw statement or method is declared as possibly throwing this.
            throw;
        }
    }

    public void Foo3()
    {
        try
        {
            Foo();
        }
        catch
        {
            // This should warn about Exception being thrown if no matching ThrowsAttribute found.
            throw;
        }
    }

    public void Foo4()
    {
        try
        {
            Foo();
        }
        catch (InvalidOperationException exc)
        {
            throw new InvalidCastException();
        }
        catch
        {
            // This should warn about Exception being thrown if no matching ThrowsAttribute found.
            throw;
        }
    }
}
