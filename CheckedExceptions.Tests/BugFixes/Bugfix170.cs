using Microsoft.CodeAnalysis.Testing;

namespace Sundstrom.CheckedExceptions.Tests.BugFixes;

using Verifier = CSharpAnalyzerVerifier<CheckedExceptionsAnalyzer, DefaultVerifier>;

public partial class Bugfix170
{
    [Fact]
    public async Task SingleExceptionTypeInThrowsAttribute_ShouldNotReportDiagnostic()
    {
        var test = /* lang=c#-test */ """
        using System;

        public class Test
        {
            [Throws(typeof(InvalidOperationException))]
            public static void NewMethod()
            {
                throw new ObjectDisposedException("");
            }
        }
        """;

        await Verifier.VerifyAnalyzerAsync(test, opt =>
        {
            opt.DisabledDiagnostics.Remove(CheckedExceptionsAnalyzer.DiagnosticIdRedundantExceptionDeclaration);
        });
    }
}