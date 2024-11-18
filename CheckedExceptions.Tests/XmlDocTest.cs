using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Testing;

namespace CheckedExceptions.Tests;

using Verifier = CSharpAnalyzerVerifier<CheckedExceptionsAnalyzer, DefaultVerifier>;

public partial class XmlDocTest
{

    // Test 8: Declaring exceptions in XML documentation
    [Fact]
    public async Task ExceptionDeclaredInXmlDocumentation_ShouldNotReportDiagnostic()
    {
        var test = /* lang=c#-test */ """
            using System;

            public class TestClass
            {
                /// <summary>
                /// Performs an operation.
                /// </summary>
                /// <exception cref="T:System.InvalidOperationException">Thrown when the operation fails.</exception>
                public void TestMethod()
                {

                }

                public void TestMethod2()
                {
                    TestMethod();
                }
            }
            """;

        var expected = Verifier.Diagnostic("THROW001")
            .WithSpan(16, 9, 16, 21)
            .WithArguments("InvalidOperationException");

        await Verifier.VerifyAnalyzerAsync(test, expected);
    }

    // Test 8: Declaring exceptions in XML documentation
    [Fact]
    public async Task ExceptionDeclaredInXmlDocumentation_ShouldNotReportDiagnostic2()
    {
        var test = /* lang=c#-test */ """
            using System;

            public class TestClass
            {
                public void TestMethod2()
                {
                    int.Parse("Foo");
                }
            }
            """;

        var expected1 = Verifier.Diagnostic("THROW001")
            .WithSpan(7, 9, 7, 25)
            .WithArguments("ArgumentNullException");

        var expected2 = Verifier.Diagnostic("THROW001")
            .WithSpan(7, 9, 7, 25)
            .WithArguments("FormatException");

        var expected3 = Verifier.Diagnostic("THROW001")
            .WithSpan(7, 9, 7, 25)
            .WithArguments("OverflowException");

        await Verifier.VerifyAnalyzerAsync(test, expected1, expected2, expected3);
    }
}