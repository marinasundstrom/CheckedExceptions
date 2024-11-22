namespace Test;

public class Foo
{
    public void Bar()
    {

        try
        {
            throw new InvalidOperationException("Argument is null");

        }
        catch (InvalidOperationException e)
        {

        }
    }

    public void Bar2()
    {

        try
        {
            int? x = null;
            x = x ?? throw new InvalidOperationException("Argument is null");
        }
        catch (InvalidOperationException e)
        {

        }
    }

    static void Test()
    {
        try
        {
            try
            {
                Console.Write("Write number: ");

                var number = Console.ReadLine();

                if (number is null)
                {
                    throw new InvalidOperationException("Argument is null");
                }

                var parsedNumber = Process(number);

                Console.WriteLine($"The result was: {parsedNumber}");
            }
            catch (IOException) { }
            catch (OutOfMemoryException) { }
            catch (ArgumentOutOfRangeException e)
            {
                Console.WriteLine(e.Message);
            }

            Console.WriteLine("End");
        }
        catch (InvalidOperationException e)
        {
#pragma warning disable THROW001 // Unhandled exception
            Console.WriteLine(e.Message);
#pragma warning restore THROW001 // Unhandled exception
        }
        catch
        {
            throw;
        }
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
}