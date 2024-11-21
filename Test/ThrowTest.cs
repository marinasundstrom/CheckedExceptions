namespace Test;

public class ThrowTest
{
    public void MethodThatThrows()
    {
        IEnumerable<string> items = [];

        var x = items.First();
    }
}