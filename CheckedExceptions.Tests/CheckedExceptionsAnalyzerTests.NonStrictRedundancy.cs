using Microsoft.CodeAnalysis.Testing;

namespace Sundstrom.CheckedExceptions.Tests;

using Verifier = CSharpAnalyzerVerifier<CheckedExceptionsAnalyzer, DefaultVerifier>;

public partial class CheckedExceptionsAnalyzerTests
{
    [Fact]
    public async Task DeclaringNonStrictException_ShouldNotReportRedundantDeclaration()
    {
        var test = /* lang=c#-test */ """
            using System;

            public class TestClass
            {
                [Throws(typeof(InvalidOperationException))]
                public void Test()
                {
                    throw new InvalidOperationException();
                }
            }
            """;

        await Verifier.VerifyAnalyzerAsync(test, t =>
        {
            t.DisabledDiagnostics.Remove(CheckedExceptionsAnalyzer.DiagnosticIdRedundantExceptionDeclaration);
            t.DisabledDiagnostics.Add(CheckedExceptionsAnalyzer.DiagnosticIdIgnoredException);
            t.TestState.AdditionalFiles.Add(("CheckedExceptions.settings.json", """
            {
                "defaultExceptionClassification": "Strict",
                "exceptions": {"System.InvalidOperationException": "NonStrict"}
            }
            """));
        });
    }

    [Fact]
    public async Task CatchingNonStrictException_TypedCatch_ShouldNotReportRedundantCatch()
    {
        var test = /* lang=c#-test */ """
            using System;

            public class TestClass
            {
                public void Test()
                {
                    try
                    {
                        throw new InvalidOperationException();
                    }
                    catch (InvalidOperationException)
                    {
                    }
                }
            }
            """;

        await Verifier.VerifyAnalyzerAsync(test, t =>
        {
            t.DisabledDiagnostics.Remove(CheckedExceptionsAnalyzer.DiagnosticIdRedundantTypedCatchClause);
            t.DisabledDiagnostics.Add(CheckedExceptionsAnalyzer.DiagnosticIdIgnoredException);
            t.TestState.AdditionalFiles.Add(("CheckedExceptions.settings.json", """
            {
                "defaultExceptionClassification": "Strict",
                "exceptions": {"System.InvalidOperationException": "NonStrict"}
            }
            """));
        });
    }

    [Fact]
    public async Task CatchingNonStrictException_CatchAll_ShouldNotReportRedundantCatchAll()
    {
        var test = /* lang=c#-test */ """
            using System;

            public class TestClass
            {
                public void Test()
                {
                    try
                    {
                        throw new InvalidOperationException();
                    }
                    catch
                    {
                    }
                }
            }
            """;

        await Verifier.VerifyAnalyzerAsync(test, t =>
        {
            t.DisabledDiagnostics.Remove(CheckedExceptionsAnalyzer.DiagnosticIdRedundantCatchAllClause);
            t.DisabledDiagnostics.Add(CheckedExceptionsAnalyzer.DiagnosticIdIgnoredException);
            t.TestState.AdditionalFiles.Add(("CheckedExceptions.settings.json", """
            {
                "defaultExceptionClassification": "Strict",
                "exceptions": {"System.InvalidOperationException": "NonStrict"}
            }
            """));
        });
    }
}
