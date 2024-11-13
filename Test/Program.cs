using Test;

Console.Write("Write number: ");

var number = Console.ReadLine();

try
{
    var parsedNumber = int.Parse(number);

    Console.WriteLine($"The number was: {parsedNumber}");
}
catch (ArgumentException e) { }
catch (FormatException e) { }
catch (OverflowException e) { }

/*
MultipleThrows multipleThrows = new MultipleThrows();
multipleThrows.ProcessData1();

DateTime.Parse("s")

Foo();

try
{
    throw new InvalidOperationException();
}
catch (InvalidOperationException)
{
    throw;
}

static void Foo()
{
    throw new NotImplementedException();
}
*/