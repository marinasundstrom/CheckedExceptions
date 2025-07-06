
try
{
    var result = AddStringsAsNumbers("1", "b");

    Console.WriteLine(result);
}
catch (FormatException)
{

}
catch (OverflowException)
{

}

[Throws(typeof(FormatException), typeof(OverflowException))]
static int AddStringsAsNumbers(string a, string b)
{
    return int.Parse(a) + int.Parse(b);
}