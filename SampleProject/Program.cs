try
{
    NewMethod();
}
catch (InvalidUserInputException invalidUserInputException)
{
}

public class Foo
{

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

    /// <summary>
    /// Test
    /// </summary>
    /// <exception cref="InvalidUserInputException" />
    [Throws(typeof(InvalidUserInputException))]
    static void NewMethod()
    {
        int result = ReadAndParse();
        Console.WriteLine(result);
    }
}

class InvalidUserInputException : Exception
{
    public InvalidUserInputException(string message, Exception inner)
        : base(message, inner) { }
}
