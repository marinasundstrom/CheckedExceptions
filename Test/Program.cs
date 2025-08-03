
try
{
    int result = ReadAndParse();
    Console.WriteLine(result);
}
catch (InvalidUserInputException ex)
{
    Console.WriteLine($"Input error: {ex.Message}");
}

[Throws(typeof(InvalidUserInputException), typeof(ArgumentException))] // ✔️ Only the domain-specific exception is exposed
static int ReadAndParse()
{
    string input = "abc";  // Simulated input — could be user input in real scenarios

    try
    {
        //May throw FormatException, or OverflowException, or return
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
        throw new InvalidUserInputException("Input was not a valid number.", overflowException);
    }
    catch
    {
        Console.WriteLine();
    }

    // This will never be reachable
    // And thus not unhandled
    throw new ArgumentException();
}

class InvalidUserInputException : Exception
{
    public InvalidUserInputException(string message, Exception inner)
        : base(message, inner) { }
}