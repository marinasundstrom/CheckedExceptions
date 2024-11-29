namespace Test;

public class LambdaLocalFunctionScoping
{
    public void Test1()
    {
        try
        {
            var test = [Throws(typeof(InvalidOperationException))] bool (string s) =>
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
            bool Test(string s)
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