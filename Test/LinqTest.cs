public class LinqTest
{
    public void Test1()
    {
        IEnumerable<int> items = [];
        var query = items.Where([Throws(typeof(FormatException), typeof(OverflowException))] (x) => x == int.Parse("10"));
        var x = query;
        var r = x.FirstOrDefault();

        foreach (var item in query)
        {

        }
    }
}