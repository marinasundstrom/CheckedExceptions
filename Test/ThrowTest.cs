namespace Test;

public class ThrowTest
{
    public void MethodThatThrows()
    {
        IEnumerable<string>? items = null;

        var x = items.First();
    }
}