using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Testing;

namespace Sundstrom.CheckedExceptions.Tests;

using Verifier = CSharpAnalyzerVerifier<CheckedExceptionsAnalyzer, DefaultVerifier>;

partial class CheckedExceptionsAnalyzerTests
{
    [Fact]
    public async Task UncaughtExceptions_ShouldReportDiagnostic()
    {
        var test = /* lang=c#-test */ """
            using System;

            public class TestClass
            {
                public void ThrowingMethod()
                {
                    throw new InvalidOperationException();
                }

                public void CallerMethod()
                {
                    ThrowingMethod(); // Diagnostic expected here
                }
            }
            """;

        var expected = Verifier.Diagnostic("THROW001")
            .WithSpan(7, 9, 7, 47)
            .WithArguments("InvalidOperationException");

        await Verifier.VerifyAnalyzerAsync(test, expected);
    }

    [Fact]
    public async Task CaughtExceptions_ShouldNotReportDiagnostic()
    {
        var test = /* lang=c#-test */ """
            using System;

            public class TestClass
            {
                [Throws(typeof(InvalidOperationException))]
                public void ThrowingMethod()
                {
                    throw new InvalidOperationException();
                }

                public void CallerMethod()
                {
                    try
                    {
                        ThrowingMethod(); // Exception is caught
                    }
                    catch (InvalidOperationException)
                    {
                        // Exception handled
                    }
                }
            }
            """;

        await Verifier.VerifyAnalyzerAsync(test);
    }

    [Fact]
    public async Task DeclaringExceptions_ShouldNotReportDiagnostic()
    {
        var test = /* lang=c#-test */ """
            using System;

            public class TestClass
            {
                [Throws(typeof(InvalidOperationException))]
                public void ThrowingMethod()
                {
                    throw new InvalidOperationException();
                }

                [Throws(typeof(InvalidOperationException))]
                public void CallerMethod()
                {
                    ThrowingMethod(); // Exception is declared
                }
            }
            """;

        await Verifier.VerifyAnalyzerAsync(test);
    }

    [Fact]
    public async Task MixedHandling_ShouldReportDiagnosticsForUnhandledExceptions()
    {
        var test = /* lang=c#-test */ """
            using System;

            public class TestClass
            {
                public void ThrowingMethod1()
                {
                    throw new InvalidOperationException();
                }

                public void ThrowingMethod2()
                {
                    throw new ArgumentException();
                }

                public void CallerMethod()
                {
                    try
                    {
                        ThrowingMethod1(); // Exception is caught
                    }
                    catch (InvalidOperationException)
                    {
                        // Exception handled
                    }

                    ThrowingMethod2(); // Diagnostic expected here
                }
            }
            """;

        var expected1 = Verifier.Diagnostic("THROW001")
            .WithSpan(7, 9, 7, 47)
            .WithArguments("InvalidOperationException");

        var expected2 = Verifier.Diagnostic("THROW001")
            .WithSpan(12, 9, 12, 39)
            .WithArguments("ArgumentException");

        await Verifier.VerifyAnalyzerAsync(test, expected1, expected2);
    }

    [Fact]
    public async Task DeclaringMultipleExceptions_ShouldNotReportDiagnostic()
    {
        var test = /* lang=c#-test */ """
        using System;

        public class TestClass
        {
            [Throws(typeof(InvalidOperationException))]
            public void ThrowingMethod1()
            {
                throw new InvalidOperationException();
            }

            [Throws(typeof(ArgumentException))]
            public void ThrowingMethod2()
            {
                throw new ArgumentException();
            }

            [Throws(typeof(InvalidOperationException))]
            [Throws(typeof(ArgumentException))]
            public void CallerMethod()
            {
                ThrowingMethod1();
                ThrowingMethod2();
            }
        }
        """;

        await Verifier.VerifyAnalyzerAsync(test);
    }


}
