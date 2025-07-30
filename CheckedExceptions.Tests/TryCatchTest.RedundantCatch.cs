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

        var expected = Verifier.RedundantTypedCatchClause("System.ArgumentException")
            .WithSpan(14, 8, 14, 25);

        await Verifier.VerifyAnalyzerAsync(test, setup: (ex) =>
        {
            ex.DisabledDiagnostics.Remove(CheckedExceptionsAnalyzer.DiagnosticIdRedundantTypedCatchClause);

            ex.ExpectedDiagnostics.Add(expected);
        }, executable: true);

        //await Verifier.VerifyAnalyzerAsync(test, executable: true);
    }
}