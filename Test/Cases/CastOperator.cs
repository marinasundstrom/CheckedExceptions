namespace Test.Cases;

public class CastOperator
{
    public void Foo()
    {

        var u = (int)42;

        var x = (string)(object)42;

        var x1 = (int)42.2;

        checked
        {
            long o = 20;
            var x3 = (int)o;

            var x4 = (int)1e100;
            var x5 = (int)42.2;
        }
    }
}