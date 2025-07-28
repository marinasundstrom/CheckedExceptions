using Microsoft.CodeAnalysis.Testing;

namespace Sundstrom.CheckedExceptions.Tests;

using Verifier = CSharpAnalyzerVerifier<CheckedExceptionsAnalyzer, DefaultVerifier>;

public partial class ExpressionBodiedPropertyTests
{
    [Fact]
    public async Task ExceptionIsUnhandled_WithDiagnostic()
    {
        var test = /* lang=c#-test */ """
            using System;

            public class TestClass
            {
                public int TestProp => Test(42);

                [Throws(typeof(InvalidOperationException))]
                public int Test(int x)
                {
                    return 0;
                }
            }
            """;

        var expected = Verifier.UnhandledException("InvalidOperationException")
            .WithSpan(5, 28, 5, 36);

        await Verifier.VerifyAnalyzerAsync(test, expected);
    }

    [Fact]
    public async Task ExceptionIsHandled_NoDiagnostic()
    {
        var test = /* lang=c#-test */ """
            using System;

            public class TestClass
            {
                [Throws(typeof(InvalidOperationException))]
                public int TestProp => Test(42);

                [Throws(typeof(InvalidOperationException))]
                public int Test(int x)
                {
                    return 0;
                }
            }
            """;

        await Verifier.VerifyAnalyzerAsync(test);
    }

    [Fact]
    public async Task InvalidThrowsDeclOnPropDeclWithAccessors()
    {
        var test = /* lang=c#-test */ """
            using System;

            public class TestClass
            {
                [Throws(typeof(InvalidOperationException))]
                public int TestProp
                {
                    get => Test(42);
                    set {}
                }

                [Throws(typeof(InvalidOperationException))]
                public int Test(int x)
                {
                    return 0;
                }
            }
            """;

        var expected = Verifier.UnhandledException("InvalidOperationException")
            .WithSpan(8, 16, 8, 24);

        var expected2 = Verifier.Diagnostic("THROW010")
            .WithSpan(5, 6, 5, 47);

        await Verifier.VerifyAnalyzerAsync(test, expected, expected2);
    }

    [Fact]
    public async Task InvalidThrowsDeclOnPropDeclThatLacksExpressionBody()
    {
        var test = /* lang=c#-test */ """
            using System;

            public class TestClass
            {
                [Throws(typeof(InvalidOperationException))]
                public int TestProp
                {
                    get
                    {
                        return Test(42);
                    }
                }

                [Throws(typeof(InvalidOperationException))]
                public int Test(int x)
                {
                    return 0;
                }
            }
            """;

        var expected = Verifier.UnhandledException("InvalidOperationException")
            .WithSpan(10, 20, 10, 28);

        var expected2 = Verifier.Diagnostic("THROW010")
            .WithSpan(5, 6, 5, 47);

        await Verifier.VerifyAnalyzerAsync(test, expected, expected2);
    }
}
