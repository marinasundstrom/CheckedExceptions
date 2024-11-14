using Test;

try
{
    Console.Write("Write number: ");

    var number = Console.ReadLine();

    if (number is null)
    {
        throw new InvalidOperationException("Argument is null");
    }

    var parsedNumber = Process(number);

    Console.WriteLine($"The number was: {parsedNumber}");
}
catch (IOException e) { }
catch (OutOfMemoryException e) { }
catch (ArgumentOutOfRangeException e) { }
catch (InvalidOperationException e)
{
#pragma warning disable THROW001 // Checked exception
    Console.WriteLine(e.Message);
#pragma warning restore THROW001 // Checked exception
}

[Throws(typeof(InvalidOperationException))]
static double Process(string value)
{
    try
    {
        var no = double.Parse(value);
        return no * 10;
    }
    catch (ArgumentNullException e)
    {
        throw new InvalidOperationException("Argument is null");
    }
    catch (FormatException e)
    {
        throw new InvalidOperationException("Format is invalid");
    }
    catch (OverflowException e)
    {
        throw new InvalidOperationException("Number exceeds values");
    }
}