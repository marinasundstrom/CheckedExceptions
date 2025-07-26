public class RedundantCatchClause
{
    public void Foo()
    {

        try
        {
            int.Parse("a");
        }
        catch (FormatException formatException)
        {
        }
        catch (OverflowException overflowException)
        {
        }
        catch (ObjectDisposedException argumentException)
        {
        }
    }
}