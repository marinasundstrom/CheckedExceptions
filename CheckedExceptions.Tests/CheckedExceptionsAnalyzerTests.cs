using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Testing;

namespace CheckedExceptions.Tests;

using Verifier = CSharpAnalyzerVerifier<CheckedExceptionsAnalyzer, DefaultVerifier>;

public partial class CheckedExceptionsAnalyzerTests
{
    // Test 1: Throwing an exception without handling or declaring
    [Fact]
    public async Task ThrowingExceptionWithoutDeclaration_ShouldReportDiagnostic()
    {
        var test = @"
using System;

public class TestClass
{
    public void TestMethod()
    {
        throw new InvalidOperationException();
    }
}";

        var expected = Verifier.Diagnostic("THROW001")
            .WithSpan(8, 9, 8, 47)
            .WithArguments("InvalidOperationException");

        await Verifier.VerifyAnalyzerAsync(test, expected);
    }

    // Test 2: Throwing a general exception without declaring
    [Fact]
    public async Task ThrowingGeneralExceptionWithoutDeclaration_ShouldReportDiagnostic()
    {
        var test = @"
using System;

public class TestClass
{
    public void TestMethod()
    {
        throw new Exception();
    }
}";

        var expected1 = Verifier.Diagnostic("THROW001")
            .WithSpan(8, 9, 8, 31)
            .WithArguments("Exception");

        var expected2 = Verifier.Diagnostic("THROW004")
            .WithSpan(8, 9, 8, 31)
            .WithArguments("Exception");

        await Verifier.VerifyAnalyzerAsync(test, expected1, expected2);
    }

    // Test 3: Declaring a general exception via [Throws] attribute
    [Fact]
    public async Task DeclaringGeneralException_ShouldReportDiagnostic()
    {
        var test = @"
using System;

public class TestClass
{
    [Throws(typeof(Exception))]
    public void TestMethod()
    {
        throw new Exception();
    }
}";

        var expected = Verifier.Diagnostic("THROW003")
            .WithSpan(6, 6, 6, 31)
            .WithArguments("Exception");

        await Verifier.VerifyAnalyzerAsync(test, expected);
    }

    // Test 4: Duplicate [Throws] attributes declaring the same exception
    [Fact]
    public async Task DuplicateThrowsAttributes_ShouldReportDiagnostic()
    {
        var test = @"
using System;

public class TestClass
{
    [Throws(typeof(InvalidOperationException))]
    [Throws(typeof(InvalidOperationException))]
    public void TestMethod()
    {
        throw new InvalidOperationException();
    }
}";

        var expected = Verifier.Diagnostic("THROW005")
            .WithSpan(7, 6, 7, 47)
            .WithArguments("InvalidOperationException");

        await Verifier.VerifyAnalyzerAsync(test, expected);
    }

    // Test 5: Properly handling an exception in a try-catch block
    [Fact]
    public async Task ExceptionHandledInTryCatch_ShouldNotReportDiagnostic()
    {
        var test = @"
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
}";

        await Verifier.VerifyAnalyzerAsync(test);
    }

    // Test 6: Properly declaring an exception via [Throws] attribute
    [Fact]
    public async Task ExceptionDeclaredViaThrowsAttribute_ShouldNotReportDiagnostic()
    {
        var test = @"
using System;

public class TestClass
{
    [Throws(typeof(InvalidOperationException))]
    public void TestMethod()
    {
        throw new InvalidOperationException();
    }
}";

        await Verifier.VerifyAnalyzerAsync(test);
    }

    // Test 7: Handling exceptions in lambdas
    [Fact]
    public async Task ExceptionHandledInLambda_ShouldNotReportDiagnostic()
    {
        var test = @"
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
}";

        await Verifier.VerifyAnalyzerAsync(test);
    }

    // Test 8: Declaring exceptions in XML documentation
    [Fact(Skip = "Not supported")]
    public async Task ExceptionDeclaredInXmlDocumentation_ShouldNotReportDiagnostic()
    {
        var test = @"
using System;

public class TestClass
{
    /// <summary>
    /// Performs an operation.
    /// </summary>
    /// <exception cref=""T:System.InvalidOperationException"">Thrown when the operation fails.</exception>
    public void TestMethod()
    {
        throw new InvalidOperationException();
    }
}";

        await Verifier.VerifyAnalyzerAsync(test);
    }

    // Test 9: Multiple exceptions declared via [Throws] attribute
    [Fact(Skip = "To be implemented")]
    public async Task MultipleExceptionsDeclaredViaThrowsAttribute_ShouldNotReportDiagnostic()
    {
        var test = @"
using System;

public class TestClass
{
    [Throws(typeof(InvalidOperationException), typeof(ArgumentException))]
    public void TestMethod()
    {
        throw new InvalidOperationException();
    }
}";

        await Verifier.VerifyAnalyzerAsync(test);
    }

    // Test 10: Throwing multiple exceptions with partial handling
    [Fact]
    public async Task ThrowingMultipleExceptionsWithPartialHandling_ShouldReportDiagnosticForUnhandled()
    {
        var test = @"
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
}";

        var expected1 = Verifier.Diagnostic("THROW001")
            .WithSpan(10, 13, 10, 51)
            .WithArguments("InvalidOperationException");

        var expected2 = Verifier.Diagnostic("THROW001")
            .WithSpan(17, 9, 17, 43)
            .WithArguments("ArgumentNullException");

        await Verifier.VerifyAnalyzerAsync(test, expected1, expected2);
    }

    [Fact]
    public async Task NestedTryCatch_ShouldReportDiagnosticForUnhandledException()
    {
        var test = @"
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
}";

        var expected = Verifier.Diagnostic("THROW001")
            .WithSpan(12, 17, 12, 55)
            .WithArguments("InvalidOperationException");

        await Verifier.VerifyAnalyzerAsync(test, expected);
    }

    [Fact]
    public async Task ThrowingCustomExceptionWithoutDeclaration_ShouldReportDiagnostic()
    {
        var test = @"
using System;

public class CustomException : Exception { }

public class TestClass
{
    public void TestMethod()
    {
        throw new CustomException();
    }
}";

        var expected = Verifier.Diagnostic("THROW001")
            .WithSpan(10, 9, 10, 37)
            .WithArguments("CustomException");

        await Verifier.VerifyAnalyzerAsync(test, expected);
    }
}