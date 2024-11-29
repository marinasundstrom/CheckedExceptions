namespace Test;

public class DictionaryTest
{
    [Throws(
        typeof(ArgumentException), 
        typeof(KeyNotFoundException))]
    public void Test()
    {
        var dict = new Dictionary<string, object>();

        dict.Add("42", 4);

        dict["20"] = 72;
        object x = dict["2"];
        //object? x2 = dict?["2"];

        bool y = dict.TryGetValue("2", out var o);

        Console.WriteLine("Foo");
    }

    public void Test2()
    {
        Test();
    }
}