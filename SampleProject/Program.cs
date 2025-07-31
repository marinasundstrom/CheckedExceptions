try
{
    int result = ReadAndParse();
    Console.WriteLine(result);
}
catch (InvalidUserInputException ex)
{
    Console.WriteLine($"Input error: {ex.Message}");
}

[Throws(typeof(InvalidUserInputException))] // ✔️ Only the domain-specific exception is exposed
static int ReadAndParse()
{
    string input = "abc";  // Simulated input — could be user input in real scenarios

    try
    {
        return int.Parse(input);
    }
    catch (FormatException formatException)
    {
        // Handle and rethrow as domain-specific exception
        throw new InvalidUserInputException("Input was not a valid number.", formatException);
    }
    catch (OverflowException overflowException)
    {
        // Handle and rethrow as domain-specific exception
        throw new InvalidUserInputException("Input number was too large.", overflowException);
    }
}

class InvalidUserInputException : Exception
{
    public InvalidUserInputException(string message, Exception inner)
        : base(message, inner) { }
}

public class TestBase
{
    [Throws(typeof(ArgumentNullException))]
    public virtual bool Foo3 { get; set; }
}

public class TestDerive : TestBase
{
    public override bool Foo3
    {
        [Throws(typeof(ArgumentNullException))]
        get => throw new ArgumentNullException();
    }
}