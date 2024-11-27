namespace Test;

public class FooBar
{
    public void X()
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
            //catch (OutOfMemoryException) { }
            catch (ArgumentOutOfRangeException e)
            {
                Console.WriteLine(e.Message);
            }

            Console.WriteLine("End");
        }
        catch (InvalidOperationException e)
        {
            Console.WriteLine(e.Message);
        }

        [Throws(typeof(InvalidOperationException))]
        static double Process(string value)
        {
            try
            {
                var no = double.Parse(value);
                return Math.Pow(no, 42);
            }
            catch (ArgumentNullException)
            {
                throw new InvalidOperationException("Argument is null");
            }
            catch (FormatException)
            {
                throw new InvalidOperationException("Format is invalid");
            }
            catch (OverflowException)
            {
                throw new InvalidOperationException("Number exceeds values");
            }
        }
    }
}