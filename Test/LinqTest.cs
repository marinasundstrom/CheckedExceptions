using System.Data.SqlTypes;
using System.Numerics;

public class LinqTest
{
    public void Test1()
    {

        IEnumerable<int> items = [];
        var query = items.Where([Throws(typeof(FormatException), typeof(OverflowException))] (x) => x == int.Parse("10"));
        var x = query;
        var r = x.Select((z) => 2);

        foreach (var n in query) { }

        foreach (var item in r)
        {

        }

        NewMethod2();

        NewMethod1();

        NewMethod();
    }

    private static void NewMethod2()
    {
        IEnumerable<int> xs = [];
        Func<int, bool> pred = [Throws(typeof(FormatException))] (z) => int.Parse("10") == z;
        var q2 = xs.Where(pred).Where(x => x == 0);
        foreach (var x in q2) { }
    }

    private static void NewMethod1()
    {
        IEnumerable<object> xs2 = [];
        var q0 = xs2.Where([Throws(typeof(InvalidCastException))] (x) => x == (string)x);
        foreach (var n in q0) { }
    }

    private static void NewMethod()
    {
        IEnumerable<object> xs2 = [];
        var q0 = xs2
            .Where([Throws(typeof(FormatException), typeof(OverflowException))] (x) => x != null)
            .Cast<string>();

        var x2 = q0.FirstOrDefault();

        foreach (var n in q0) { }
    }

    private static void NewMethod0()
    {
        IEnumerable<int> xs2 = [];
        var q0 = xs2
            .Where([Throws(typeof(FormatException), typeof(OverflowException))] (x) => x != null)
            .Cast<int>();

        var x2 = q0.FirstOrDefault();

        foreach (var n in q0) { }
    }
}