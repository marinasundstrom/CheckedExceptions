namespace Test;

public class LambdaLocalFunctionScoping
{
    public void Test1()
    {
        try
        {
            var test = bool (string s) =>
            {
                throw new InvalidOperationException();
                return true;
            };

            test("");
        }
        catch (InvalidOperationException e)
        {

        }
    }

    public void Foo()
    {
        try
        {
            Test("");

            [Throws(typeof(InvalidOperationException))]
            static bool Test(string s)
            {
                throw new InvalidOperationException();
                return true;
            }
        }
        catch (InvalidOperationException e)
        {

        }
    }
}