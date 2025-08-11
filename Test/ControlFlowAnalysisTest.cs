namespace Test;

public class ControlFlowAnalysisTest
{
    public int Test()
    {
        try
        {
            return int.Parse("foo");
        }
        catch (FormatException ex)
        {
            return -1;
        }
        catch (OverflowException ex)
        {
            return -1;
        }
        catch (InvalidOperationException ex)
        {
            return -1;
        }
        catch
        {
            return -1;
        }
        finally
        {
            Console.WriteLine("Hey!");
        }

        Console.WriteLine("Test");
    }
}