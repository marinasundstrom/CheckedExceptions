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
                .Where([Throws(typeof(FormatException), typeof(OverflowException))] (x) => x != null)
                .Cast<string>();

            foreach (var item in query) { }
            """;

        var expected = Verifier.UnhandledException("FormatException")
            .WithSpan(11, 22, 11, 27);

        var expected2 = Verifier.UnhandledException("OverflowException")
            .WithSpan(11, 22, 11, 27);

        var expected3 = Verifier.UnhandledException("InvalidCastException")
            .WithSpan(11, 22, 11, 27);

        await Verifier.VerifyAnalyzerAsync(test, setup: o =>
        {
            o.ExpectedDiagnostics.AddRange(expected, expected2, expected3);
        }, executable: true);
    }
}