Calculator calculator = new();

try
{
    Console.Write("Add integers\n");

    Console.Write("Left-hand side: ");
    var lhsStr = Console.ReadLine();

    var lhs = int.Parse(lhsStr!);

    Console.Write("Right-hand side: ");
    var rhsStr = Console.ReadLine();

    var rhs = int.Parse(rhsStr!);

    try
    {
        var sum = calculator.Add(lhs, rhs);

        Console.WriteLine($"Sum is: {sum}");
    }
    catch (OverflowException e)
    {
        Console.WriteLine($"The sum exceeds the bounds of which min is {int.MinValue} and and max is {int.MaxValue}");
    }
}
catch (OverflowException)
{
    Console.WriteLine($"Specified number must be between {int.MinValue} and {int.MaxValue}");
}
catch (FormatException)
{
    Console.WriteLine("Expected integer number");
}
catch (ArgumentOutOfRangeException)
{
    Console.WriteLine("Argument is out of range");
}
catch
{
    Console.WriteLine("Out of memory");
}

public class Calculator
{
    /// <summary>
    /// Add numbers
    /// </summary>
    /// <param name="a">Left-hand side</param>
    /// <param name="b">Right-hand side</param>
    /// <returns>The sum of a and b.</returns>
    /// <exception cref="System.OverflowException">The result exceeds the bounds.</exception>
    [Throws(typeof(OverflowException))]
    public int Add(int a, int b)
    {
        return a + b;
    }
}