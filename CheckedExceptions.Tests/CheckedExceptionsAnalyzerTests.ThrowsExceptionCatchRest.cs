using System.Linq;

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
    public async Task DeclaringExceptionWithSpecific_TreatAsCatchRest_ShouldReportBaseDiagnostic()
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

        var expected = Verifier.AvoidDeclaringTypeException()
            .WithSpan(5, 55, 5, 64);

        await Verifier.VerifyAnalyzerAsync(test, t =>
        {
            t.TestState.AdditionalFiles.Add(("CheckedExceptions.settings.json", """
            {
                "defaultExceptionClassification": "Strict",
                "exceptions": {},
                "treatThrowsExceptionAsCatchRest": true
            }
            """));

            var allDiagnostics = CheckedExceptionsAnalyzer.AllDiagnosticsIds;
            t.DisabledDiagnostics.Add(CheckedExceptionsAnalyzer.DiagnosticIdRedundantTypedCatchClause);
            t.DisabledDiagnostics.Add(CheckedExceptionsAnalyzer.DiagnosticIdRedundantExceptionDeclaration);
            t.DisabledDiagnostics.Add(CheckedExceptionsAnalyzer.DiagnosticIdUnreachableCode);
            t.DisabledDiagnostics.Add(CheckedExceptionsAnalyzer.DiagnosticIdUnreachableCodeHidden);
            t.DisabledDiagnostics.AddRange(allDiagnostics.Except(new[] { expected.Id }));

            t.ExpectedDiagnostics.Add(expected);
        });
    }
}