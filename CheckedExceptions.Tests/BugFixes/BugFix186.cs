using Microsoft.CodeAnalysis.Testing;

namespace Sundstrom.CheckedExceptions.Tests.BugFixes;

using Verifier = CSharpAnalyzerVerifier<CheckedExceptionsAnalyzer, DefaultVerifier>;

public class BugFix186
{
    [Fact]
    public async Task Test1()
    {
        var test = /* lang=c#-test */ """
        using System;

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


        var expected = Verifier.Diagnostic(CheckedExceptionsAnalyzer.DiagnosticIdThrowsDeclarationNotValidOnFullProperty)
            .WithSpan(5, 6, 5, 43);

        var expected2 = Verifier.Diagnostic(CheckedExceptionsAnalyzer.DiagnosticIdMissingThrowsOnBaseMember)
            .WithArguments("TestBase.get_Foo3()", "ArgumentNullException")
            .WithSpan(13, 24, 13, 45);

        await Verifier.VerifyAnalyzerAsync(test, s =>
        {
            s.ExpectedDiagnostics.AddRange(expected, expected2);

            s.DisabledDiagnostics.Remove(CheckedExceptionsAnalyzer.DiagnosticIdRedundantExceptionDeclaration);

            s.DisabledDiagnostics.Add(CheckedExceptionsAnalyzer.DiagnosticIdGeneralThrows);
        });
    }

    [Fact]
    public async Task Test2()
    {
        var test = /* lang=c#-test */ """
        using System;

        public class TestBase
        {
            public virtual bool Foo3 
            {
                [Throws(typeof(ArgumentNullException))] 
                get; 
                set; 
            }
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
    public async Task Test3()
    {
        var test = /* lang=c#-test */ """
        using System;

        public class TestBase
        {
            public virtual bool Foo3 
            {
                [Throws(typeof(ArgumentNullException))] 
                get; 
                set; 
            }
        }

        public class TestDerive : TestBase
        {
            public override bool Foo3
            {
                [Throws(typeof(InvalidOperationException))]
                get => throw new InvalidOperationException();
            }
        }
        """;

        var expected = Verifier.Diagnostic(CheckedExceptionsAnalyzer.DiagnosticIdMissingThrowsOnBaseMember)
            .WithArguments("TestBase.get_Foo3()", "InvalidOperationException")
            .WithSpan(17, 24, 17, 49);

        var expected2 = Verifier.Diagnostic(CheckedExceptionsAnalyzer.DiagnosticIdMissingThrowsFromBaseMember)
            .WithArguments("TestBase.get_Foo3()", "ArgumentNullException")
            .WithSpan(18, 9, 18, 12);

        await Verifier.VerifyAnalyzerAsync(test, s =>
        {
            s.ExpectedDiagnostics.AddRange(expected, expected2);

            s.DisabledDiagnostics.Remove(CheckedExceptionsAnalyzer.DiagnosticIdRedundantExceptionDeclaration);

            s.DisabledDiagnostics.Add(CheckedExceptionsAnalyzer.DiagnosticIdGeneralThrows);
        });
    }
}