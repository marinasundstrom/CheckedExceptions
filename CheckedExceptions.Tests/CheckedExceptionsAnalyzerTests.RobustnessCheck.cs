using Microsoft.CodeAnalysis.Testing;

namespace Sundstrom.CheckedExceptions.Tests;

using Verifier = CSharpAnalyzerVerifier<CheckedExceptionsAnalyzer, DefaultVerifier>;

partial class CheckedExceptionsAnalyzerTests
{
    [Fact]
    public async Task DeclareExceptionOnExpressionBodiesPropThatDoesntThrowType_Diagnostic()
    {
        var test = /* lang=c#-test */ """
        using System;

        public class TestBase
        {
            [Throws(typeof(ArgumentException))]
            public bool Foo2 => true;
        }
        """;

        var expected = Verifier.Diagnostic(CheckedExceptionsAnalyzer.DiagnosticIdRedundantExceptionDeclaration)
            .WithArguments("ArgumentException")
            .WithSpan(5, 20, 5, 37);

        await Verifier.VerifyAnalyzerAsync(test, s =>
        {
            s.ExpectedDiagnostics.Add(expected);

            s.DisabledDiagnostics.Remove(CheckedExceptionsAnalyzer.DiagnosticIdRedundantExceptionDeclaration);

            s.DisabledDiagnostics.Add(CheckedExceptionsAnalyzer.DiagnosticIdGeneralThrows);
        });
    }

    [Fact]
    public async Task DeclareExceptionOnExpressionBodiesPropThatThrowsType_NoDiagnostic()
    {
        var test = /* lang=c#-test */ """
        using System;

        public class TestBase
        {
            [Throws(typeof(ArgumentException))]
            public bool Foo2 => throw new ArgumentException();
        }
        """;

        await Verifier.VerifyAnalyzerAsync(test, s =>
        {
            s.DisabledDiagnostics.Remove(CheckedExceptionsAnalyzer.DiagnosticIdRedundantExceptionDeclaration);

            s.DisabledDiagnostics.Add(CheckedExceptionsAnalyzer.DiagnosticIdGeneralThrows);
        });
    }


    [Fact]
    public async Task DeclareThrowsOnVirtualFullProperty_NoDiagnostic()
    {
        var test = /* lang=c#-test */ """
        using System;

        public class TestBase
        {
            [Throws(typeof(ArgumentException))]
            public virtual bool Foo2 { get; set; }
        }
        """;

        await Verifier.VerifyAnalyzerAsync(test, s =>
        {
            s.DisabledDiagnostics.Remove(CheckedExceptionsAnalyzer.DiagnosticIdRedundantExceptionDeclaration);

            s.DisabledDiagnostics.Add(CheckedExceptionsAnalyzer.DiagnosticIdGeneralThrows);
        });
    }
}