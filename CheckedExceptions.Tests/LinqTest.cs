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
            using System;
            using System.Collections.Generic;
            using System.Linq;

            IEnumerable<int> items = [];
            var query = items.Where([Throws(typeof(FormatException), typeof(OverflowException))] (x) => x == int.Parse("10"));
            var r = query.First();
            """;

        var expected = Verifier.UnhandledException("InvalidOperationException")
            .WithSpan(16, 9, 16, 21);

        var expected2 = Verifier.Diagnostic(CheckedExceptionsAnalyzer.DiagnosticIdXmlDocButNoThrows)
            .WithArguments("InvalidOperationException")
            .WithSpan(9, 17, 9, 27);

        await Verifier.VerifyAnalyzerAsync(test, [expected, expected2]);
    }
}