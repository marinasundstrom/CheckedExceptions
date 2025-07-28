namespace NetStandard2_0Test;

public class Class1
{
    [Throws(typeof(InvalidOperationException))]
    public void MethodThatThrows()
    {
        // Won't indicate exceptions since .NET Standard 2.0 XML Docs is malformed.
        int.Parse("");

        // Handled
        throw new InvalidOperationException();

        // Unhandled
        throw new ArgumentException();

        // Your own libraries with XML docs will indicate whether handled or not
        Dependency.MethodThatThrows();
    }
}

public static class Dependency
{
    /// <summary>
    /// Test
    /// </summary>
    /// <exception cref="ArgumentException"></exception>
    public static void MethodThatThrows()
    {

    }
}
