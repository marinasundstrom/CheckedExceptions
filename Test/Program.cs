
try
{
    int.Parse("a");
}
catch (FormatException formatException)
{
}
catch (OverflowException overflowException)
{

}
catch
{
    Console.WindowWidth;
}