using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Testing;

namespace Sundstrom.CheckedExceptions.Tests;

using Verifier = CSharpAnalyzerVerifier<CheckedExceptionsAnalyzer, DefaultVerifier>;

public partial class TryCatchTest
{
    [Fact]
    public async Task RedundantTypedCatchClause()
    {
        var test = /* lang=c#-test */ """
            #nullable enable
            using System;

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
            catch (ArgumentException argumentException)
            {
            }
            """;

        var expected = Verifier.RedundantTypedCatchClause("ArgumentException")
            .WithSpan(14, 8, 14, 25);

        var expected2 = Verifier.Diagnostic(CheckedExceptionsAnalyzer.DiagnosticIdRedundantCatchClause)
  .WithSpan(14, 1, 14, 6);

        await Verifier.VerifyAnalyzerAsync(test, setup: (ex) =>
        {
            ex.DisabledDiagnostics.Remove(CheckedExceptionsAnalyzer.DiagnosticIdRedundantTypedCatchClause);
            ex.DisabledDiagnostics.Remove(CheckedExceptionsAnalyzer.DiagnosticIdRedundantCatchClause);

            ex.ExpectedDiagnostics.AddRange(expected, expected2);
        }, executable: true);

        //await Verifier.VerifyAnalyzerAsync(test, executable: true);
    }
}