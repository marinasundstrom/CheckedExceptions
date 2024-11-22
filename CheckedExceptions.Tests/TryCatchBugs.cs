using Microsoft.CodeAnalysis.Testing;

namespace Sundstrom.CheckedExceptions.Tests;

using Verifier = CSharpAnalyzerVerifier<CheckedExceptionsAnalyzer, DefaultVerifier>;

public partial class TryCatchBugs
{
    [Fact]
    public async Task Should_ReportDiagnostics_ForUnhandledThrowInCatch()
    {
        var test = /* lang=c#-test */ """
        using System;
        using System.IO;

        public class Test
        {
            [Throws(typeof(InvalidOperationException))]
            public void Foo1() 
            {
                throw new InvalidOperationException();
            }

            public void Foo()
            {
                try
                {
                    Foo1();
                }
                catch (InvalidOperationException e)
                {
                    throw new ArgumentException();
                }
            }
        }
        """;

        var expected = Verifier.Diagnostic("THROW001")
            .WithSpan(20, 13, 20, 43)
            .WithArguments("ArgumentException");

        await Verifier.VerifyAnalyzerAsync(test, expected);
    }

    [Fact(Skip = "Should fix this")]
    public async Task Should_ReportDiagnostics_ForUnhandledThrowingMethodFooInCatch()
    {
        var test = /* lang=c#-test */ """
        using System;
        using System.IO;

        public class Test
        {
            [Throws(typeof(InvalidOperationException))]
            public void Foo1() 
            {
                throw new InvalidOperationException();
            }

            public void Foo()
            {
                try
                {
                    Foo1();
                }
                catch (InvalidOperationException e)
                {
                    Foo1();
                }
            }
        }
        """;

        var expected = Verifier.Diagnostic("THROW001")
            .WithSpan(20, 13, 20, 43)
            .WithArguments("InvalidOperationException");

        await Verifier.VerifyAnalyzerAsync(test, expected);
    }
}