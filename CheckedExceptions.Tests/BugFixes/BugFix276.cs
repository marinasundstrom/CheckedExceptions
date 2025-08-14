using Microsoft.CodeAnalysis.Testing;

namespace Sundstrom.CheckedExceptions.Tests.BugFixes;

using Verifier = CSharpAnalyzerVerifier<CheckedExceptionsAnalyzer, DefaultVerifier>;

public class BugFix276
{
    [Fact]
    public async Task CollectExceptionsFromMethodGroup()
    {
        var test = /* lang=c#-test */ """
        #nullable enable
        using System;
        using System.Collections.Generic;
        using System.Linq;

        IEnumerable<string> strings = ["1", "2", "3", "4"];
        var numbers = strings.Select(int.Parse);
        var query = numbers.Where((x) => x % 2 == 1);
        var r = query.First();
        """;

        var expected = Verifier.UnhandledException("FormatException")
            .WithSpan(9, 15, 9, 22);

        var expected2 = Verifier.UnhandledException("InvalidOperationException")
            .WithSpan(9, 15, 9, 22);

        var expected3 = Verifier.UnhandledException("OverflowException")
            .WithSpan(9, 15, 9, 22);

        await Verifier.VerifyAnalyzerAsync(test, s =>
        {
            s.ExpectedDiagnostics.AddRange(expected, expected2, expected3);

            s.DisabledDiagnostics.Remove(CheckedExceptionsAnalyzer.DiagnosticIdRedundantTypedCatchClause);
            s.DisabledDiagnostics.Add(CheckedExceptionsAnalyzer.DiagnosticIdGeneralThrowDeclared);
        }, executable: true);
    }
}