using System.Data.SqlTypes;
using System.Numerics;

using Test;

public class LinqTest
{
    public void Test1()
    {
        ExplicitlyDeclaredThrows();
        ImplicitlyDeclaredThrows();

        WithMethodGroup();

        NestedConversionExpression();

        Cast();

        Cast2();
    }

    private static void ExplicitlyDeclaredThrows()
    {
        IEnumerable<int> items = [];
        var query = items.Where([Throws(typeof(FormatException), typeof(OverflowException))] (x) => x == int.Parse("10"));
        var x = query;
        var r = x.Select((z) => 2);

        foreach (var n in query) { }

        foreach (var item in r)
        {

        }
    }

    private static void ImplicitlyDeclaredThrows()
    {
        IEnumerable<int> items = [];
        var query = items.Where((x) => x == int.Parse("10"));
        var x = query;
        var r = x.Select((z) => 2);

        foreach (var n in query) { }

        foreach (var item in r)
        {

        }
    }

    private static void WithMethodGroup()
    {
        IEnumerable<int> xs = [];
        Func<int, bool> pred = z => int.Parse("10") == z;
        var q2 = xs.Where(pred).Where(x => x is 0);
        foreach (var x in q2) { }
    }

    private static void NestedConversionExpression()
    {
        IEnumerable<object> xs2 = [];
        var q0 = xs2.Where(x => x == (string)x);
        foreach (var n in q0) { }
    }

    private static void Cast()
    {
        IEnumerable<object> xs2 = [];
        var q0 = xs2
            .Where(x => x is not null)
            .Cast<string>();

        var x2 = q0.FirstOrDefault();

        foreach (var n in q0) { }
    }

    private static void Cast2()
    {
        IEnumerable<object> xs2 = [];
        var q0 = xs2
            .Where(x => x is not null)
            .Cast<int>();

        var x2 = q0.FirstOrDefault();

        foreach (var n in q0) { }
    }

    private static int Cast3()
    {
        IEnumerable<object> xs2 = [];
        var q0 = xs2
            .Where((x) => x is not null)
            .Cast<int>();

        return q0.FirstOrDefault();
    }

    private static IEnumerable<int> Cast4()
    {
        IEnumerable<object> xs2 = [];
        var q0 = xs2
            .Where((x) => x is not null)
            .Cast<int>();

        return q0;
    }

    private static IEnumerable<int> Cast5()
    {
        IEnumerable<object> xs2 = [];
        var q0 = xs2
            .Where((x) => x is not null)
            .Cast<int>();

        return Foo(q0);
    }

    private static IEnumerable<int> Cast6()
    {
        IEnumerable<object> xs2 = [];
        var q0 = xs2
            .Where((x) => x is not null)
            .Cast<int>();

        return Foo(q0.ToArray());
    }

    private static IEnumerable<int> Foo(IEnumerable<int> q0)
    {
        throw new NotImplementedException();
    }

    private static void Cast7()
    {
        IEnumerable<string> items = [];
        var query = items.Where(x => int.Parse(x) > 0);
        foreach (var i in query.ToArray()) { }
    }
}