namespace Test.Cases;

public class CastOperator
{
    public void Foo()
    {

        var u = (int)42;

        var z = (string)42;

        var x = (string)(object)42;

        var x1 = (int)42.2;

        checked
        {
            var x3 = (int)42.2
        }
    }
}