using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Testing;

namespace Sundstrom.CheckedExceptions.Tests;

using Verifier = CSharpAnalyzerVerifier<CheckedExceptionsAnalyzer, DefaultVerifier>;

public partial class LinqTest
{
    [Fact]
    public async Task QueryOperator()
    {
        var test = /* lang=c#-test */ """
            #nullable enable
            using System;
            using System.Collections.Generic;
            using System.Linq;

            IEnumerable<int> items = [];
            var query = items.Where([Throws(typeof(FormatException), typeof(OverflowException))] (x) => x == int.Parse("10"));
            var r = query.First();
            """;

        var expected = Verifier.UnhandledException("FormatException")
            .WithSpan(8, 15, 8, 22);

        var expected2 = Verifier.UnhandledException("OverflowException")
            .WithSpan(8, 15, 8, 22);

        var expected3 = Verifier.UnhandledException("InvalidOperationException")
            .WithSpan(8, 15, 8, 22);

        var expected4 = Verifier.Diagnostic(CheckedExceptionsAnalyzer.DiagnosticIdRedundantExceptionDeclaration)
            .WithArguments("FormatException")
            .WithSpan(7, 40, 7, 55);

        var expected5 = Verifier.Diagnostic(CheckedExceptionsAnalyzer.DiagnosticIdRedundantExceptionDeclaration)
            .WithArguments("OverflowException")
            .WithSpan(7, 65, 7, 82);

        await Verifier.VerifyAnalyzerAsync(test, setup: o =>
        {
            o.ExpectedDiagnostics.AddRange(expected, expected2, expected3, expected4, expected5);
            o.DisabledDiagnostics.Remove(CheckedExceptionsAnalyzer.DiagnosticIdRedundantExceptionDeclaration);
        }, executable: true);
    }

    [Fact]
    public async Task AsyncQueryOperator()
    {
        var test = /* lang=c#-test */ """
            #nullable enable
            using System;
            using System.Collections.Generic;
            using System.Linq;
            using System.Threading.Tasks;

            IAsyncEnumerable<int> items = default!;
            var query = items.Where([Throws(typeof(FormatException), typeof(OverflowException))] (x) => x == int.Parse("10"));
            var r = await query.FirstAsync();
            """;

        var expected = Verifier.UnhandledException("FormatException")
            .WithSpan(9, 21, 9, 33);

        var expected2 = Verifier.UnhandledException("OverflowException")
            .WithSpan(9, 21, 9, 33);

        var expected3 = Verifier.UnhandledException("InvalidOperationException")
            .WithSpan(9, 21, 9, 33);

        var expected4 = Verifier.Diagnostic(CheckedExceptionsAnalyzer.DiagnosticIdRedundantExceptionDeclaration)
            .WithArguments("FormatException")
            .WithSpan(8, 40, 8, 55);

        var expected5 = Verifier.Diagnostic(CheckedExceptionsAnalyzer.DiagnosticIdRedundantExceptionDeclaration)
            .WithArguments("OverflowException")
            .WithSpan(8, 65, 8, 82);

        await Verifier.VerifyAnalyzerAsync(test, setup: o =>
        {
            o.TestState.AdditionalReferences.Add(MetadataReference.CreateFromFile(typeof(System.Linq.AsyncEnumerable).Assembly.Location));
            o.ExpectedDiagnostics.AddRange(expected, expected2, expected3, expected4, expected5);
            o.DisabledDiagnostics.Remove(CheckedExceptionsAnalyzer.DiagnosticIdRedundantExceptionDeclaration);
        }, executable: true);
    }

    [Fact]
    public async Task QueryOperator_ImplicitlyDeclared()
    {
        var test = /* lang=c#-test */ """
            #nullable enable
            using System;
            using System.Collections.Generic;
            using System.Linq;

            IEnumerable<int> items = [];
            var query = items.Where((x) => x == int.Parse("10"));
            var r = query.First();
            """;

        var expected = Verifier.UnhandledException("FormatException")
            .WithSpan(8, 15, 8, 22);

        var expected2 = Verifier.UnhandledException("OverflowException")
            .WithSpan(8, 15, 8, 22);

        var expected3 = Verifier.UnhandledException("InvalidOperationException")
            .WithSpan(8, 15, 8, 22);

        var expected4 = Verifier.Diagnostic(CheckedExceptionsAnalyzer.DiagnosticIdImplicitlyDeclaredException)
            .WithArguments("FormatException")
            .WithSpan(7, 41, 7, 52);

        var expected5 = Verifier.Diagnostic(CheckedExceptionsAnalyzer.DiagnosticIdImplicitlyDeclaredException)
            .WithArguments("OverflowException")
            .WithSpan(7, 41, 7, 52);

        await Verifier.VerifyAnalyzerAsync(test, setup: o =>
        {
            o.ExpectedDiagnostics.AddRange(expected, expected2, expected3, expected4, expected5);
        }, executable: true);
    }

    [Fact]
    public async Task ForEach()
    {
        var test = /* lang=c#-test */ """
            #nullable enable
            using System;
            using System.Collections.Generic;
            using System.Linq;

            IEnumerable<int> items = [];
            var query = items.Where([Throws(typeof(FormatException), typeof(OverflowException))] (x) => x == int.Parse("10"));
            foreach (var item in query) 
            {

            }
            """;

        var expected = Verifier.UnhandledException("FormatException")
            .WithSpan(8, 22, 8, 27);

        var expected2 = Verifier.UnhandledException("OverflowException")
            .WithSpan(8, 22, 8, 27);
        var expected3 = Verifier.Diagnostic(CheckedExceptionsAnalyzer.DiagnosticIdRedundantExceptionDeclaration)
            .WithArguments("FormatException")
            .WithSpan(7, 40, 7, 55);

        var expected4 = Verifier.Diagnostic(CheckedExceptionsAnalyzer.DiagnosticIdRedundantExceptionDeclaration)
            .WithArguments("OverflowException")
            .WithSpan(7, 65, 7, 82);

        await Verifier.VerifyAnalyzerAsync(test, setup: o =>
        {
            o.ExpectedDiagnostics.AddRange(expected, expected2, expected3, expected4);
            o.DisabledDiagnostics.Remove(CheckedExceptionsAnalyzer.DiagnosticIdRedundantExceptionDeclaration);
        }, executable: true);
    }

    [Fact]
    public async Task ForEachCaughtByCatchAll()
    {
        var test = /* lang=c#-test */ """
            #nullable enable
            using System;
            using System.Collections.Generic;
            using System.Linq;

            IEnumerable<int> items = [];
            var query = items.Where(x => int.Parse("10") == x);
            try
            {
                foreach (var item in query) { }
            }
            catch
            {
            }
            """;

        var expected = Verifier.Diagnostic(CheckedExceptionsAnalyzer.DiagnosticIdImplicitlyDeclaredException)
            .WithArguments("FormatException")
            .WithSpan(7, 34, 7, 45);

        var expected2 = Verifier.Diagnostic(CheckedExceptionsAnalyzer.DiagnosticIdImplicitlyDeclaredException)
            .WithArguments("OverflowException")
            .WithSpan(7, 34, 7, 45);

        await Verifier.VerifyAnalyzerAsync(test, setup: o =>
        {
            o.ExpectedDiagnostics.AddRange(expected, expected2);
        }, executable: true);
    }

    [Fact]
    public async Task PassDelegateByVariable()
    {
        var test = /* lang=c#-test */ """
            #nullable enable
            using System;
            using System.Collections.Generic;
            using System.Linq;

            IEnumerable<int> items = [];
            Func<int, bool> pred = [Throws(typeof(FormatException), typeof(OverflowException))] (z) => int.Parse("10") == z;
            var query = items.Where(pred);
            foreach (var item in query) { }
            """;

        var expected = Verifier.UnhandledException("FormatException")
            .WithSpan(9, 22, 9, 27);

        var expected2 = Verifier.UnhandledException("OverflowException")
            .WithSpan(9, 22, 9, 27);
        await Verifier.VerifyAnalyzerAsync(test, setup: o =>
        {
            o.ExpectedDiagnostics.AddRange(expected, expected2);
        }, executable: true);
    }

    [Fact]
    public async Task PassDelegateByVariable_Chained()
    {
        var test = /* lang=c#-test */ """
            #nullable enable
            using System;
            using System.Collections.Generic;
            using System.Linq;

            IEnumerable<int> items = [];
            Func<int, bool> pred = [Throws(typeof(FormatException), typeof(OverflowException))] (z) => int.Parse("10") == z;
            var query = items.Where(pred).Where(x => x is 0);
            foreach (var item in query) { }
            """;

        var expected = Verifier.UnhandledException("FormatException")
            .WithSpan(9, 22, 9, 27);

        var expected2 = Verifier.UnhandledException("OverflowException")
            .WithSpan(9, 22, 9, 27);
        await Verifier.VerifyAnalyzerAsync(test, setup: o =>
        {
            o.ExpectedDiagnostics.AddRange(expected, expected2);
        }, executable: true);
    }

    [Fact]
    public async Task CastOperator()
    {
        var test = /* lang=c#-test */ """
            #nullable enable
            using System;
            using System.Collections.Generic;
            using System.Linq;

            IEnumerable<object> items = [];
            var query = items
                .Where([Throws(typeof(FormatException), typeof(OverflowException))] (x) => x is not null)
            .Cast<string>();

            foreach (var item in query) { }
            """;

        var expected = Verifier.UnhandledException("FormatException")
            .WithSpan(11, 22, 11, 27);

        var expected2 = Verifier.UnhandledException("OverflowException")
            .WithSpan(11, 22, 11, 27);

        var expected3 = Verifier.UnhandledException("InvalidCastException")
            .WithSpan(11, 22, 11, 27);
        var expected4 = Verifier.Diagnostic(CheckedExceptionsAnalyzer.DiagnosticIdRedundantExceptionDeclaration)
            .WithArguments("FormatException")
            .WithSpan(8, 27, 8, 42);

        var expected5 = Verifier.Diagnostic(CheckedExceptionsAnalyzer.DiagnosticIdRedundantExceptionDeclaration)
            .WithArguments("OverflowException")
            .WithSpan(8, 52, 8, 69);

        await Verifier.VerifyAnalyzerAsync(test, setup: o =>
        {
            o.ExpectedDiagnostics.AddRange(expected, expected2, expected3, expected4, expected5);
            o.DisabledDiagnostics.Remove(CheckedExceptionsAnalyzer.DiagnosticIdRedundantExceptionDeclaration);
        }, executable: true);
    }

    [Fact]
    public async Task QueryAsArgument()
    {
        var test = /* lang=c#-test */ """
            #nullable enable
            using System;
            using System.Collections.Generic;
            using System.Linq;

            IEnumerable<string> items = [];
            void Consume(IEnumerable<string> q) { }
            Consume(items.Where(x => int.Parse(x) > 0));
            """;

        var expected = Verifier.UnhandledExceptionEnumerableBoundary("IEnumerable<string>", "FormatException")
            .WithSpan(8, 15, 8, 43);

        var expected2 = Verifier.UnhandledExceptionEnumerableBoundary("IEnumerable<string>", "OverflowException")
            .WithSpan(8, 15, 8, 43);

        var expected3 = Verifier.Diagnostic(CheckedExceptionsAnalyzer.DiagnosticIdImplicitlyDeclaredException)
            .WithArguments("FormatException")
            .WithSpan(8, 30, 8, 38);

        var expected4 = Verifier.Diagnostic(CheckedExceptionsAnalyzer.DiagnosticIdImplicitlyDeclaredException)
            .WithArguments("OverflowException")
            .WithSpan(8, 30, 8, 38);

        await Verifier.VerifyAnalyzerAsync(test, setup: o =>
        {
            o.ExpectedDiagnostics.AddRange(expected, expected2, expected3, expected4);
        }, executable: true);
    }

    [Fact]
    public async Task EnumerableAsArgument()
    {
        var test = /* lang=c#-test */ """
            #nullable enable
            using System;
            using System.Collections.Generic;
            using System.Linq;

            IEnumerable<string> items = [];
            void Consume(IEnumerable<string> q) { }
            var query = items.Where(x => int.Parse(x) > 0);
            Consume(query);
            """;

        var expected = Verifier.Diagnostic(CheckedExceptionsAnalyzer.DiagnosticIdImplicitlyDeclaredException)
            .WithArguments("FormatException")
            .WithSpan(8, 34, 8, 42);

        var expected2 = Verifier.Diagnostic(CheckedExceptionsAnalyzer.DiagnosticIdImplicitlyDeclaredException)
            .WithArguments("OverflowException")
            .WithSpan(8, 34, 8, 42);

        var expected3 = Verifier.UnhandledExceptionEnumerableBoundary("IEnumerable<string>", "FormatException")
            .WithSpan(9, 9, 9, 14);

        var expected4 = Verifier.UnhandledExceptionEnumerableBoundary("IEnumerable<string>", "OverflowException")
            .WithSpan(9, 9, 9, 14);

        await Verifier.VerifyAnalyzerAsync(test, setup: o =>
        {
            o.ExpectedDiagnostics.AddRange(expected, expected2, expected3, expected4);
        }, executable: true);
    }

    [Fact]
    public async Task MaterializeEnumerableAsArgument()
    {
        var test = /* lang=c#-test */ """
            #nullable enable
            using System;
            using System.Collections.Generic;
            using System.Linq;

            IEnumerable<string> items = [];
            void Consume(IEnumerable<string> q) { }
            var query = items.Where(x => int.Parse(x) > 0);
            Consume(query.ToArray());
            """;

        var expected = Verifier.UnhandledException("FormatException")
            .WithSpan(9, 15, 9, 24);

        var expected2 = Verifier.UnhandledException("OverflowException")
            .WithSpan(9, 15, 9, 24);

        var expected3 = Verifier.Diagnostic(CheckedExceptionsAnalyzer.DiagnosticIdImplicitlyDeclaredException)
            .WithArguments("FormatException")
            .WithSpan(8, 34, 8, 42);

        var expected4 = Verifier.Diagnostic(CheckedExceptionsAnalyzer.DiagnosticIdImplicitlyDeclaredException)
            .WithArguments("OverflowException")
            .WithSpan(8, 34, 8, 42);

        await Verifier.VerifyAnalyzerAsync(test, setup: o =>
        {
            o.ExpectedDiagnostics.AddRange(expected, expected2, expected3, expected4);
        }, executable: true);
    }

    [Fact]
    public async Task MaterializeEnumerableInForeach()
    {
        var test = /* lang=c#-test */ """
            #nullable enable
            using System;
            using System.Collections.Generic;
            using System.Linq;

            IEnumerable<string> items = [];
            var query = items.Where(x => int.Parse(x) > 0);
            foreach(var i in query.ToArray()) {}
            """;

        var expected = Verifier.UnhandledException("FormatException")
            .WithSpan(8, 24, 8, 33);

        var expected2 = Verifier.UnhandledException("OverflowException")
            .WithSpan(8, 24, 8, 33);

        var expected3 = Verifier.Diagnostic(CheckedExceptionsAnalyzer.DiagnosticIdImplicitlyDeclaredException)
            .WithArguments("FormatException")
            .WithSpan(7, 34, 7, 42);

        var expected4 = Verifier.Diagnostic(CheckedExceptionsAnalyzer.DiagnosticIdImplicitlyDeclaredException)
            .WithArguments("OverflowException")
            .WithSpan(7, 34, 7, 42);

        await Verifier.VerifyAnalyzerAsync(test, setup: o =>
        {
            o.ExpectedDiagnostics.AddRange(expected, expected2, expected3, expected4);
        }, executable: true);
    }

    [Fact]
    public async Task SpreadMaterializesQuery()
    {
        var test = /* lang=c#-test */ """
            #nullable enable
            using System;
            using System.Collections.Generic;
            using System.Linq;

            IEnumerable<int> Cast4()
            {
                IEnumerable<object> xs2 = [];
                var q0 = xs2.Where(x => x is not null).Cast<int>();
                return [.. q0];
            }
            """;

        var expected = Verifier.UnhandledException("InvalidCastException")
            .WithSpan(10, 13, 10, 18);

        await Verifier.VerifyAnalyzerAsync(test, setup: o =>
        {
            o.ExpectedDiagnostics.Add(expected);
        }, executable: true);
    }

    [Fact]
    public async Task Spread()
    {
        var test = /* lang=c#-test */ """
            #nullable enable
            using System;
            using System.Collections.Generic;
            using System.Linq;

            IEnumerable<int> Cast4()
            {
                IEnumerable<object> xs2 = [];
                var q0 = xs2.Where(x => x is not null).Cast<int>();
                IEnumerable<int> foo = [.. q0];
                return foo;
            }
            """;

        var expected = Verifier.UnhandledException("InvalidCastException")
            .WithSpan(10, 29, 10, 34);

        await Verifier.VerifyAnalyzerAsync(test, setup: o =>
        {
            o.ExpectedDiagnostics.Add(expected);
        }, executable: true);
    }

    [Fact]
    public async Task ReturnQuery()
    {
        var test = /* lang=c#-test */ """
            #nullable enable
            using System;
            using System.Collections.Generic;
            using System.Linq;

            IEnumerable<string> items = [];
            IEnumerable<string> Get()
            {
                return items.Where(x => int.Parse(x) > 0);
            }
            """;

        var expected = Verifier.UnhandledExceptionEnumerableBoundary("IEnumerable<string>", "FormatException")
            .WithSpan(9, 18, 9, 46);

        var expected2 = Verifier.UnhandledExceptionEnumerableBoundary("IEnumerable<string>", "OverflowException")
            .WithSpan(9, 18, 9, 46);

        var expected3 = Verifier.Diagnostic(CheckedExceptionsAnalyzer.DiagnosticIdImplicitlyDeclaredException)
            .WithArguments("FormatException")
            .WithSpan(9, 33, 9, 41);

        var expected4 = Verifier.Diagnostic(CheckedExceptionsAnalyzer.DiagnosticIdImplicitlyDeclaredException)
            .WithArguments("OverflowException")
            .WithSpan(9, 33, 9, 41);

        await Verifier.VerifyAnalyzerAsync(test, setup: o =>
        {
            o.ExpectedDiagnostics.AddRange(expected, expected2, expected3, expected4);
        }, executable: true);
    }

    [Fact]
    public async Task ReturnEnumerable()
    {
        var test = /* lang=c#-test */ """
            #nullable enable
            using System;
            using System.Collections.Generic;
            using System.Linq;

            IEnumerable<string> items = [];
            IEnumerable<string> Get()
            {
                var query = items.Where(x => int.Parse(x) > 0);
                return query;
            }
            """;

        var expected = Verifier.Diagnostic(CheckedExceptionsAnalyzer.DiagnosticIdImplicitlyDeclaredException)
            .WithArguments("FormatException")
            .WithSpan(9, 38, 9, 46);

        var expected2 = Verifier.Diagnostic(CheckedExceptionsAnalyzer.DiagnosticIdImplicitlyDeclaredException)
            .WithArguments("OverflowException")
            .WithSpan(9, 38, 9, 46);

        var expected3 = Verifier.UnhandledExceptionEnumerableBoundary("IEnumerable<string>", "FormatException")
            .WithSpan(10, 12, 10, 17);

        var expected4 = Verifier.UnhandledExceptionEnumerableBoundary("IEnumerable<string>", "OverflowException")
            .WithSpan(10, 12, 10, 17);

        await Verifier.VerifyAnalyzerAsync(test, setup: o =>
        {
            o.ExpectedDiagnostics.AddRange(expected, expected2, expected3, expected4);
        }, executable: true);
    }

    [Fact]
    public async Task QueryableOperator_EnabledByDefault()
    {
        var test = /* lang=c#-test */ """
            #nullable enable
            using System;
            using System.Collections.Generic;
            using System.Linq;

            IQueryable<int> items = new List<int>().AsQueryable();
            var query = items.Where(x => true);
            var r = Queryable.First<int>(query);
            """;

        var expected = Verifier.UnhandledException("InvalidOperationException")
            .WithSpan(8, 19, 8, 36);

        await Verifier.VerifyAnalyzerAsync(test, setup: o =>
        {
            o.ExpectedDiagnostics.Add(expected);
        }, executable: true);
    }

    [Fact]
    public async Task QueryableOperator_Disabled()
    {
        var test = /* lang=c#-test */ """
            #nullable enable
            using System;
            using System.Collections.Generic;
            using System.Linq;

            IQueryable<int> items = new List<int>().AsQueryable();
            var query = items.Where(x => true);
            var r = Queryable.First<int>(query);
            """;

        await Verifier.VerifyAnalyzerAsync(test, setup: o =>
        {
            o.TestState.AdditionalFiles.Add(("CheckedExceptions.settings.json", """
{
  "disableLinqQueryableSupport": true
}
"""));
        }, executable: true);
    }
}