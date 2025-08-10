using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Testing;

namespace Sundstrom.CheckedExceptions.Tests;

using Verifier = CSharpAnalyzerVerifier<CheckedExceptionsAnalyzer, DefaultVerifier>;

public partial class LinqTest
{
    [Fact]
    public async Task Test()
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


        await Verifier.VerifyAnalyzerAsync(test, [expected, expected2]);
    }
}