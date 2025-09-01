using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Testing;

namespace Sundstrom.CheckedExceptions.Tests;

using Verifier = CSharpAnalyzerVerifier<CheckedExceptionsAnalyzer, DefaultVerifier>;

public partial class CheckedExceptionsAnalyzerTests
{
    [Fact]
    public async Task DeclaringExceptionWithSpecific_ShouldReportRedundantDiagnosticByDefault()
    {
        var test = /* lang=c#-test */ """
            using System;

            public class TestClass
            {
                [Throws(typeof(InvalidOperationException), typeof(Exception))]
                public void TestMethod()
                {
                    // Throws "FormatException" and "OverflowException"
                    var x = int.Parse("42");
                    throw new InvalidOperationException();
                }
            }
            """;

        var expectedRedundant = Verifier.RedundantExceptionDeclarationBySuperType("Exception")
            .WithSpan(5, 20, 5, 45);

        await Verifier.VerifyAnalyzerAsync(test, expectedRedundant);
    }

    [Fact]
    public async Task DeclaringExceptionWithSpecific_TreatAsCatchRest_ShouldNotReportDiagnostics()
    {
        var test = /* lang=c#-test */ """
            using System;

            public class TestClass
            {
                [Throws(typeof(InvalidOperationException), typeof(Exception))]
                public void TestMethod()
                {
                    // Throws "FormatException" and "OverflowException"
                    var x = int.Parse("42");
                    throw new InvalidOperationException();
                }
            }
            """;

        await Verifier.VerifyAnalyzerAsync(test, t =>
        {
            t.TestState.AdditionalFiles.Add(("CheckedExceptions.settings.json", """
            {
                "ignoredExceptions": [],
                "informationalExceptions": {},
                "treatThrowsExceptionAsCatchRest": true
            }
            """));
        });
    }
}