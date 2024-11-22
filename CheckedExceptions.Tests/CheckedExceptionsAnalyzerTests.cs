using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Testing;

namespace Sundstrom.CheckedExceptions.Tests;

using Verifier = CSharpAnalyzerVerifier<CheckedExceptionsAnalyzer, DefaultVerifier>;

public partial class CheckedExceptionsAnalyzerTests
{
    // Test 1: Throwing an exception without handling or declaring
    [Fact]
    public async Task ThrowingExceptionWithoutDeclaration_ShouldReportDiagnostic()
    {
        var test = /* lang=c#-test */ """
            using System;

            public class TestClass
            {
                public void TestMethod()
                {
                    throw new InvalidOperationException();
                }
            }
            """;

        var expected = Verifier.IsThrown("InvalidOperationException")
            .WithSpan(7, 9, 7, 47);

        await Verifier.VerifyAnalyzerAsync(test, expected);
    }

    // Test 2: Throwing a general exception without declaring
    [Fact]
    public async Task ThrowingGeneralExceptionWithoutDeclaration_ShouldReportDiagnostic()
    {
        var test = /* lang=c#-test */ """
            using System;

            public class TestClass
            {
                public void TestMethod()
                {
                    throw new Exception();
                }
            }
            """;

        var expected1 = Verifier.IsThrown("Exception")
            .WithSpan(7, 9, 7, 31);

        var expected2 = Verifier.Diagnostic("THROW004")
            .WithSpan(7, 9, 7, 31)
            .WithArguments("Exception");

        await Verifier.VerifyAnalyzerAsync(test, expected1, expected2);
    }

    // Test 3: Declaring a general exception via [Throws] attribute
    [Fact]
    public async Task DeclaringGeneralException_ShouldReportDiagnostic()
    {
        var test = /* lang=c#-test */ """
            using System;

            public class TestClass
            {
                [Throws(typeof(Exception))]
                public void TestMethod()
                {
                    throw new Exception();
                }
            }
            """;

        var expected = Verifier.Diagnostic("THROW003")
            .WithSpan(5, 6, 5, 31)
            .WithArguments("Exception");

        await Verifier.VerifyAnalyzerAsync(test, expected);
    }

    // Test 4: Duplicate [Throws] attributes declaring the same exception
    [Fact]
    public async Task DuplicateThrowsAttributes_ShouldReportDiagnostic()
    {
        var test = /* lang=c#-test */ """
            using System;

            public class TestClass
            {
                [Throws(typeof(InvalidOperationException))]
                [Throws(typeof(InvalidOperationException))]
                public void TestMethod()
                {
                    throw new InvalidOperationException();
                }
            }
            """;

        var expected = Verifier.Diagnostic("THROW005")
            .WithSpan(6, 6, 6, 47)
            .WithArguments("InvalidOperationException");

        await Verifier.VerifyAnalyzerAsync(test, expected);
    }

    // Test 5: Properly handling an exception in a try-catch block
    [Fact]
    public async Task ExceptionHandledInTryCatch_ShouldNotReportDiagnostic()
    {
        var test = /* lang=c#-test */ """
        using System;

        public class TestClass
        {
            public void TestMethod()
            {
                try
                {
                    throw new InvalidOperationException();
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

    // Test 6: Properly declaring an exception via [Throws] attribute
    [Fact]
    public async Task ExceptionDeclaredViaThrowsAttribute_ShouldNotReportDiagnostic()
    {
        var test = /* lang=c#-test */ """
            using System;

            public class TestClass
            {
                [Throws(typeof(InvalidOperationException))]
                public void TestMethod()
                {
                    throw new InvalidOperationException();
                }
            }
            """;

        await Verifier.VerifyAnalyzerAsync(test);
    }

    // Test 7: Handling exceptions in lambdas
    [Fact]
    public async Task ExceptionHandledInLambda_ShouldNotReportDiagnostic()
    {
        var test = /* lang=c#-test */ """
        using System;

        public class TestClass
        {
            public void TestMethod()
            {
                Action action = () =>
                {
                    try
                    {
                        throw new InvalidOperationException();
                    }
                    catch (InvalidOperationException)
                    {
                        // Exception handled
                    }
                };

                action();
            }
        }
        """;

        await Verifier.VerifyAnalyzerAsync(test);
    }

    // Test 9: Multiple exceptions declared via [Throws] attribute
    [Fact(Skip = "To be implemented")]
    public async Task MultipleExceptionsDeclaredViaThrowsAttribute_ShouldNotReportDiagnostic()
    {
        var test = /* lang=c#-test */ """
            using System;

            public class TestClass
            {
                [Throws(typeof(InvalidOperationException), typeof(ArgumentException))]
                public void TestMethod()
                {
                    throw new InvalidOperationException();
                }
            }
            """;

        await Verifier.VerifyAnalyzerAsync(test);
    }

    // Test 10: Throwing multiple exceptions with partial handling
    [Fact]
    public async Task ThrowingMultipleExceptionsWithPartialHandling_ShouldReportDiagnosticForUnhandled()
    {
        var test = /* lang=c#-test */ """
            using System;

            public class TestClass
            {
                public void TestMethod()
                {
                    try
                    {
                        throw new InvalidOperationException();
                    }
                    catch (ArgumentException)
                    {
                        // Handle ArgumentException only
                    }

                    throw new ArgumentNullException();
                }
            }
            """;

        var expected1 = Verifier.IsThrown("InvalidOperationException")
            .WithSpan(9, 13, 9, 51);

        var expected2 = Verifier.IsThrown("ArgumentNullException")
            .WithSpan(16, 9, 16, 43);

        await Verifier.VerifyAnalyzerAsync(test, expected1, expected2);
    }

    [Fact]
    public async Task NestedTryCatch_ShouldReportDiagnosticForUnhandledException()
    {
        var test = /* lang=c#-test */ """
            using System;

            public class TestClass
            {
                public void TestMethod()
                {
                    try
                    {
                        try
                        {
                            throw new InvalidOperationException();
                        }
                        catch (ArgumentException)
                        {
                            // Handle ArgumentException only
                        }
                    }
                    catch (ArgumentNullException)
                    {
                        // Handle ArgumentNullException only
                    }
                }
            }
            """;

        var expected = Verifier.IsThrown("InvalidOperationException")
            .WithSpan(11, 17, 11, 55);

        await Verifier.VerifyAnalyzerAsync(test, expected);
    }

    [Fact]
    public async Task ThrowingCustomExceptionWithoutDeclaration_ShouldReportDiagnostic()
    {
        var test = /* lang=c#-test */ """
            using System;

            public class CustomException : Exception { }

            public class TestClass
            {
                public void TestMethod()
                {
                    throw new CustomException();
                }
            }
            """;

        var expected = Verifier.IsThrown("CustomException")
            .WithSpan(9, 9, 9, 37);

        await Verifier.VerifyAnalyzerAsync(test, expected);
    }
}