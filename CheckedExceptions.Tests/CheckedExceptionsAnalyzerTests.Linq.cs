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

        await Verifier.VerifyAnalyzerAsync(test, setup: o =>
        {
            o.ExpectedDiagnostics.AddRange(expected, expected2, expected3);
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

        var expected4 = Verifier.UnhandledException("FormatException")
            .WithSpan(8, 6, 8, 94);

        var expected5 = Verifier.UnhandledException("OverflowException")
            .WithSpan(8, 6, 8, 94);

        await Verifier.VerifyAnalyzerAsync(test, setup: o =>
        {
            o.ExpectedDiagnostics.AddRange(expected, expected2, expected3, expected4, expected5);
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

        var expected = Verifier.UnhandledException("FormatException")
            .WithSpan(8, 15, 8, 43);

        var expected2 = Verifier.UnhandledException("OverflowException")
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

        var expected = Verifier.UnhandledException("FormatException")
            .WithSpan(9, 15, 8, 43);

        var expected2 = Verifier.UnhandledException("OverflowException")
            .WithSpan(9, 15, 8, 43);

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

        var expected = Verifier.UnhandledException("FormatException")
            .WithSpan(9, 18, 9, 46);

        var expected2 = Verifier.UnhandledException("OverflowException")
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

        var expected = Verifier.UnhandledException("FormatException")
            .WithSpan(10, 18, 9, 46);

        var expected2 = Verifier.UnhandledException("OverflowException")
            .WithSpan(10, 18, 9, 46);

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
}