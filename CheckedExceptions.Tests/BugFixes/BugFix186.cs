using Microsoft.CodeAnalysis.Testing;

namespace Sundstrom.CheckedExceptions.Tests.BugFixes;

using Verifier = CSharpAnalyzerVerifier<CheckedExceptionsAnalyzer, DefaultVerifier>;

public class BugFix186
{
    [Fact]
    public async Task Test1()
    {
        var test = /* lang=c#-test */ """
        public class TestBase
        {
            [Throws(typeof(ArgumentNullException))]
            public virtual bool Foo3 { get; set; }
        }

        public class TestDerive : TestBase
        {
            public override bool Foo3
            {
                [Throws(typeof(ArgumentNullException))]
                get => throw new ArgumentNullException();
            }
        }
        """;

        await Verifier.VerifyAnalyzerAsync(test, s =>
        {
            s.DisabledDiagnostics.Remove(CheckedExceptionsAnalyzer.DiagnosticIdRedundantExceptionDeclaration);

            s.DisabledDiagnostics.Add(CheckedExceptionsAnalyzer.DiagnosticIdGeneralThrows);
        });
    }

    [Fact]
    public async Task Test2()
    {
        var test = /* lang=c#-test */ """
        public class TestBase
        {
            [Throws(typeof(ArgumentNullException))]
            public virtual bool Foo3 { get; set; }
        }

        public class TestDerive : TestBase
        {
            public override bool Foo3
            {
                get => throw new ArgumentNullException();
            }
        }
        """;

        await Verifier.VerifyAnalyzerAsync(test, s =>
        {
            s.DisabledDiagnostics.Remove(CheckedExceptionsAnalyzer.DiagnosticIdRedundantExceptionDeclaration);

            s.DisabledDiagnostics.Add(CheckedExceptionsAnalyzer.DiagnosticIdGeneralThrows);
        });
    }
}