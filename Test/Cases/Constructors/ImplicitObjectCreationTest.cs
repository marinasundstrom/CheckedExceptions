namespace Test.Cases.Constructors;

public class ImplicitObjectCreationTest
{
    [Throws(typeof(InvalidOperationException))]
    public ImplicitObjectCreationTest()
    {
        throw new InvalidOperationException("Constructor exception.");
    }

    public static void Test()
    {
        ImplicitObjectCreationTest foo = new();
    }
}